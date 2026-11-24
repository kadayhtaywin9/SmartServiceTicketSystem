using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SmartTicketApi.Hubs;

[Authorize]
public class TicketHub : Hub
{
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> ActiveViewers = new();

    public async Task JoinTicketRoom(string ticketId, string agentName)
    {
        string publicGroup = $"ticket-{ticketId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, publicGroup);

        string role = Context.User?.FindFirstValue(ClaimTypes.Role) ?? "EndUser";
        if (role != "EndUser")
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"ticket-{ticketId}-staff");
        }

        var viewers = ActiveViewers.GetOrAdd(ticketId, _ => new ConcurrentDictionary<string, string>());
        viewers[Context.ConnectionId] = agentName;

        await Clients.Group(publicGroup).SendAsync("PresenceUpdated", viewers.Values.Distinct().ToList());
    }

    public async Task LeaveTicketRoom(string ticketId)
    {
        string publicGroup = $"ticket-{ticketId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, publicGroup);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"ticket-{ticketId}-staff");

        if (ActiveViewers.TryGetValue(ticketId, out var viewers))
        {
            viewers.TryRemove(Context.ConnectionId, out _);
            await Clients.Group(publicGroup).SendAsync("PresenceUpdated", viewers.Values.Distinct().ToList());
        }
    }

    public async Task NotifyTyping(string ticketId, string agentName, bool isTyping)
    {
        await Clients.OthersInGroup($"ticket-{ticketId}").SendAsync("AgentTyping", new { agentName, isTyping });
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var kvp in ActiveViewers)
        {
            if (kvp.Value.TryRemove(Context.ConnectionId, out _))
            {
                await Clients.Group($"ticket-{kvp.Key}").SendAsync("PresenceUpdated", kvp.Value.Values.Distinct().ToList());
            }
        }
        await base.OnDisconnectedAsync(exception);
    }
}
