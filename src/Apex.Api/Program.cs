using Apex.Api.Hubs;
using Apex.Api.Jobs;
using Apex.Core.Interfaces;
using Apex.Infrastructure.Audit;
using Apex.Infrastructure.Context;
using Apex.Infrastructure.Data;
using Apex.Infrastructure.Experiments;
using Apex.Infrastructure.Memory;
using Apex.Infrastructure.Services;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var sqlConn = builder.Configuration.GetConnectionString("ApexDb")
    ?? "Server=127.0.0.1,1433;Database=ApexDb;User Id=sa;Password=Apex_Dev_2026!;TrustServerCertificate=true;";

// Data
builder.Services.AddDbContext<ApexDbContext>(options =>
    options.UseSqlServer(sqlConn));

// Core services
builder.Services.AddTransient<ISessionService, SessionService>();
builder.Services.AddTransient<IMessageService, MessageService>();
builder.Services.AddTransient<ISuggestionService, SuggestionService>();
builder.Services.AddTransient<IOutcomeService, OutcomeService>();
builder.Services.AddTransient<IPreferenceService, PreferenceService>();
builder.Services.AddTransient<IAntiPatternService, AntiPatternService>();
builder.Services.AddTransient<IAuditService, AuditService>();
builder.Services.AddTransient<ITaskService, TaskService>();
// Agency gate options — singleton so runtime updates propagate
var gateOptions = builder.Configuration.GetSection(AgencyGateOptions.SectionName).Get<AgencyGateOptions>() ?? new AgencyGateOptions();
builder.Services.AddSingleton(gateOptions);
builder.Services.AddTransient<IAgencyGate, AgencyGate>();

// Context injection
builder.Services.AddSingleton<AclFormatter>();
builder.Services.AddSingleton<ProseFormatter>();
builder.Services.AddSingleton<ITokenCounter, ApproximateTokenCounter>();
builder.Services.AddTransient<IContextInjector, ContextInjector>();

// Experiments
builder.Services.AddTransient<IExperimentService, ExperimentService>();

// Redis
var redisConn = builder.Configuration["Apex:Redis:Connection"] ?? "127.0.0.1:6379";
builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp =>
    StackExchange.Redis.ConnectionMultiplexer.Connect(redisConn));
builder.Services.AddTransient<ITaskQueue, RedisTaskQueue>();
builder.Services.AddTransient<IProjectSessionService, ProjectSessionService>();

// Ollama HTTP client
var ollamaUrl = builder.Configuration["Apex:Ollama:BaseUrl"] ?? "http://127.0.0.1:11434";
builder.Services.AddHttpClient("Ollama", client =>
{
    client.BaseAddress = new Uri(ollamaUrl);
    client.Timeout = TimeSpan.FromSeconds(300);
});

// Memory + Embeddings + LLM
builder.Services.AddTransient<IEmbeddingService, EmbeddingService>();
builder.Services.AddTransient<IMemoryService, MemoryService>();
builder.Services.AddTransient<ILlmService, LlmService>();

// Hangfire
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(sqlConn, new SqlServerStorageOptions
    {
        SchemaName = "Hangfire",
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.Zero
    }));
builder.Services.AddHangfireServer();

// SignalR
builder.Services.AddSignalR();

// Presidio HTTP client
var presidioUrl = builder.Configuration["Apex:Presidio:BaseUrl"] ?? "http://127.0.0.1:3000";
builder.Services.AddHttpClient("Presidio", client =>
{
    client.BaseAddress = new Uri(presidioUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

// API
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS for dashboard
builder.Services.AddCors(o => o.AddPolicy("Dashboard", p =>
    p.SetIsOriginAllowed(origin => new Uri(origin).Host == "localhost")
     .AllowAnyMethod().AllowAnyHeader().AllowCredentials()));

var app = builder.Build();

// Run SQL migrations & load persisted settings
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApexDbContext>();

    // Ensure SystemSettings table exists
    try
    {
        await db.Database.ExecuteSqlRawAsync(@"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SystemSettings')
            BEGIN
                CREATE TABLE SystemSettings (
                    [Key]       NVARCHAR(100) NOT NULL PRIMARY KEY,
                    [Value]     NVARCHAR(500) NOT NULL,
                    UpdatedAt   DATETIME NOT NULL DEFAULT GETDATE()
                );
            END");
    }
    catch { /* table may already exist */ }

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
    catch { /* first run, no settings yet */ }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Dashboard");
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [] // No auth in dev
});
app.MapControllers();
app.MapHub<ApexHub>("/hubs/apex");

// Hangfire recurring jobs
app.Lifetime.ApplicationStarted.Register(() =>
{
    using var scope = app.Services.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

    // Nightly consolidation at 2 AM
    manager.AddOrUpdate<ConsolidationJob>(
        "nightly-consolidation",
        job => job.RunAsync(),
        "0 2 * * *",
        new RecurringJobOptions { MisfireHandling = MisfireHandlingMode.Relaxed });
});

app.MapGet("/health", async (IHttpClientFactory httpFactory, IConfiguration config) =>
{
    var checks = new Dictionary<string, object>();

    // Presidio sidecar
    try
    {
        var presidioClient = httpFactory.CreateClient("Presidio");
        var response = await presidioClient.GetAsync("/health");
        checks["presidio"] = new { status = response.IsSuccessStatusCode ? "healthy" : "degraded" };
    }
    catch
    {
        checks["presidio"] = new { status = "offline" };
    }

    // Ollama
    try
    {
        var ollamaUrl = config["Apex:Ollama:BaseUrl"] ?? "http://127.0.0.1:11434";
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var response = await http.GetAsync($"{ollamaUrl}/api/tags");
        checks["ollama"] = new { status = response.IsSuccessStatusCode ? "healthy" : "degraded" };
    }
    catch
    {
        checks["ollama"] = new { status = "offline" };
    }

    var allHealthy = checks.Values.All(v =>
        v.GetType().GetProperty("status")?.GetValue(v)?.ToString() == "healthy");

    return Results.Ok(new
    {
        status = allHealthy ? "healthy" : "degraded",
        version = "2.0",
        services = checks,
        warnings = checks.Where(kv =>
            kv.Value.GetType().GetProperty("status")?.GetValue(kv.Value)?.ToString() != "healthy")
            .Select(kv => $"{kv.Key} is not available — running without {(kv.Key == "presidio" ? "PII detection" : kv.Key)}")
            .ToList()
    });
});

app.Run();
