using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartTicketApi.Data;
using SmartTicketApi.DTOs;
using SmartTicketApi.Entities;
using SmartTicketApi.Services;

namespace SmartTicketApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TicketService _ticketService;
    private readonly AiIntelligenceService _aiService;
    private readonly IWebHostEnvironment _env;

    public TicketsController(
        AppDbContext db,
        TicketService ticketService,
        AiIntelligenceService aiService,
        IWebHostEnvironment env)
    {
        _db = db;
        _ticketService = ticketService;
        _aiService = aiService;
        _env = env;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string CurrentUserName => User.FindFirstValue(ClaimTypes.Name) ?? "Unknown";
    private string CurrentUserRole => User.FindFirstValue(ClaimTypes.Role) ?? "EndUser";
    private string ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments()
    {
        var depts = await _db.Departments
            .OrderBy(d => d.Id)
            .Select(d => new { d.Id, d.Name })
            .ToListAsync();
        return Ok(depts);
    }

    [HttpGet("agents")]
    [Authorize(Roles = "SuperAdmin,DepartmentLead,SupportAgent")]
    public async Task<IActionResult> GetAssignableAgents()
    {
        var agents = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.Department)
            .Where(u => u.IsActive && u.Role.Name != "EndUser")
            .OrderBy(u => u.FullName)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                Role = u.Role.Name,
                Department = u.Department != null ? u.Department.Name : "Global"
            })
            .ToListAsync();

        return Ok(agents);
    }

    [HttpGet]
    public async Task<IActionResult> GetTickets(
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromQuery] int? departmentId,
        [FromQuery] string? q)
    {
        var query = _db.Tickets
            .Include(t => t.Requester)
            .Include(t => t.Assignee)
            .Include(t => t.Department)
            .Include(t => t.ParentTicket)
            .AsQueryable();

        if (CurrentUserRole == "EndUser")
            query = query.Where(t => t.RequesterId == CurrentUserId);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(t => t.Status == status);

        if (!string.IsNullOrWhiteSpace(priority))
            query = query.Where(t => t.Priority == priority);

        if (departmentId.HasValue)
            query = query.Where(t => t.DepartmentId == departmentId.Value);

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(t => t.Subject.Contains(q) || t.TicketCode.Contains(q));

        var tickets = await query
            .OrderByDescending(t => t.UpdatedAt)
            .Select(t => new
            {
                t.Id,
                t.TicketCode,
                t.ParentTicketId,
                ParentTicketCode = t.ParentTicket != null ? t.ParentTicket.TicketCode : null,
                t.Subject,
                t.Status,
                t.Priority,
                t.SentimentLabel,
                t.SentimentScore,
                t.SourceChannel,
                t.EscalationLevel,
                t.IsSlaBreached,
                t.FirstResponseDueAt,
                t.ResolutionDueAt,
                t.CreatedAt,
                t.UpdatedAt,
                RequesterName = t.Requester.FullName,
                AssigneeId = t.AssigneeId,
                AssigneeName = t.Assignee != null ? t.Assignee.FullName : "Unassigned",
                DepartmentId = t.DepartmentId,
                DepartmentName = t.Department.Name,
                ChildCount = t.ChildTickets.Count
            })
            .ToListAsync();

        return Ok(tickets);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetTicketDetail(long id)
    {
        var ticket = await _db.Tickets
            .Include(t => t.Requester).ThenInclude(u => u.Assets)
            .Include(t => t.Assignee)
            .Include(t => t.Department)
            .Include(t => t.ParentTicket)
            .Include(t => t.ChildTickets)
            .Include(t => t.Messages).ThenInclude(m => m.Sender)
            .Include(t => t.Attachments).ThenInclude(a => a.Uploader)
            .Include(t => t.CsatSurvey)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (ticket == null) return NotFound(new { message = "Ticket not found." });

        if (CurrentUserRole == "EndUser" && ticket.RequesterId != CurrentUserId)
            return Forbid();

        var visibleMessages = ticket.Messages
            .Where(m => CurrentUserRole != "EndUser" || !m.IsInternal)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new
            {
                m.Id,
                m.SenderId,
                SenderName = m.Sender.FullName,
                m.Body,
                m.IsInternal,
                m.ContainsMaskedPii,
                m.CreatedAt
            })
            .ToList();

        var attachments = ticket.Attachments
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                a.Id,
                a.FileName,
                a.ContentType,
                a.FileSize,
                UploaderName = a.Uploader.FullName,
                a.CreatedAt
            })
            .ToList();

        var aiInsights = CurrentUserRole != "EndUser"
            ? await _aiService.GenerateThreadInsightsAsync(ticket)
            : null;

        return Ok(new
        {
            ticket.Id,
            ticket.TicketCode,
            ticket.ParentTicketId,
            ParentTicket = ticket.ParentTicket == null ? null : new { ticket.ParentTicket.Id, ticket.ParentTicket.TicketCode, ticket.ParentTicket.Subject, ticket.ParentTicket.Status },
            ChildTickets = ticket.ChildTickets.Select(c => new { c.Id, c.TicketCode, c.Subject, c.Status, c.Priority }),
            ticket.Subject,
            ticket.Description,
            ticket.Status,
            ticket.Priority,
            ticket.SentimentLabel,
            ticket.SentimentScore,
            ticket.AiReasoning,
            ticket.SourceChannel,
            ticket.EscalationLevel,
            ticket.IsSlaBreached,
            ticket.FirstResponseDueAt,
            ticket.ResolutionDueAt,
            ticket.CreatedAt,
            Requester = new
            {
                ticket.Requester.Id,
                ticket.Requester.FullName,
                ticket.Requester.Email,
                Assets = ticket.Requester.Assets
            },
            Assignee = ticket.Assignee == null ? null : new { ticket.Assignee.Id, ticket.Assignee.FullName },
            Department = new { ticket.Department.Id, ticket.Department.Name },
            Messages = visibleMessages,
            Attachments = attachments,
            CsatSurvey = ticket.CsatSurvey,
            AiInsights = aiInsights
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateTicket([FromBody] CreateTicketRequest req)
    {
        var ticket = await _ticketService.CreateTicketAsync(req, CurrentUserId, CurrentUserName, ClientIp);
        return CreatedAtAction(nameof(GetTicketDetail), new { id = ticket.Id }, new
        {
            ticket.Id,
            ticket.TicketCode,
            ticket.Priority,
            ticket.SentimentLabel,
            ticket.AiReasoning
        });
    }

    [HttpPost("{id:long}/messages")]
    public async Task<IActionResult> AddMessage(long id, [FromBody] AddMessageRequest req)
    {
        try
        {
            var msg = await _ticketService.AddMessageAsync(id, req, CurrentUserId, CurrentUserName, CurrentUserRole, ClientIp);
            return Ok(new { msg.Id, msg.TicketId, msg.SenderId, SenderName = CurrentUserName, msg.Body, msg.IsInternal, msg.ContainsMaskedPii, msg.CreatedAt });
        }
        catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:long}/attachments")]
    [RequestSizeLimit(10_485_760)]
    public async Task<IActionResult> UploadAttachment(long id, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Please select a file to upload." });

        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null) return NotFound(new { message = "Ticket not found." });

        if (CurrentUserRole == "EndUser" && ticket.RequesterId != CurrentUserId)
            return Forbid();

        string uploadsRoot = Path.Combine(_env.ContentRootPath, "uploads");
        Directory.CreateDirectory(uploadsRoot);

        string safeName = Path.GetFileName(file.FileName);
        string storedName = $"{Guid.NewGuid():N}_{safeName}";
        string fullPath = Path.Combine(uploadsRoot, storedName);

        await using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var attachment = new TicketAttachment
        {
            TicketId = id,
            UploadedBy = CurrentUserId,
            FileName = safeName,
            StoredName = storedName,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            FileSize = file.Length,
            CreatedAt = DateTime.UtcNow
        };

        _db.TicketAttachments.Add(attachment);
        _db.TicketAuditLogs.Add(new TicketAuditLog
        {
            TicketId = id,
            ActorId = CurrentUserId,
            ActorName = CurrentUserName,
            Action = "ATTACHMENT_UPLOADED",
            NewValue = System.Text.Json.JsonSerializer.Serialize(new { File = safeName, SizeKB = Math.Max(1, file.Length / 1024) }),
            IpAddress = ClientIp
        });

        await _db.SaveChangesAsync();

        return Ok(new
        {
            attachment.Id,
            attachment.FileName,
            attachment.ContentType,
            attachment.FileSize,
            UploaderName = CurrentUserName,
            attachment.CreatedAt
        });
    }

    [HttpGet("attachments/{attachmentId:long}")]
    public async Task<IActionResult> DownloadAttachment(long attachmentId)
    {
        var att = await _db.TicketAttachments.Include(a => a.Ticket).FirstOrDefaultAsync(a => a.Id == attachmentId);
        if (att == null) return NotFound();

        if (CurrentUserRole == "EndUser" && att.Ticket.RequesterId != CurrentUserId)
            return Forbid();

        string fullPath = Path.Combine(_env.ContentRootPath, "uploads", att.StoredName);
        if (!System.IO.File.Exists(fullPath)) return NotFound(new { message = "File not found on server." });

        var bytes = await System.IO.File.ReadAllBytesAsync(fullPath);
        return File(bytes, att.ContentType, att.FileName);
    }

    [HttpPatch("{id:long}/status")]
    [Authorize(Roles = "SuperAdmin,DepartmentLead,SupportAgent")]
    public async Task<IActionResult> UpdateStatus(long id, [FromBody] UpdateTicketStatusRequest req)
    {
        await _ticketService.UpdateStatusWithCascadeAsync(id, req, CurrentUserId, CurrentUserName, ClientIp);
        return Ok(new { message = "Status updated." });
    }

    [HttpPatch("{id:long}/assign")]
    [Authorize(Roles = "SuperAdmin,DepartmentLead,SupportAgent")]
    public async Task<IActionResult> AssignTicket(long id, [FromBody] AssignTicketRequest req)
    {
        await _ticketService.AssignTicketAsync(id, req.AssigneeId, CurrentUserId, CurrentUserName, ClientIp);
        return Ok(new { message = "Assignee updated." });
    }

    [HttpPatch("{id:long}/priority")]
    [Authorize(Roles = "SuperAdmin,DepartmentLead,SupportAgent")]
    public async Task<IActionResult> UpdatePriority(long id, [FromBody] UpdatePriorityRequest req)
    {
        await _ticketService.UpdatePriorityAsync(id, req.Priority, CurrentUserId, CurrentUserName, ClientIp);
        return Ok(new { message = "Priority updated." });
    }

    [HttpPatch("{id:long}/department")]
    [Authorize(Roles = "SuperAdmin,DepartmentLead,SupportAgent")]
    public async Task<IActionResult> TransferDepartment(long id, [FromBody] TransferDepartmentRequest req)
    {
        await _ticketService.TransferDepartmentAsync(id, req.DepartmentId, CurrentUserId, CurrentUserName, ClientIp);
        return Ok(new { message = "Department transferred." });
    }

    [HttpPost("{id:long}/link-parent")]
    [Authorize(Roles = "SuperAdmin,DepartmentLead,SupportAgent")]
    public async Task<IActionResult> LinkParent(long id, [FromBody] LinkParentTicketRequest req)
    {
        try
        {
            await _ticketService.LinkParentTicketAsync(id, req.ParentTicketId, CurrentUserId, CurrentUserName, ClientIp);
            return Ok(new { message = "Linked to parent ticket." });
        }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{id:long}/csat")]
    public async Task<IActionResult> SubmitCsat(long id, [FromBody] SubmitCsatRequest req)
    {
        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null) return NotFound(new { message = "Ticket not found." });

        var existing = await _db.CsatSurveys.FirstOrDefaultAsync(c => c.TicketId == id);
        if (existing != null) return BadRequest(new { message = "Feedback already submitted." });

        var survey = new CsatSurvey
        {
            TicketId = id,
            Rating = req.Rating,
            Feedback = req.Feedback,
            SubmittedAt = DateTime.UtcNow
        };

        _db.CsatSurveys.Add(survey);
        await _db.SaveChangesAsync();
        return Ok(survey);
    }

    [HttpGet("export-csv")]
    [Authorize(Roles = "SuperAdmin,DepartmentLead")]
    public async Task<IActionResult> ExportCsv()
    {
        var rows = await _db.Tickets
            .Include(t => t.Department)
            .Include(t => t.Requester)
            .Include(t => t.Assignee)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("TicketCode,Subject,Department,Priority,Status,Requester,Assignee,SLABreached,CreatedAtUTC");

        foreach (var t in rows)
        {
            string subject = t.Subject.Replace("\"", "\"\"");
            string dept = t.Department.Name.Replace("\"", "\"\"");
            string req = t.Requester.FullName.Replace("\"", "\"\"");
            string asg = (t.Assignee?.FullName ?? "Unassigned").Replace("\"", "\"\"");
            sb.AppendLine($"\"{t.TicketCode}\",\"{subject}\",\"{dept}\",\"{t.Priority}\",\"{t.Status}\",\"{req}\",\"{asg}\",{t.IsSlaBreached},\"{t.CreatedAt:yyyy-MM-dd HH:mm}\"");
        }

        byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"stms-tickets-{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}
