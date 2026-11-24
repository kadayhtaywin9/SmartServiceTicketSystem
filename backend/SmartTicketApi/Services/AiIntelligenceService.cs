using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartTicketApi.Data;
using SmartTicketApi.Entities;

namespace SmartTicketApi.Services;

public record AiTriageResult(
    string SentimentLabel,
    decimal SentimentScore,
    string RecommendedPriority,
    int DepartmentId,
    int? AssigneeId,
    string ReasoningTrace,
    string EngineMode
);

public class AiIntelligenceService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AiIntelligenceService> _logger;

    public AiIntelligenceService(
        AppDbContext db,
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        ILogger<AiIntelligenceService> logger)
    {
        _db = db;
        _config = config;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<AiTriageResult> AnalyzeAndRouteTicketAsync(
        string subject,
        string description,
        string? requestedPriority,
        int? preferredDeptId)
    {
        var departments = await _db.Departments.ToListAsync();
        string? apiKey = _config["AiSettings:ApiKey"];
        string? endpoint = _config["AiSettings:Endpoint"];
        string model = _config["AiSettings:Model"] ?? "gpt-4o-mini";

        string sentimentLabel = "Calm";
        decimal sentimentScore = 0.15m;
        string finalPriority = string.IsNullOrWhiteSpace(requestedPriority) ? "Medium" : requestedPriority;
        int targetDeptId = preferredDeptId ?? (departments.FirstOrDefault()?.Id ?? 1);
        var reasoningSteps = new List<string>();
        string engineMode = "Built-in Reasoning Engine";

        if (!string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(endpoint))
        {
            try
            {
                var llmDecision = await CallLlmTriageAsync(endpoint, apiKey, model, subject, description, departments);
                if (llmDecision != null)
                {
                    sentimentLabel = llmDecision.Value.Sentiment;
                    sentimentScore = llmDecision.Value.Score;
                    if (string.IsNullOrWhiteSpace(requestedPriority))
                        finalPriority = llmDecision.Value.Priority;
                    if (!preferredDeptId.HasValue && llmDecision.Value.DepartmentId > 0)
                        targetDeptId = llmDecision.Value.DepartmentId;

                    reasoningSteps.Add($"[LLM {model}] {llmDecision.Value.Thinking}");
                    engineMode = $"Live LLM ({model})";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Live LLM triage unavailable, using built-in reasoning engine.");
            }
        }

        if (reasoningSteps.Count == 0)
        {
            string combined = $"{subject} {description}".ToLowerInvariant();
            string[] angryWords = { "angry", "furious", "frustrated", "unacceptable", "outage", "down", "broken", "immediately", "asap", "crash" };
            string[] urgentWords = { "slow", "issue", "again", "waiting", "delay", "problem", "error", "disconnecting", "urgently" };

            int angryHits = angryWords.Count(w => combined.Contains(w));
            int urgentHits = urgentWords.Count(w => combined.Contains(w));

            if (angryHits >= 2 || combined.Contains("outage") || combined.Contains("down"))
            {
                sentimentLabel = "Angry";
                sentimentScore = Math.Min(0.98m, 0.76m + (angryHits * 0.05m));
                finalPriority = (requestedPriority == "Critical" || combined.Contains("outage")) ? "Critical" : "High";
                reasoningSteps.Add($"Step 1 (Sentiment): Detected critical/outage distress signals -> Sentiment '{sentimentLabel}' ({sentimentScore:P0}), Priority elevated to {finalPriority}.");
            }
            else if (angryHits == 1 || urgentHits >= 2)
            {
                sentimentLabel = "Frustrated";
                sentimentScore = 0.62m;
                if (string.IsNullOrWhiteSpace(requestedPriority) || requestedPriority == "Low")
                    finalPriority = "High";
                reasoningSteps.Add($"Step 1 (Sentiment): Detected urgency markers -> Sentiment '{sentimentLabel}' ({sentimentScore:P0}), Priority set to {finalPriority}.");
            }
            else
            {
                reasoningSteps.Add($"Step 1 (Sentiment): Neutral operational tone -> Sentiment 'Calm' ({sentimentScore:P0}), Priority '{finalPriority}'.");
            }

            if (!preferredDeptId.HasValue)
            {
                foreach (var dept in departments)
                {
                    if (!string.IsNullOrWhiteSpace(dept.Keywords))
                    {
                        var keys = dept.Keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        var matched = keys.Where(k => combined.Contains(k.ToLowerInvariant())).ToList();
                        if (matched.Any())
                        {
                            targetDeptId = dept.Id;
                            reasoningSteps.Add($"Step 2 (Routing): Matched keywords [{string.Join(", ", matched)}] -> Routed to '{dept.Name}'.");
                            break;
                        }
                    }
                }
            }
        }

        string lowerText = $"{subject} {description}".ToLowerInvariant();
        var candidates = await _db.Users
            .Include(u => u.Role)
            .Where(u => u.IsActive
                        && u.DepartmentId == targetDeptId
                        && (u.Role.Name == "SupportAgent" || u.Role.Name == "DepartmentLead"))
            .Select(u => new
            {
                Agent = u,
                OpenCount = _db.Tickets.Count(t => t.AssigneeId == u.Id && (t.Status == "Open" || t.Status == "In_Progress"))
            })
            .ToListAsync();

        var chosen = candidates
            .OrderByDescending(c => CountSkillMatches(c.Agent.Skills, lowerText))
            .ThenBy(c => c.OpenCount)
            .FirstOrDefault();

        if (chosen != null)
        {
            int skillMatches = CountSkillMatches(chosen.Agent.Skills, lowerText);
            reasoningSteps.Add($"Step 3 (Assignment): Assigned to '{chosen.Agent.FullName}' (Skill matches: {skillMatches}, Active queue: {chosen.OpenCount}).");
        }

        return new AiTriageResult(
            sentimentLabel,
            sentimentScore,
            finalPriority,
            targetDeptId,
            chosen?.Agent.Id,
            string.Join("\n", reasoningSteps),
            engineMode
        );
    }

    private static int CountSkillMatches(string? skillsCsv, string lowerText)
    {
        if (string.IsNullOrWhiteSpace(skillsCsv)) return 0;
        return skillsCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Count(s => lowerText.Contains(s.ToLowerInvariant()));
    }

    private async Task<(string Sentiment, decimal Score, string Priority, int DepartmentId, string Thinking)?> CallLlmTriageAsync(
        string endpoint, string apiKey, string model, string subject, string description, List<Department> departments)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(12);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        string deptList = string.Join("; ", departments.Select(d => $"{d.Id}={d.Name}"));
        string systemPrompt =
            "You are an enterprise IT Service Desk AI triage engine. Return ONLY valid JSON: " +
            "{\"sentiment\":\"Calm|Frustrated|Angry\",\"score\":0.0-1.0,\"priority\":\"Low|Medium|High|Critical\",\"departmentId\":number,\"thinking\":\"step-by-step reasoning\"}. " +
            $"Departments: {deptList}.";

        var payload = new
        {
            model,
            temperature = 0.2,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = $"Subject: {subject}\nDescription: {description}" }
            }
        };

        var response = await client.PostAsync(endpoint, new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
        if (!response.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        string content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        content = content.Replace("```json", "").Replace("```", "").Trim();
        using var parsed = JsonDocument.Parse(content);
        var root = parsed.RootElement;

        return (
            root.GetProperty("sentiment").GetString() ?? "Calm",
            root.GetProperty("score").GetDecimal(),
            root.GetProperty("priority").GetString() ?? "Medium",
            root.GetProperty("departmentId").GetInt32(),
            root.GetProperty("thinking").GetString() ?? "Analyzed via LLM."
        );
    }

    public async Task<object> GenerateThreadInsightsAsync(Ticket ticket)
    {
        var messages = (ticket.Messages ?? Enumerable.Empty<TicketMessage>())
            .OrderBy(m => m.CreatedAt)
            .ToList();

        var articles = await _db.KbArticles.Where(k => k.IsPublished).ToListAsync();
        var bestKb = articles.FirstOrDefault(k =>
            k.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                  .Any(tag => ticket.Subject.Contains(tag, StringComparison.OrdinalIgnoreCase)
                           || ticket.Description.Contains(tag, StringComparison.OrdinalIgnoreCase)));

        string? apiKey = _config["AiSettings:ApiKey"];
        string? endpoint = _config["AiSettings:Endpoint"];
        string model = _config["AiSettings:Model"] ?? "gpt-4o-mini";

        if (!string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(endpoint))
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(12);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                string transcript = string.Join("\n", messages.Select(m => $"[{(m.IsInternal ? "INTERNAL" : "PUBLIC")}] {m.Sender?.FullName}: {m.Body}"));
                string kbContext = bestKb != null ? $"Matched KB '{bestKb.Title}': {bestKb.Content}" : "No direct KB match.";

                var payload = new
                {
                    model,
                    temperature = 0.3,
                    messages = new[]
                    {
                        new { role = "system", content = "Return ONLY JSON: {\"thinking\":\"step-by-step analysis\",\"summary\":\"2-sentence summary\",\"suggestedReply\":\"helpful response\"}." },
                        new { role = "user", content = $"Ticket {ticket.TicketCode}: {ticket.Subject}\n{kbContext}\nTranscript:\n{transcript}" }
                    }
                };

                var res = await client.PostAsync(endpoint, new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
                if (res.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
                    string raw = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
                    raw = raw.Replace("```json", "").Replace("```", "").Trim();
                    using var parsed = JsonDocument.Parse(raw);

                    return new
                    {
                        thinking = parsed.RootElement.GetProperty("thinking").GetString(),
                        summary = parsed.RootElement.GetProperty("summary").GetString(),
                        suggestedReply = parsed.RootElement.GetProperty("suggestedReply").GetString(),
                        matchedKbTitle = bestKb?.Title,
                        engineMode = $"Live LLM ({model})"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Live LLM reply synthesis fallback triggered.");
            }
        }

        string customerName = ticket.Requester?.FullName ?? "Customer";
        string thinkingTrace =
            !string.IsNullOrWhiteSpace(ticket.AiReasoning)
                ? ticket.AiReasoning
                : $"Evaluated {messages.Count} messages. Sentiment: {ticket.SentimentLabel} ({ticket.SentimentScore:P0}). " +
                  (bestKb != null ? $"Matched SOP '{bestKb.Title}'." : "Recommend diagnostic engineering update.");

        string summary =
            $"{ticket.TicketCode} ({ticket.Priority} · {ticket.Status}) — {messages.Count(m => !m.IsInternal)} public replies, {messages.Count(m => m.IsInternal)} internal notes.";

        string suggestedReply = bestKb != null
            ? $"Hi {customerName},\n\nBased on our troubleshooting guide ({bestKb.Title}), please try these steps:\n\n{bestKb.Content}\n\nLet us know if the issue persists."
            : $"Hi {customerName},\n\nThank you for your patience. Our {ticket.Department?.Name ?? "Support"} team is investigating \"{ticket.Subject}\" and will update you shortly.";

        return new
        {
            thinking = thinkingTrace,
            summary,
            suggestedReply,
            matchedKbTitle = bestKb?.Title,
            engineMode = "Built-in Reasoning Engine"
        };
    }
}
