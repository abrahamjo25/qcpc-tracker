using Microsoft.EntityFrameworkCore;
using QCPCMvc.Data;
using QCPCMvc.Models;
using System.Text.Json;

namespace QCPCMvc.Services;

/// <summary>
/// Semantic vector search backed by OpenAI text-embedding-3-small.
///
/// Design:
///   • When an issue is created/updated its text is embedded and the float[]
///     stored as JSON in IssueEmbeddings (SQL Server, no special extension needed).
///   • At search time the query is embedded, all stored vectors loaded into
///     memory, and ranked by cosine similarity in O(n) — fine for hundreds of issues.
///   • If OpenAI is not configured (no API key) every call returns null and
///     IssueService falls back to SQL LIKE — zero user-visible impact.
///
/// Configuration (appsettings.json):
///   "OpenAI": { "ApiKey": "sk-...", "EmbeddingModel": "text-embedding-3-small" }
/// </summary>
public class VectorSearchService
{
    private readonly AppDbContext _db;
    private readonly ILogger<VectorSearchService> _log;
    private readonly IConfiguration _config;

    // Lazy HttpClient-based OpenAI embeddings caller (avoids heavy SK dependency at startup)
    private readonly IHttpClientFactory _http;

    public bool IsEnabled =>
        !string.IsNullOrWhiteSpace(_config["OpenAI:ApiKey"]);

    public VectorSearchService(
        AppDbContext db,
        ILogger<VectorSearchService> log,
        IConfiguration config,
        IHttpClientFactory http)
    { _db = db; _log = log; _config = config; _http = http; }

    // ── Index one issue ───────────────────────────────────────────────────────
    public async Task IndexIssueAsync(int issueId)
    {
        if (!IsEnabled) return;
        var issue = await _db.Issues.FindAsync(issueId);
        if (issue == null) return;

        var vec = await EmbedAsync(BuildText(issue));
        if (vec == null) return;

        var json = JsonSerializer.Serialize(vec);
        var existing = await _db.IssueEmbeddings.FindAsync(issueId);
        if (existing != null) { existing.VectorJson = json; existing.UpdatedAt = DateTime.UtcNow; }
        else _db.IssueEmbeddings.Add(new IssueEmbedding { IssueId=issueId, VectorJson=json });

        await _db.SaveChangesAsync();
    }

    // ── Re-index everything (admin utility) ───────────────────────────────────
    public async Task ReIndexAllAsync()
    {
        if (!IsEnabled) return;
        var ids = await _db.Issues.Select(i => i.Id).ToListAsync();
        foreach (var id in ids)
        {
            await IndexIssueAsync(id);
            await Task.Delay(60); // ~16 req/s – well within tier-1 limits
        }
        _log.LogInformation("VectorSearch: re-indexed {Count} issues", ids.Count);
    }

    // ── Search ────────────────────────────────────────────────────────────────
    /// <summary>Returns issue IDs ordered by cosine similarity, or null on failure/disabled.</summary>
    public async Task<List<int>?> SearchAsync(string query, int topK = 30)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(query)) return null;

        var queryVec = await EmbedAsync(query);
        if (queryVec == null) return null;

        var stored = await _db.IssueEmbeddings.ToListAsync();
        if (!stored.Any()) return null;

        var results = stored
            .Select(e => {
                float[]? v = null;
                try { v = JsonSerializer.Deserialize<float[]>(e.VectorJson); } catch { }
                return (e.IssueId, score: v != null ? Cosine(queryVec, v) : 0f);
            })
            .Where(x => x.score > 0.30f)        // relevance threshold
            .OrderByDescending(x => x.score)
            .Take(topK)
            .Select(x => x.IssueId)
            .ToList();

        _log.LogDebug("VectorSearch '{Query}' → {Count} hits", query, results.Count);
        return results.Any() ? results : null;  // null → caller uses SQL LIKE fallback
    }

    // ── OpenAI REST call (text-embedding-3-small, 1536-dim) ───────────────────
    private async Task<float[]?> EmbedAsync(string text)
    {
        try
        {
            var apiKey = _config["OpenAI:ApiKey"]!;
            var model  = _config["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small";

            using var client = _http.CreateClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var payload = JsonSerializer.Serialize(new { input = text, model });
            using var resp = await client.PostAsync(
                "https://api.openai.com/v1/embeddings",
                new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));

            resp.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var arr = doc.RootElement
                         .GetProperty("data")[0]
                         .GetProperty("embedding");

            return arr.EnumerateArray()
                      .Select(e => e.GetSingle())
                      .ToArray();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "OpenAI embedding call failed");
            return null;
        }
    }

    private static string BuildText(Issue i) =>
        string.Join(" | ", i.Title, i.Description,
            i.Process.ToString(), i.Priority.ToString(),
            i.ResponsibleTeam ?? "", i.Tags, i.CorrectiveAction ?? "");

    private static float Cosine(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0f;
        float dot=0, na=0, nb=0;
        for (int i=0; i<a.Length; i++) { dot+=a[i]*b[i]; na+=a[i]*a[i]; nb+=b[i]*b[i]; }
        var d = MathF.Sqrt(na) * MathF.Sqrt(nb);
        return d == 0 ? 0f : dot / d;
    }
}
