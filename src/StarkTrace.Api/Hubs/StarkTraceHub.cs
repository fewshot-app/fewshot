using Microsoft.AspNetCore.SignalR;

namespace StarkTrace.Api.Hubs;

public class StarkTraceHub : Hub
{
    public async Task JoinSessionGroup(int sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"session-{sessionId}");
    }

    public async Task LeaveSessionGroup(int sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"session-{sessionId}");
    }
}

public static class StarkTraceHubExtensions
{
    /// <summary>
    /// Broadcasts to all dashboard clients when a session's consolidation status changes.
    /// </summary>
    public static async Task SendSessionUpdate(this IHubContext<StarkTraceHub> hub, int sessionId, string status, string? summary = null)
    {
        await hub.Clients.All.SendAsync("SessionUpdate", sessionId, status, summary ?? "");
    }

    public static async Task SendExperimentUpdate(this IHubContext<StarkTraceHub> hub, string tier, string verdict)
    {
        await hub.Clients.All.SendAsync("ExperimentUpdate", tier, verdict);
    }
}
