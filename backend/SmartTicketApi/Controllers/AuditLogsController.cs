using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartTicketApi.Data;

namespace SmartTicketApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin,DepartmentLead")]
public class AuditLogsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AuditLogsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAuditLogs([FromQuery] long? ticketId, [FromQuery] string? action)
    {
        var query = _db.TicketAuditLogs.AsQueryable();

        if (ticketId.HasValue)
            query = query.Where(a => a.TicketId == ticketId.Value);

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(a => a.Action.Contains(action));

        var logs = await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(200)
            .ToListAsync();

        return Ok(logs);
    }
}
