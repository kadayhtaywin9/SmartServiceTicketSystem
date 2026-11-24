using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartTicketApi.Data;
using SmartTicketApi.Entities;
using SmartTicketApi.Hubs;

namespace SmartTicketApi.BackgroundJobs;

public class SlaEscalationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<TicketHub> _hubContext;
    private readonly ILogger<SlaEscalationWorker> _logger;

    public SlaEscalationWorker(
        IServiceScopeFactory scopeFactory,
        IHubContext<TicketHub> hubContext,
        ILogger<SlaEscalationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckSlaAndEscalateAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("SLA Worker waiting for database: {Message}", ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
        }
    }

    private async Task CheckSlaAndEscalateAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        DateTime now = DateTime.UtcNow;

        var activeTickets = await db.Tickets
            .Include(t => t.Department)
            .Where(t => t.Status != "Resolved" && t.Status != "Closed")
            .ToListAsync(ct);

        foreach (var ticket in activeTickets)
        {
            double minsRemaining = (ticket.ResolutionDueAt - now).TotalMinutes;

            if (minsRemaining <= 30 && minsRemaining > 0 && ticket.EscalationLevel == 0)
            {
                ticket.EscalationLevel = 1;
                if (ticket.Department.LeadUserId.HasValue)
                    ticket.AssigneeId = ticket.Department.LeadUserId;

                db.TicketAuditLogs.Add(new TicketAuditLog
                {
                    TicketId = ticket.Id,
                    ActorName = "SLA Escalation Engine",
                    Action = "SLA_TIER1_ESCALATED",
                    NewValue = JsonSerializer.Serialize(new { EscalationLevel = 1, MinsRemaining = Math.Round(minsRemaining) }),
                    IpAddress = "127.0.0.1"
                });

                await _hubContext.Clients.All.SendAsync("SlaAlert", new
                {
                    ticket.Id,
                    ticket.TicketCode,
                    Severity = "Warning",
                    Message = $"{ticket.TicketCode} escalated to Department Lead ({Math.Round(minsRemaining)}m left)."
                }, ct);
            }
            else if (minsRemaining <= 0 && !ticket.IsSlaBreached)
            {
                ticket.IsSlaBreached = true;
                ticket.EscalationLevel = 2;
                ticket.Priority = "Critical";

                db.TicketAuditLogs.Add(new TicketAuditLog
                {
                    TicketId = ticket.Id,
                    ActorName = "SLA Escalation Engine",
                    Action = "SLA_BREACHED_CRITICAL",
                    NewValue = JsonSerializer.Serialize(new { IsSlaBreached = true, EscalationLevel = 2 }),
                    IpAddress = "127.0.0.1"
                });

                await _hubContext.Clients.All.SendAsync("SlaAlert", new
                {
                    ticket.Id,
                    ticket.TicketCode,
                    Severity = "Breached",
                    Message = $"{ticket.TicketCode} breached SLA and escalated to Critical!"
                }, ct);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
