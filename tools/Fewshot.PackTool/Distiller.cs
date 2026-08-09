using System.Net.Http.Json;
using System.Text.Json;
using Fewshot.Core.Models;

namespace Fewshot.PackTool;

public static class Distiller
{
    private static readonly JsonSerializerOptions Camel = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static async Task<int> RunAsync(
        string inputPath, string outputPath, string ollamaUrl,
        string model, string embedModel, double threshold, int maxClusterChars)
    {
        var pack = JsonSerializer.Deserialize<FewshotPack>(File.ReadAllText(inputPath), Camel)
            ?? throw new InvalidOperationException("Could not parse pack JSON");

        var drafts = pack.Memories;
        Console.WriteLine($"Distilling {drafts.Count} memories from {pack.PackId} via {model} @ {ollamaUrl}");

        using var http = new HttpClient { BaseAddress = new Uri(ollamaUrl), Timeout = TimeSpan.FromMinutes(10) };

        Console.WriteLine("Embedding...");
        var vectors = new List<float[]>(drafts.Count);
        const int embedBatch = 32;
        for (var i = 0; i < drafts.Count; i += embedBatch)
        {
            var batch = drafts.Skip(i).Take(embedBatch).Select(m => $"{m.Summary}\n{m.Solution}").ToArray();
            vectors.AddRange(await EmbedAsync(http, embedModel, batch));
            Console.Write($"\r  {Math.Min(i + embedBatch, drafts.Count)}/{drafts.Count}");
        }
        Console.WriteLine();

        Console.WriteLine("Clustering...");
        var clusters = Cluster(drafts, vectors, threshold, maxClusterChars);
        var multi = clusters.Where(c => c.Count > 1).ToList();
        var singles = clusters.Where(c => c.Count == 1).Select(c => drafts[c[0]]).ToList();
        Console.WriteLine($"  {clusters.Count} clusters: {multi.Count} multi-member covering {multi.Sum(c => c.Count)} drafts, {singles.Count} singletons pass through");

        var outMemories = new List<PackMemory>();
        var outPreferences = new List<PackPreference>(pack.Preferences);
        var outAntiPatterns = new List<PackAntiPattern>(pack.AntiPatterns);
        var failed = 0;

        for (var ci = 0; ci < multi.Count; ci++)
        {
            var members = multi[ci].Select(idx => drafts[idx]).ToList();
            var result = await DistillClusterAsync(http, model, members);
            if (result is null)
            {
                failed++;
                outMemories.AddRange(members);
                continue;
            }

            var provenance = string.Join("; ", members.Select(m => m.Summary).Distinct().Take(8));
            foreach (var m in result.Memories)
            {
                m.OutcomeLabel = "distilled-unreviewed";
                m.Approach = $"sources: {provenance}";
                m.Tags = members[0].Tags;
                outMemories.Add(m);
            }
            foreach (var p in result.Preferences)
            {
                p.ConfidenceScore = p.ConfidenceScore is > 0 and <= 1 ? Math.Min(p.ConfidenceScore, 0.8) : 0.6;
                outPreferences.Add(p);
            }
            outAntiPatterns.AddRange(result.AntiPatterns);

            Console.Write($"\r  cluster {ci + 1}/{multi.Count} ({members.Count} drafts -> {result.Memories.Count}m/{result.Preferences.Count}p/{result.AntiPatterns.Count}a)      ");
        }
        Console.WriteLine();

        outMemories.AddRange(singles);

        pack.Memories = outMemories;
        pack.Preferences = outPreferences;
        pack.AntiPatterns = outAntiPatterns;
        pack.Version = "0.2.0-distilled";

        File.WriteAllText(outputPath, JsonSerializer.Serialize(pack, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        Console.WriteLine($"{drafts.Count} drafts -> {outMemories.Count} memories ({singles.Count} untouched singletons), {outPreferences.Count} preferences, {outAntiPatterns.Count} anti-patterns");
        if (failed > 0) Console.WriteLine($"{failed} clusters failed to parse and passed through unchanged");
        Console.WriteLine($"Wrote {outputPath} — everything is unreviewed; review before shipping.");
        return 0;
    }

    private static async Task<List<float[]>> EmbedAsync(HttpClient http, string model, string[] inputs)
    {
        var resp = await http.PostAsJsonAsync("/api/embed", new { model, input = inputs });
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("embeddings").EnumerateArray()
            .Select(e => e.EnumerateArray().Select(v => v.GetSingle()).ToArray())
            .ToList();
    }

    private static List<List<int>> Cluster(List<PackMemory> drafts, List<float[]> vectors, double threshold, int maxChars)
    {
        var clusters = new List<List<int>>();
        var centroids = new List<float[]>();
        var sizes = new List<int>();

        for (var i = 0; i < drafts.Count; i++)
        {
            var len = (drafts[i].Solution ?? "").Length;
            var best = -1;
            var bestSim = threshold;
            for (var c = 0; c < clusters.Count; c++)
            {
                if (sizes[c] + len > maxChars) continue;
                var sim = Cosine(vectors[i], centroids[c]);
                if (sim > bestSim) { bestSim = sim; best = c; }
            }

            if (best >= 0)
            {
                clusters[best].Add(i);
                sizes[best] += len;
                var n = clusters[best].Count;
                for (var d = 0; d < centroids[best].Length; d++)
                    centroids[best][d] = (centroids[best][d] * (n - 1) + vectors[i][d]) / n;
            }
            else
            {
                clusters.Add([i]);
                centroids.Add((float[])vectors[i].Clone());
                sizes.Add(len);
            }
        }
        return clusters;
    }

    private static float Cosine(float[] a, float[] b)
    {
        float dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++) { dot += a[i] * b[i]; na += a[i] * a[i]; nb += b[i] * b[i]; }
        return dot / (MathF.Sqrt(na) * MathF.Sqrt(nb) + 1e-8f);
    }

    private sealed class DistillResult
    {
        public List<PackMemory> Memories { get; set; } = [];
        public List<PackPreference> Preferences { get; set; } = [];
        public List<PackAntiPattern> AntiPatterns { get; set; } = [];
    }

    private static async Task<DistillResult?> DistillClusterAsync(HttpClient http, string model, List<PackMemory> members)
    {
        var chunks = string.Join("\n\n---CHUNK---\n\n", members.Select((m, i) => $"[{i + 1}] {m.Summary}\n{m.Solution}"));
        var prompt = $$"""
        You are distilling related documentation chunks into a knowledge pack that will be injected into an AI coding agent's memory.

        Rules:
        - Merge duplicate and overlapping information. Preserve EVERY distinct technical fact.
        - Keep code blocks, JSON schemas, CSS selectors, hook names, and exact identifiers VERBATIM.
        - Rewrite all prose concisely in your own words. Drop navigation text, marketing language, and links.
        - Each memory must be self-contained. summary is a topic statement under 120 characters; solution holds the facts.
        - Guidance shaped "always/prefer/use X" belongs in preferences (category: lowercase snake_case topic, key: short identifier, value: the guidance).
        - Failure modes, gotchas, and "don't do X" belong in antiPatterns (pattern: what people do wrong, reason: why it fails and what to do instead).
        - Output the FEWEST items that preserve all facts.

        Respond with ONLY this JSON shape:
        {"memories":[{"summary":"","solution":"","language":null}],"preferences":[{"category":"","key":"","value":""}],"antiPatterns":[{"pattern":"","reason":"","language":null}]}

        Chunks:
        {{chunks}}
        """;

        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                var resp = await http.PostAsJsonAsync("/api/generate", new
                {
                    model,
                    prompt,
                    stream = false,
                    format = "json",
                    options = new { temperature = 0.2, num_ctx = 16384 }
                });
                resp.EnsureSuccessStatusCode();
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                var raw = doc.RootElement.GetProperty("response").GetString() ?? "";
                var parsed = JsonSerializer.Deserialize<DistillResult>(raw, Camel);
                if (parsed is not null && parsed.Memories.Count > 0) return parsed;
            }
            catch when (attempt == 0) { }
            catch { return null; }
        }
        return null;
    }
}
