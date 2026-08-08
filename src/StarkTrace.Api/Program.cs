using System.Text.RegularExpressions;
using StarkTrace.Api.Hubs;
using StarkTrace.Api.Jobs;
using StarkTrace.Core.Interfaces;
using StarkTrace.Core.Models;
using StarkTrace.Infrastructure.Audit;
using StarkTrace.Infrastructure.Context;
using StarkTrace.Infrastructure.Data;
using StarkTrace.Infrastructure.Experiments;
using StarkTrace.Infrastructure.Memory;
using StarkTrace.Infrastructure.Packs;
using StarkTrace.Infrastructure.Services;
using StarkTrace.Api.Services;
using Hangfire;
using Microsoft.EntityFrameworkCore;

// ── Pin working directory so Windows Service resolves paths correctly ──
Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService();

// Serve Blazor Dashboard static web assets from build output even in Production
// (auto-enabled only in Development; no-op for published layouts with a real wwwroot)
builder.WebHost.UseStaticWebAssets();

// ── SQLite connection string (expands %PROGRAMDATA% et al.) ──────
var rawConn = builder.Configuration.GetConnectionString("StarkTraceDb")
    ?? "Data Source=%PROGRAMDATA%\\APEX\\apex.db";
var sqliteConn = rawConn
    .Replace("%APPDATA%", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData))
    .Replace("%LOCALAPPDATA%", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
    .Replace("%PROGRAMDATA%", Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));

// Ensure data directory exists
var dbPath = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(sqliteConn).DataSource;
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

// ── Data ─────────────────────────────────────────────────────────
builder.Services.AddDbContext<StarkTraceDbContext>(options =>
    options.UseSqlite(sqliteConn));

// ── Core services ─────────────────────────────────────────────────
builder.Services.AddTransient<ISessionService, SessionService>();
builder.Services.AddTransient<IMessageService, MessageService>();
builder.Services.AddTransient<ISuggestionService, SuggestionService>();
builder.Services.AddTransient<IOutcomeService, OutcomeService>();
builder.Services.AddTransient<IPreferenceService, PreferenceService>();
builder.Services.AddTransient<IAntiPatternService, AntiPatternService>();
builder.Services.AddTransient<IAuditService, AuditService>();
builder.Services.AddSingleton<IAuditAllowlistProvider, AuditAllowlistProvider>();

// ── Context injection ─────────────────────────────────────────────
builder.Services.AddSingleton<AclFormatter>();
builder.Services.AddSingleton<ProseFormatter>();
builder.Services.AddSingleton<ITokenCounter, ApproximateTokenCounter>();
builder.Services.AddTransient<IContextInjector, ContextInjector>();

// ── Experiments ───────────────────────────────────────────────────
builder.Services.AddTransient<IExperimentService, ExperimentService>();

// ── Presidio process manager ──────────────────────────────────────
builder.Services.AddSingleton<PresidioProcessManager>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<PresidioProcessManager>());

// ── In-process cache (no Redis) ───────────────────────────────────
builder.Services.AddMemoryCache();
builder.Services.AddTransient<IProjectSessionService, ProjectSessionService>();

// ── Ollama HTTP client ────────────────────────────────────────────
var ollamaUrl = builder.Configuration["StarkTrace:Ollama:BaseUrl"] ?? "http://127.0.0.1:11434";
builder.Services.AddHttpClient("Ollama", client =>
{
    client.BaseAddress = new Uri(ollamaUrl);
    client.Timeout = TimeSpan.FromSeconds(300);
});

// ── Memory + Embeddings + LLM ─────────────────────────────────────
builder.Services.AddTransient<IEmbeddingService, EmbeddingService>();
builder.Services.AddTransient<IMemoryService, MemoryService>();

// ── Packs ────────────────────────────────────────────────────────────────────
builder.Services.AddTransient<PackImportService>();
builder.Services.AddTransient<PackExportService>();
builder.Services.AddTransient<ILlmService, LlmService>();

// ── Hangfire (in-memory — recurring jobs re-registered on startup) ──
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseInMemoryStorage());
builder.Services.AddHangfireServer();

// ── SignalR ───────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── Presidio HTTP client (optional sidecar) ───────────────────────
var presidioUrl = builder.Configuration["StarkTrace:Presidio:BaseUrl"] ?? "http://localhost:3000";
builder.Services.AddHttpClient("Presidio", client =>
{
    client.BaseAddress = new Uri(presidioUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

// ── API ───────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(o => o.AddPolicy("Dashboard", p =>
    p.SetIsOriginAllowed(origin =>
        Uri.TryCreate(origin, UriKind.Absolute, out var u) && u.Host == "localhost")
     .AllowAnyMethod().AllowAnyHeader().AllowCredentials()));

var app = builder.Build();

// ── Auto-migrate SQLite on startup ────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<StarkTraceDbContext>();

    // Baseline: DBs created by EnsureCreated (pre-migrations) have tables but no
    // __EFMigrationsHistory. Record InitialCreate as already applied, then migrate.
    var migrations = db.Database.GetMigrations().ToList();
    if (migrations.Count > 0)
    {
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        long userTables, historyTables;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory'";
            historyTables = (long)(await cmd.ExecuteScalarAsync())!;
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
            userTables = (long)(await cmd.ExecuteScalarAsync())!;
        }
        if (historyTables == 0 && userTables > 0)
        {
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TABLE \"__EFMigrationsHistory\" (\"MigrationId\" TEXT NOT NULL CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY, \"ProductVersion\" TEXT NOT NULL)");
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ({0}, {1})",
                migrations[0], "8.0.23");
        }
        await db.Database.MigrateAsync();
    }
    else
    {
        // No migrations generated yet — fall back to EnsureCreated
        await db.Database.EnsureCreatedAsync();
    }

    // Seed default audit allowlist on first run (user profile paths + env tokens)
    if (!await db.SystemSettings.AnyAsync(s => s.Key == AuditAllowlistProvider.SettingKey))
    {
        db.SystemSettings.Add(new SystemSetting
        {
            Key = AuditAllowlistProvider.SettingKey,
            Value = System.Text.Json.JsonSerializer.Serialize(new[]
            {
                Regex.Escape(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\"),
                Regex.Escape("%LOCALAPPDATA%"),
                Regex.Escape("%APPDATA%"),
                Regex.Escape("%PROGRAMDATA%")
            }),
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

}

if (app.Environment.IsDevelopment() ||
    builder.Configuration.GetValue<bool>("StarkTrace:EnableSwagger"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();
app.UseCors("Dashboard");
app.UseStaticFiles(new StaticFileOptions
{
    ServeUnknownFileTypes = true,
    DefaultContentType = "application/octet-stream"
});
if (app.Environment.IsDevelopment() ||
    builder.Configuration.GetValue<bool>("StarkTrace:EnableHangfire"))
{
    app.UseHangfireDashboard("/hangfire");
}
app.MapControllers();
app.MapHub<StarkTraceHub>("/hubs/starktrace");

// SPA fallback ΓÇö skip API/hub/health routes and middleware paths
app.MapFallback(async context =>
{
    var path = context.Request.Path.Value ?? "";
    if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/hubs", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/hangfire", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = 404;
        return;
    }
    var index = app.Environment.WebRootFileProvider.GetFileInfo("index.html");
    if (!index.Exists)
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsync("Dashboard assets not found. Rebuild the API or check the deployment layout.");
        return;
    }
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(index);
});

app.Lifetime.ApplicationStarted.Register(() =>
{
    using var scope = app.Services.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    manager.AddOrUpdate<ConsolidationJob>(
        "nightly-consolidation",
        job => job.RunAsync(),
        "0 2 * * *",
        new RecurringJobOptions { MisfireHandling = MisfireHandlingMode.Relaxed });
});

app.MapGet("/health", async (IHttpClientFactory httpFactory, IConfiguration config) =>
{
    var checks = new Dictionary<string, object>();

    try
    {
        var presidioClient = httpFactory.CreateClient("Presidio");
        var response = await presidioClient.GetAsync("/health");
        checks["presidio"] = new { status = response.IsSuccessStatusCode ? "healthy" : "degraded" };
    }
    catch { checks["presidio"] = new { status = "offline" }; }

    try
    {
        var http = httpFactory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var response = await http.GetAsync($"{ollamaUrl}/api/tags", cts.Token);
        checks["ollama"] = new { status = response.IsSuccessStatusCode ? "healthy" : "degraded" };
    }
    catch { checks["ollama"] = new { status = "offline" }; }

    var allHealthy = checks.Values.All(v =>
        v.GetType().GetProperty("status")?.GetValue(v)?.ToString() == "healthy");

    return Results.Ok(new
    {
        status = allHealthy ? "healthy" : "degraded",
        version = "2.0",
        services = checks,
        warnings = checks
            .Where(kv => kv.Value.GetType().GetProperty("status")?.GetValue(kv.Value)?.ToString() != "healthy")
            .Select(kv => $"{kv.Key} is not available — running without {(kv.Key == "presidio" ? "PII detection" : kv.Key)}")
            .ToList()
    });
});

app.Run();
