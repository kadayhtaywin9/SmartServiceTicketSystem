using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SmartTicketApi.Data;
using SmartTicketApi.DTOs;
using SmartTicketApi.Services;

namespace SmartTicketApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AuthController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest req)
    {
        var user = await _db.Users
            .Include(u => u.Role)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Email == req.Email && u.IsActive);

        if (user == null)
            return Unauthorized(new { message = "Invalid email or password." });

        bool validPassword = PasswordHelper.VerifyPassword(req.Password, user.PasswordHash);
        if (!validPassword)
            return Unauthorized(new { message = "Invalid email or password." });

        var permissions = user.Role.RolePermissions.Select(rp => rp.Permission.Slug).ToList();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.Name)
        };

        foreach (var perm in permissions)
            claims.Add(new Claim("permission", perm));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["JwtSettings:SecretKey"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["JwtSettings:Issuer"],
            audience: _config["JwtSettings:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_config.GetValue<int>("JwtSettings:ExpiryMinutes", 480)),
            signingCredentials: creds
        );

        return Ok(new AuthResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            user.Id,
            user.FullName,
            user.Email,
            user.Role.Name,
            permissions
        ));
    }
}
