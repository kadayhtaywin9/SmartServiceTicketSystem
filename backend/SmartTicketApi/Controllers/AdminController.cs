using System.Security.Claims;
using System.Text.Json;
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
[Authorize(Roles = "SuperAdmin,DepartmentLead")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminController(AppDbContext db)
    {
        _db = db;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string CurrentUserName => User.FindFirstValue(ClaimTypes.Name) ?? "Admin";
    private string ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview()
    {
        var tickets = await _db.Tickets.Include(t => t.Department).ToListAsync();
        int total = tickets.Count;
        int active = tickets.Count(t => t.Status == "Open" || t.Status == "In_Progress" || t.Status == "Pending_Customer");
        int resolved = tickets.Count(t => t.Status == "Resolved" || t.Status == "Closed");
        int breached = tickets.Count(t => t.IsSlaBreached);
        double slaRate = total == 0 ? 100.0 : Math.Round(((double)(total - breached) / total) * 100.0, 1);

        var departments = await _db.Departments.OrderBy(d => d.Id).ToListAsync();
        var deptStats = departments.Select(d => new
        {
            d.Id,
            d.Name,
            Total = tickets.Count(t => t.DepartmentId == d.Id),
            Open = tickets.Count(t => t.DepartmentId == d.Id && t.Status != "Resolved" && t.Status != "Closed"),
            Resolved = tickets.Count(t => t.DepartmentId == d.Id && (t.Status == "Resolved" || t.Status == "Closed"))
        });

        return Ok(new
        {
            total,
            active,
            resolved,
            breached,
            slaComplianceRate = slaRate,
            departments = deptStats
        });
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles()
    {
        var roles = await _db.Roles.OrderBy(r => r.Id).Select(r => new { r.Id, r.Name }).ToListAsync();
        return Ok(roles);
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.Department)
            .OrderBy(u => u.RoleId)
            .ThenBy(u => u.FullName)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.Email,
                RoleName = u.Role.Name,
                DepartmentName = u.Department != null ? u.Department.Name : "All / Employee",
                u.Skills,
                u.IsActive,
                OpenAssigned = _db.Tickets.Count(t => t.AssigneeId == u.Id && t.Status != "Resolved" && t.Status != "Closed")
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpPost("users")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.FullName) || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { message = "Full name, email, and password are required." });

        bool exists = await _db.Users.AnyAsync(u => u.Email == req.Email.Trim());
        if (exists)
            return BadRequest(new { message = "An account with this email already exists." });

        var user = new User
        {
            FullName = req.FullName.Trim(),
            Email = req.Email.Trim().ToLowerInvariant(),
            PasswordHash = PasswordHelper.HashPassword(req.Password),
            RoleId = req.RoleId,
            DepartmentId = req.DepartmentId,
            Skills = string.IsNullOrWhiteSpace(req.Skills) ? null : req.Skills.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        _db.TicketAuditLogs.Add(new TicketAuditLog
        {
            ActorId = CurrentUserId,
            ActorName = CurrentUserName,
            Action = "USER_ONBOARDED",
            NewValue = JsonSerializer.Serialize(new { user.FullName, user.Email, user.RoleId }),
            IpAddress = ClientIp
        });

        await _db.SaveChangesAsync();
        return Ok(new { user.Id, user.FullName, user.Email });
    }

    [HttpPatch("users/{id:int}/toggle-active")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        if (id == CurrentUserId)
            return BadRequest(new { message = "You cannot deactivate your own admin account." });

        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        bool oldState = user.IsActive;
        user.IsActive = !user.IsActive;

        _db.TicketAuditLogs.Add(new TicketAuditLog
        {
            ActorId = CurrentUserId,
            ActorName = CurrentUserName,
            Action = user.IsActive ? "USER_ACTIVATED" : "USER_DEACTIVATED",
            OldValue = JsonSerializer.Serialize(new { Active = oldState, User = user.FullName }),
            NewValue = JsonSerializer.Serialize(new { Active = user.IsActive, User = user.FullName }),
            IpAddress = ClientIp
        });

        await _db.SaveChangesAsync();
        return Ok(new { user.Id, user.IsActive });
    }
}
