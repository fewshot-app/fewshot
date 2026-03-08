using System.Data;
using Apex.Core.Interfaces;
using Apex.Core.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Apex.Infrastructure.Memory;

/// <summary>
/// SQL Server 2025 vector-native memory service.
/// Uses VECTOR(768) column + VECTOR_DISTANCE() for cosine similarity search.
/// </summary>
public class MemoryService : IMemoryService
{
    private readonly string _connectionString;
    private readonly IEmbeddingService _embeddings;
    private readonly ILogger<MemoryService> _logger;

    private const int MinSummaryLength = 20;
    private const int MaxSummaryLength = 2000;
    private const double DuplicateThreshold = 0.10; // VECTOR_DISTANCE cosine: lower = more similar

    public MemoryService(
        IConfiguration config,
        IEmbeddingService embeddings,
        ILogger<MemoryService> logger)
    {
        _connectionString = config.GetConnectionString("ApexDb")
            ?? throw new InvalidOperationException("ApexDb connection string not configured");
        _embeddings = embeddings;
        _logger = logger;
    }

    public async Task<StoredMemory?> StoreAsync(MemoryStoreRequest request)
    {
        // Quality gate
        if (!ValidateRequest(request, out var rejection))
        {
            _logger.LogInformation("Memory rejected: {Reason}", rejection);
            return null;
        }

        // Duplicate check
        if (await IsDuplicateAsync(request.Summary))
        {
            _logger.LogInformation("Memory rejected: near-duplicate exists");
            return null;
        }

        var embedding = await _embeddings.EmbedAsync(BuildEmbeddingText(request));
        var pointId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // SQL Server 2025: cast JSON float array to VECTOR
        const string sql = """
            INSERT INTO Memories
                (PointId, SessionId, Project, Summary, Solution, Approach, OutcomeLabel, Tags, Language, Embedding, CreatedAt)
            VALUES
                (@PointId, @SessionId, @Project, @Summary, @Solution, @Approach, @OutcomeLabel, @Tags, @Language,
                 CAST(@Embedding AS VECTOR(768)), @CreatedAt)
            """;

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@PointId", pointId);
        cmd.Parameters.AddWithValue("@SessionId", request.SessionId);
        cmd.Parameters.AddWithValue("@Project", (object?)request.Project ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Summary", request.Summary);
        cmd.Parameters.AddWithValue("@Solution", (object?)request.Solution ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Approach", (object?)request.Approach ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@OutcomeLabel", (object?)request.OutcomeLabel ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Tags", (object?)request.Tags ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Language", (object?)request.Language ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Embedding", ToJsonArray(embedding));
        cmd.Parameters.AddWithValue("@CreatedAt", now);

        await cmd.ExecuteNonQueryAsync();

        _logger.LogInformation("Stored memory {PointId} for session {SessionId}: {Summary}",
            pointId, request.SessionId, Truncate(request.Summary, 80));

        return new StoredMemory
        {
            PointId = pointId,
            SessionId = request.SessionId,
            Summary = request.Summary,
            Solution = request.Solution,
            Approach = request.Approach,
            OutcomeLabel = request.OutcomeLabel,
            Tags = request.Tags,
            Language = request.Language,
            Project = request.Project,
            CreatedAt = now
        };
    }

    public async Task<List<SemanticMemory>> SearchAsync(string query, int sessionId, int limit = 5, double minScore = 0.55)
    {
        var queryVector = await _embeddings.EmbedAsync(query);

        // VECTOR_DISTANCE('cosine', ...) returns 0=identical, 1=opposite
        // Convert to similarity score: score = 1 - distance
        // minScore 0.55 → max distance 0.45
        var maxDistance = 1.0 - minScore;

        const string sql = """
            SELECT TOP (@Limit)
                PointId, SessionId, Project, Summary, Solution, Approach,
                OutcomeLabel, Tags, Language, CreatedAt,
                VECTOR_DISTANCE('cosine', Embedding, CAST(@QueryVector AS VECTOR(768))) AS Distance
            FROM Memories
            WHERE VECTOR_DISTANCE('cosine', Embedding, CAST(@QueryVector AS VECTOR(768))) <= @MaxDistance
            ORDER BY Distance ASC
            """;

        var memories = new List<SemanticMemory>();

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Limit", limit);
        cmd.Parameters.AddWithValue("@QueryVector", ToJsonArray(queryVector));
        cmd.Parameters.AddWithValue("@MaxDistance", maxDistance);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var distance = reader.GetDouble(reader.GetOrdinal("Distance"));
            memories.Add(new SemanticMemory
            {
                Summary = reader.GetString("Summary"),
                Solution = reader.IsDBNull("Solution") ? null : reader.GetString("Solution"),
                Approach = reader.IsDBNull("Approach") ? null : reader.GetString("Approach"),
                OutcomeLabel = reader.IsDBNull("OutcomeLabel") ? null : reader.GetString("OutcomeLabel"),
                Tags = reader.IsDBNull("Tags") ? null : reader.GetString("Tags"),
                RelevanceScore = 1.0 - distance,
                SessionId = reader.GetInt32("SessionId"),
                CreatedAt = reader.GetDateTime("CreatedAt")
            });
        }

        _logger.LogInformation("Search for '{Query}' returned {Count} memories", Truncate(query, 60), memories.Count);
        return memories;
    }

    public async Task<StoredMemory?> GetAsync(string pointId)
    {
        const string sql = """
            SELECT PointId, SessionId, Project, Summary, Solution, Approach,
                   OutcomeLabel, Tags, Language, CreatedAt
            FROM Memories WHERE PointId = @PointId
            """;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@PointId", pointId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return MapStoredMemory(reader);
    }

    public async Task<bool> DeleteAsync(string pointId)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand("DELETE FROM Memories WHERE PointId = @PointId", conn);
        cmd.Parameters.AddWithValue("@PointId", pointId);
        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<List<StoredMemory>> GetBySessionAsync(int sessionId)
    {
        const string sql = """
            SELECT PointId, SessionId, Project, Summary, Solution, Approach,
                   OutcomeLabel, Tags, Language, CreatedAt
            FROM Memories WHERE SessionId = @SessionId
            ORDER BY CreatedAt DESC
            """;

        var memories = new List<StoredMemory>();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@SessionId", sessionId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            memories.Add(MapStoredMemory(reader));

        return memories;
    }

    public async Task<bool> IsDuplicateAsync(string summary)
    {
        var vector = await _embeddings.EmbedAsync(summary);

        const string sql = """
            SELECT TOP 1 VECTOR_DISTANCE('cosine', Embedding, CAST(@Vector AS VECTOR(768))) AS Distance
            FROM Memories
            WHERE VECTOR_DISTANCE('cosine', Embedding, CAST(@Vector AS VECTOR(768))) <= @Threshold
            ORDER BY Distance ASC
            """;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Vector", ToJsonArray(vector));
        cmd.Parameters.AddWithValue("@Threshold", DuplicateThreshold);

        var result = await cmd.ExecuteScalarAsync();
        return result is not null and not DBNull;
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static StoredMemory MapStoredMemory(SqlDataReader r) => new()
    {
        PointId = r.GetString("PointId"),
        SessionId = r.GetInt32("SessionId"),
        Project = r.IsDBNull("Project") ? null : r.GetString("Project"),
        Summary = r.GetString("Summary"),
        Solution = r.IsDBNull("Solution") ? null : r.GetString("Solution"),
        Approach = r.IsDBNull("Approach") ? null : r.GetString("Approach"),
        OutcomeLabel = r.IsDBNull("OutcomeLabel") ? null : r.GetString("OutcomeLabel"),
        Tags = r.IsDBNull("Tags") ? null : r.GetString("Tags"),
        Language = r.IsDBNull("Language") ? null : r.GetString("Language"),
        CreatedAt = r.GetDateTime("CreatedAt")
    };

    private static bool ValidateRequest(MemoryStoreRequest r, out string reason)
    {
        if (string.IsNullOrWhiteSpace(r.Summary)) { reason = "Summary is empty"; return false; }
        if (r.Summary.Length < MinSummaryLength) { reason = $"Summary too short ({r.Summary.Length} chars)"; return false; }
        if (r.Summary.Length > MaxSummaryLength) { reason = $"Summary too long ({r.Summary.Length} chars)"; return false; }
        if (r.SessionId <= 0) { reason = "Invalid session ID"; return false; }
        reason = "";
        return true;
    }

    private static string BuildEmbeddingText(MemoryStoreRequest r)
    {
        var parts = new List<string> { r.Summary };
        if (!string.IsNullOrEmpty(r.Solution)) parts.Add(r.Solution);
        if (!string.IsNullOrEmpty(r.Approach)) parts.Add(r.Approach);
        if (!string.IsNullOrEmpty(r.Tags)) parts.Add(r.Tags);
        return string.Join(" ", parts);
    }

    /// <summary>
    /// SQL Server 2025 VECTOR type is cast from a JSON float array string: '[0.1, 0.2, ...]'
    /// </summary>
    private static string ToJsonArray(float[] v) =>
        "[" + string.Join(",", v.Select(f => f.ToString("G7", System.Globalization.CultureInfo.InvariantCulture))) + "]";

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";
}
