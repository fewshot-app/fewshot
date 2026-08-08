using Microsoft.AspNetCore.SignalR.Client;

namespace StarkTrace.Dashboard.Services;

public class StarkTraceSignalRService : IAsyncDisposable
{
    private HubConnection? _connection;
    private readonly string _hubUrl;

    public event Action<int, string, string>? OnSessionUpdate;
    public HubConnectionState State => _connection?.State ?? HubConnectionState.Disconnected;
    public bool IsConnected => State == HubConnectionState.Connected;

    public StarkTraceSignalRService(IConfiguration config)
    {
        var apiBase = config["ApiBase"] ?? "http://localhost:5000";
        _hubUrl = $"{apiBase.TrimEnd('/')}/hubs/starktrace";
    }

    public async Task StartAsync()
    {
        if (_connection != null) return;

        _connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _connection.On<int, string, string>("SessionUpdate", (sessionId, status, summary) =>
            OnSessionUpdate?.Invoke(sessionId, status, summary));

        _connection.Reconnected += _ => { NotifyStateChanged(); return Task.CompletedTask; };
        _connection.Closed += _ => { NotifyStateChanged(); return Task.CompletedTask; };

        try
        {
            await _connection.StartAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SignalR] Failed to connect: {ex.Message}");
        }

        NotifyStateChanged();
    }

    public event Action? OnStateChanged;
    private void NotifyStateChanged() => OnStateChanged?.Invoke();

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
            await _connection.DisposeAsync();
    }
}
