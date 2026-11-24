using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartTicketApi.Data;
using SmartTicketApi.DTOs;
using SmartTicketApi.Entities;
using SmartTicketApi.Hubs;

namespace SmartTicketApi.Services;

public class TicketService
{
    private readonly AppDbContext _db;
    private readonly AiIntelligenceService _aiService;
    private readonly SlaCalculatorService _slaService;
    private readonly PiiMaskingService _piiService;
    private readonly IHubContext<TicketHub> _hubContext;

    public TicketService(
        AppDbContext db,
        AiIntelligenceService aiService,
        SlaCalculatorService slaService,
        PiiMaskingService piiService,
        IHubContext<TicketHub> hubContext)
    {
        _db = db;
        _aiService = aiService;
        _slaService = slaService;
        _piiService = piiService;
        _hubContext = hubContext;
    }

    public async Task<Ticket> CreateTicketAsync(CreateTicketRequest req, int requesterId, string actorName, string ipAddress)
    {
        var (maskedSubject, _) = _piiService.MaskSensitiveData(req.Subject);
        var (maskedDesc, descMasked) = _piiService.MaskSensitiveData(req.Description);

        var triage = await _aiService.AnalyzeAndRouteTicketAsync(maskedSubject, maskedDesc, req.Priority, req.DepartmentId);

        var slaPolicy = await _db.SlaPolicies.FirstOrDefaultAsync(s => s.Priority == triage.RecommendedPriority)
                        ?? await _db.SlaPolicies.FirstAsync();

        DateTime now = DateTime.UtcNow;
        var ticket = new Ticket
        {
            TicketCode = $"TKT-{now:yyyy}-{Random.Shared.Next(100000, 999999)}",
            RequesterId = requesterId,
            AssigneeId = triage.AssigneeId,
            DepartmentId = triage.DepartmentId,
            SlaPolicyId = slaPolicy.Id,
            SourceChannel = req.SourceChannel ?? "WebPortal",
            Subject = maskedSubject,
            Description = maskedDesc,
            Status = triage.AssigneeId.HasValue ? "In_Progress" : "Open",
            Priority = triage.RecommendedPriority,
            SentimentLabel = triage.SentimentLabel,
            SentimentScore = triage.SentimentScore,
            AiReasoning = triage.ReasoningTrace,
            FirstResponseDueAt = _slaService.CalculateDueDate(now, slaPolicy.FirstResponseMins, slaPolicy.BusinessHoursOnly),
            ResolutionDueAt = _slaService.CalculateDueDate(now, slaPolicy.ResolutionMins, slaPolicy.BusinessHoursOnly),
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync();

        _db.TicketMessages.Add(new TicketMessage
        {
            TicketId = ticket.Id,
            SenderId = requesterId,
            Body = maskedDesc,
            IsInternal = false,
            ContainsMaskedPii = descMasked,
            CreatedAt = now
        });

        _db.TicketAuditLogs.Add(new TicketAuditLog
        {
            TicketId = ticket.Id,
            ActorId = requesterId,
            ActorName = actorName,
            Action = "TICKET_CREATED",
            OldValue = null,
            NewValue = JsonSerializer.Serialize(new { ticket.TicketCode, ticket.Priority, ticket.SentimentLabel }),
            IpAddress = ipAddress,
            CreatedAt = now
        });

        await _db.SaveChangesAsync();
        await _hubContext.Clients.All.SendAsync("TicketCreated", new { ticket.Id, ticket.TicketCode, ticket.Subject, ticket.Priority });

        return ticket;
    }

    public async Task<TicketMessage> AddMessageAsync(long ticketId, AddMessageRequest req, int senderId, string actorName, string role, string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(req.Body))
            throw new ArgumentException("Message body cannot be empty.");

        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId)
                     ?? throw new KeyNotFoundException("Ticket not found.");

        if (role == "EndUser")
        {
            if (ticket.RequesterId != senderId)
                throw new UnauthorizedAccessException("You can only reply to your own tickets.");
            if (req.IsInternal)
                throw new UnauthorizedAccessException("EndUsers cannot post internal notes.");
        }

        var (maskedBody, wasMasked) = _piiService.MaskSensitiveData(req.Body.Trim());
        DateTime now = DateTime.UtcNow;

        var message = new TicketMessage
        {
            TicketId = ticketId,
            SenderId = senderId,
            Body = maskedBody,
            IsInternal = req.IsInternal,
            ContainsMaskedPii = wasMasked,
            CreatedAt = now
        };

        // Smart Status & First-Response SLA transitions on reply
        if (role != "EndUser")
        {
            if (!req.IsInternal && !ticket.FirstRespondedAt.HasValue)
                ticket.FirstRespondedAt = now;

            if (ticket.Status == "Open")
                ticket.Status = "In_Progress";

            if (!ticket.AssigneeId.HasValue)
                ticket.AssigneeId = senderId;
        }
        else if (ticket.Status == "Pending_Customer")
        {
            ticket.Status = "In_Progress";
        }

        ticket.UpdatedAt = now;
        _db.TicketMessages.Add(message);

        _db.TicketAuditLogs.Add(new TicketAuditLog
        {
            TicketId = ticketId,
            ActorId = senderId,
            ActorName = actorName,
            Action = req.IsInternal ? "INTERNAL_NOTE_ADDED" : "PUBLIC_REPLY_ADDED",
            NewValue = JsonSerializer.Serialize(new { Type = req.IsInternal ? "Internal Note" : "Public Reply", PiiMasked = wasMasked }),
            IpAddress = ipAddress,
            CreatedAt = now
        });

        await _db.SaveChangesAsync();

        var payload = new
        {
            message.Id,
            message.TicketId,
            message.SenderId,
            SenderName = actorName,
            message.Body,
            message.IsInternal,
            message.ContainsMaskedPii,
            message.CreatedAt
        };

        // Strictly isolate internal notes to staff-only SignalR room!
        string targetGroup = req.IsInternal ? $"ticket-{ticketId}-staff" : $"ticket-{ticketId}";
        await _hubContext.Clients.Group(targetGroup).SendAsync("ReceiveMessage", payload);

        return message;
    }

    public async Task UpdateStatusWithCascadeAsync(long ticketId, UpdateTicketStatusRequest req, int actorId, string actorName, string ipAddress)
    {
        var ticket = await _db.Tickets
            .Include(t => t.ChildTickets)
            .FirstOrDefaultAsync(t => t.Id == ticketId) ?? throw new KeyNotFoundException("Ticket not found.");

        string oldStatus = ticket.Status;
        if (string.Equals(oldStatus, req.Status, StringComparison.OrdinalIgnoreCase))
            return;

        using var transaction = await _db.Database.BeginTransactionAsync();

        ticket.Status = req.Status;
        ticket.UpdatedAt = DateTime.UtcNow;

        if (req.Status == "Resolved" || req.Status == "Closed")
            ticket.ResolvedAt ??= DateTime.UtcNow;

        _db.TicketAuditLogs.Add(new TicketAuditLog
        {
            TicketId = ticket.Id,
            ActorId = actorId,
            ActorName = actorName,
            Action = "STATUS_CHANGED",
            OldValue = JsonSerializer.Serialize(new { Status = oldStatus }),
            NewValue = JsonSerializer.Serialize(new { Status = req.Status }),
            IpAddress = ipAddress
        });

        if ((req.Status == "Resolved" || req.Status == "Closed") && ticket.ChildTickets.Any())
        {
            foreach (var child in ticket.ChildTickets)
            {
                if (child.Status != "Resolved" && child.Status != "Closed")
                {
                    string childOldStatus = child.Status;
                    child.Status = req.Status;
                    child.ResolvedAt ??= DateTime.UtcNow;
                    child.UpdatedAt = DateTime.UtcNow;

                    string cascadeNote = !string.IsNullOrWhiteSpace(req.ResolutionNote)
                        ? $"[Auto-Resolved via Master Incident {ticket.TicketCode}]: {req.ResolutionNote}"
                        : $"[Auto-Resolved via Master Incident {ticket.TicketCode}]: Root incident resolved.";

                    _db.TicketMessages.Add(new TicketMessage
                    {
                        TicketId = child.Id,
                        SenderId = actorId,
                        Body = cascadeNote,
                        IsInternal = false,
                        CreatedAt = DateTime.UtcNow
                    });

                    _db.TicketAuditLogs.Add(new TicketAuditLog
                    {
                        TicketId = child.Id,
                        ActorId = actorId,
                        ActorName = actorName,
                        Action = "PARENT_CASCADE_RESOLVED",
                        OldValue = JsonSerializer.Serialize(new { Status = childOldStatus }),
                        NewValue = JsonSerializer.Serialize(new { Status = req.Status, ParentCode = ticket.TicketCode }),
                        IpAddress = ipAddress
                    });
                }
            }
        }

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        await _hubContext.Clients.All.SendAsync("TicketUpdated", new { ticketId, status = req.Status });
    }

    public async Task AssignTicketAsync(long ticketId, int? assigneeId, int actorId, string actorName, string ipAddress)
    {
        var ticket = await _db.Tickets.Include(t => t.Assignee).FirstOrDefaultAsync(t => t.Id == ticketId)
                     ?? throw new KeyNotFoundException("Ticket not found.");

        if (ticket.AssigneeId == assigneeId) return;

        string oldAssignee = ticket.Assignee?.FullName ?? "Unassigned";
        User? newAgent = assigneeId.HasValue ? await _db.Users.FindAsync(assigneeId.Value) : null;
        string newAssignee = newAgent?.FullName ?? "Unassigned";

        ticket.AssigneeId = assigneeId;
        ticket.UpdatedAt = DateTime.UtcNow;

        _db.TicketAuditLogs.Add(new TicketAuditLog
        {
            TicketId = ticket.Id,
            ActorId = actorId,
            ActorName = actorName,
            Action = "TICKET_REASSIGNED",
            OldValue = JsonSerializer.Serialize(new { Assignee = oldAssignee }),
            NewValue = JsonSerializer.Serialize(new { Assignee = newAssignee }),
            IpAddress = ipAddress
        });

        await _db.SaveChangesAsync();
        await _hubContext.Clients.All.SendAsync("TicketUpdated", new { ticketId });
    }

    public async Task UpdatePriorityAsync(long ticketId, string priority, int actorId, string actorName, string ipAddress)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId)
                     ?? throw new KeyNotFoundException("Ticket not found.");

        if (string.Equals(ticket.Priority, priority, StringComparison.OrdinalIgnoreCase)) return;

        string oldPrio = ticket.Priority;
        ticket.Priority = priority;
        ticket.UpdatedAt = DateTime.UtcNow;

        var slaPolicy = await _db.SlaPolicies.FirstOrDefaultAsync(s => s.Priority == priority);
        if (slaPolicy != null)
        {
            ticket.SlaPolicyId = slaPolicy.Id;
            ticket.ResolutionDueAt = _slaService.CalculateDueDate(ticket.CreatedAt, slaPolicy.ResolutionMins, slaPolicy.BusinessHoursOnly);
        }

        _db.TicketAuditLogs.Add(new TicketAuditLog
        {
            TicketId = ticket.Id,
            ActorId = actorId,
            ActorName = actorName,
            Action = "PRIORITY_CHANGED",
            OldValue = JsonSerializer.Serialize(new { Priority = oldPrio }),
            NewValue = JsonSerializer.Serialize(new { Priority = priority }),
            IpAddress = ipAddress
        });

        await _db.SaveChangesAsync();
        await _hubContext.Clients.All.SendAsync("TicketUpdated", new { ticketId });
    }

    public async Task LinkParentTicketAsync(long childTicketId, long parentTicketId, int actorId, string actorName, string ipAddress)
    {
        if (childTicketId == parentTicketId)
            throw new InvalidOperationException("A ticket cannot be its own parent.");

        var child = await _db.Tickets.FindAsync(childTicketId) ?? throw new KeyNotFoundException("Child ticket not found.");
        var parent = await _db.Tickets.FindAsync(parentTicketId) ?? throw new KeyNotFoundException("Parent ticket not found.");

        long? oldParentId = child.ParentTicketId;
        child.ParentTicketId = parent.Id;
        child.UpdatedAt = DateTime.UtcNow;

        _db.TicketAuditLogs.Add(new TicketAuditLog
        {
            TicketId = child.Id,
            ActorId = actorId,
            ActorName = actorName,
            Action = "PARENT_TICKET_LINKED",
            OldValue = JsonSerializer.Serialize(new { ParentTicketId = oldParentId }),
            NewValue = JsonSerializer.Serialize(new { ParentTicketId = parent.Id, ParentCode = parent.TicketCode }),
            IpAddress = ipAddress
        });

        await _db.SaveChangesAsync();
        await _hubContext.Clients.All.SendAsync("TicketUpdated", new { ticketId = childTicketId });
    }

    public async Task TransferDepartmentAsync(long ticketId, int departmentId, int actorId, string actorName, string ipAddress)
    {
        var ticket = await _db.Tickets.Include(t => t.Department).FirstOrDefaultAsync(t => t.Id == ticketId)
                     ?? throw new KeyNotFoundException("Ticket not found.");

        if (ticket.DepartmentId == departmentId) return;

        var newDept = await _db.Departments.FindAsync(departmentId)
                      ?? throw new KeyNotFoundException("Department not found.");

        string oldDeptName = ticket.Department.Name;
        ticket.DepartmentId = departmentId;
        ticket.UpdatedAt = DateTime.UtcNow;

        _db.TicketAuditLogs.Add(new TicketAuditLog
        {
            TicketId = ticket.Id,
            ActorId = actorId,
            ActorName = actorName,
            Action = "DEPARTMENT_TRANSFERRED",
            OldValue = JsonSerializer.Serialize(new { Department = oldDeptName }),
            NewValue = JsonSerializer.Serialize(new { Department = newDept.Name }),
            IpAddress = ipAddress
        });

        await _db.SaveChangesAsync();
        await _hubContext.Clients.All.SendAsync("TicketUpdated", new { ticketId });
    }
}
