using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartTicketApi.Data;
using SmartTicketApi.DTOs;
using SmartTicketApi.Services;

namespace SmartTicketApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebhooksController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TicketService _ticketService;
    private readonly IConfiguration _config;

    public WebhooksController(AppDbContext db, TicketService ticketService, IConfiguration config)
    {
        _db = db;
        _ticketService = ticketService;
        _config = config;
    }

    [HttpPost("tickets")]
    public async Task<IActionResult> IngestExternalTicket(
        [FromHeader(Name = "X-Webhook-Key")] string? apiKey,
        [FromBody] WebhookTicketRequest req)
    {
        string expectedKey = _config["WebhookSettings:ApiKey"] ?? "stms-webhook-secret-key-2026";
        if (apiKey != expectedKey)
            return Unauthorized(new { message = "Invalid X-Webhook-Key header." });

        var requester = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.RequesterEmail)
                        ?? await _db.Users.FirstAsync();

        var createReq = new CreateTicketRequest(
            Subject: $"[{req.SourceSystem}] {req.Subject}",
            Description: req.Description,
            Priority: req.Severity ?? "High",
            DepartmentId: null,
            SourceChannel: "MonitoringWebhook"
        );

        var ticket = await _ticketService.CreateTicketAsync(
            createReq,
            requester.Id,
            $"Webhook:{req.SourceSystem}",
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "webhook"
        );

        return Ok(new { ticket.Id, ticket.TicketCode, ticket.Priority, ticket.AssigneeId });
    }
}
