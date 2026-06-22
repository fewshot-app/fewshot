using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Apex.Mcp;

// MCP protocol uses stdio — stdout must contain ONLY JSON protocol messages.
// Redirect Console.Out to stderr so any accidental writes don't corrupt the protocol.
Console.SetOut(Console.Error);

var host = Host.CreateDefaultBuilder(args)
    .UseConsoleLifetime(opts => opts.SuppressStatusMessages = true)
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
        logging.SetMinimumLevel(LogLevel.Debug);
    })
    .ConfigureServices((ctx, services) =>
    {
        var apexBase = ctx.Configuration["Apex:ApiBaseUrl"] ?? "http://localhost:5000";
        services.AddHttpClient("Apex", c =>
        {
            c.BaseAddress = new Uri(apexBase);
            c.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddSingleton<ApexClient>();
        services.AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<ApexTools>();
    })
    .Build();

await host.RunAsync();
