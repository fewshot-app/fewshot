using Apex.Api.Hubs;
using Apex.Api.Jobs;
using Apex.Core.Interfaces;
using Apex.Infrastructure.Audit;
using Apex.Infrastructure.Context;
using Apex.Infrastructure.Data;
using Apex.Infrastructure.Experiments;
using Apex.Infrastructure.Memory;
using Apex.Infrastructure.Packs;
using Apex.Infrastructure.Services;
using Hangfire;
using Microsoft.EntityFrameworkCore;

// ── Pin working directory so Windows Service resolves paths correctly ──
Directory.SetCurrentDirectory(AppContext.BaseDirectory);

// ── Startup logging to Windows Event Log ──
const string EventSource = "APEX";
const string EventLogName = "Application";
if (!System.Diagnostics.EventLog.SourceExists(EventSource))
    System.Diagnostics.EventLog.CreateEventSource(EventSource, EventLogName);

void Log(string message) =>
    System.Diagnostics.EventLog.WriteEntry(EventSource, message, System.Diagnostics.EventLogEntryType.Information);

try
{
Log("APEX starting...");
Log($"  BaseDirectory: {AppContext.BaseDirectory}");
Log($"  CurrentDirectory: {Directory.GetCurrentDirectory()}");
Log($"  User: {Environment.UserName}");
Log($"  Is service: {!Environment.UserInteractive}");

var builder = WebApplication.CreateBuilder(args);

// ── SQLite connection string (expands %APPDATA%) ─────────────────
var rawConn = builder.Configuration.GetConnectionString("ApexDb")
    ?? "Data Source=%APPDATA%\\APEX\\apex.db";
var sqliteConn = rawConn.Replace("%APPDATA%",
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));

// Ensure data directory exists
var dbPath = sqliteConn.Replace("Data Source=", "").Trim();
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

// ── Data ─────────────────────────────────────────────────────────
builder.Services.AddDbContext<ApexDbContext>(options =>
    options.UseSqlite(sqliteConn));

// ── Core services ─────────────────────────────────────────────────
builder.Services.AddTransient<ISessionService, SessionService>();
builder.Services.AddTransient<IMessageService, MessageService>();
builder.Services.AddTransient<ISuggestionService, SuggestionService>();
builder.Services.AddTransient<IOutcomeService, OutcomeService>();
builder.Services.AddTransient<IPreferenceService, PreferenceService>();
builder.Services.AddTransient<IAntiPatternService, AntiPatternService>();
builder.Services.AddTransient<IAuditService, AuditService>();
builder.Services.AddTransient<ITaskService, TaskService>();

var gateOptions = builder.Configuration.GetSection(AgencyGateOptions.SectionName).Get<AgencyGateOptions>() ?? new AgencyGateOptions();
builder.Services.AddSingleton(gateOptions);
builder.Services.AddTransient<IAgencyGate, AgencyGate>();

// ── Context injection ─────────────────────────────────────────────
builder.Services.AddSingleton<AclFormatter>();
builder.Services.AddSingleton<ProseFormatter>();
builder.Services.AddSingleton<ITokenCounter, ApproximateTokenCounter>();
builder.Services.AddTransient<IContextInjector, ContextInjector>();

// ── Experiments ───────────────────────────────────────────────────
builder.Services.AddTransient<IExperimentService, ExperimentService>();

// ── In-process cache + task queue (no Redis) ──────────────────────
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ITaskQueue, InMemoryTaskQueue>();
builder.Services.AddTransient<IProjectSessionService, ProjectSessionService>();

// ── Ollama HTTP client ────────────────────────────────────────────
var ollamaUrl = builder.Configuration["Apex:Ollama:BaseUrl"] ?? "http://127.0.0.1:11434";
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

// ── Hangfire (in-memory — jobs survive restarts via SQLite sessions) ──
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseInMemoryStorage());
builder.Services.AddHangfireServer();

// ── SignalR ───────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── Presidio HTTP client (optional sidecar) ───────────────────────
var presidioUrl = builder.Configuration["Apex:Presidio:BaseUrl"] ?? "http://127.0.0.1:3000";
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
    p.SetIsOriginAllowed(origin => new Uri(origin).Host == "localhost")
     .AllowAnyMethod().AllowAnyHeader().AllowCredentials()));

var app = builder.Build();

// ── Auto-migrate SQLite on startup ────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApexDbContext>();
    await db.Database.EnsureCreatedAsync();

    // Load persisted gate thresholds
    try
    {
        var settings = await db.SystemSettings.ToDictionaryAsync(s => s.Key, s => s.Value);
        if (settings.TryGetValue("AgencyGate:MinSuggestions", out var ms))
            gateOptions.MinSuggestions = int.Parse(ms);
        if (settings.TryGetValue("AgencyGate:MinFeedbackRate", out var fr))
            gateOptions.MinFeedbackRate = double.Parse(fr);
        if (settings.TryGetValue("AgencyGate:MinAntiPatternSuppressions", out var ap))
            gateOptions.MinAntiPatternSuppressions = int.Parse(ap);
        if (settings.TryGetValue("AgencyGate:MinConsolidatedSessions", out var cs))
            gateOptions.MinConsolidatedSessions = int.Parse(cs);
    }
    catch { /* first run */ }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Dashboard");
app.UseHangfireDashboard("/hangfire", new DashboardOptions { Authorization = [] });
app.MapControllers();
app.MapHub<ApexHub>("/hubs/apex");

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
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var response = await http.GetAsync($"{ollamaUrl}/api/tags");
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

Log("Host built and configured. Calling app.Run()...");
app.Run();
Log("APEX stopped.");
}
catch (Exception ex)
{
    System.Diagnostics.EventLog.WriteEntry(EventSource, $"FATAL: {ex}",
        System.Diagnostics.EventLogEntryType.Error);
    throw;
}
