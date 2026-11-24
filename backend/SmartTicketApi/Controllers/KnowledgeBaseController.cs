using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartTicketApi.Data;
using SmartTicketApi.DTOs;
using SmartTicketApi.Entities;

namespace SmartTicketApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class KnowledgeBaseController : ControllerBase
{
    private readonly AppDbContext _db;

    public KnowledgeBaseController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("suggest")]
    public async Task<IActionResult> SuggestArticles([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 3)
            return Ok(Array.Empty<object>());

        var words = q.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var articles = await _db.KbArticles.Where(a => a.IsPublished).ToListAsync();

        var matches = articles
            .Where(a => words.Any(w => w.Length > 2 &&
                (a.Title.Contains(w, StringComparison.OrdinalIgnoreCase) ||
                 a.Tags.Contains(w, StringComparison.OrdinalIgnoreCase))))
            .Take(3)
            .Select(a => new { a.Id, a.Category, a.Title, a.Content, a.HelpfulCount });

        return Ok(matches);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? q)
    {
        var query = _db.KbArticles.Where(a => a.IsPublished).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(a => a.Title.Contains(q) || a.Tags.Contains(q) || a.Category.Contains(q));
        }
        return Ok(await query.OrderByDescending(a => a.HelpfulCount).ToListAsync());
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,DepartmentLead,SupportAgent")]
    public async Task<IActionResult> Create([FromBody] CreateKbArticleRequest req)
    {
        var article = new KbArticle
        {
            Category = req.Category,
            Title = req.Title,
            Tags = req.Tags,
            Content = req.Content,
            HelpfulCount = 1,
            IsPublished = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.KbArticles.Add(article);
        await _db.SaveChangesAsync();
        return Ok(article);
    }
}
