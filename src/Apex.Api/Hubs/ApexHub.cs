using Microsoft.AspNetCore.SignalR;

namespace Apex.Api.Hubs;

public class ApexHub : Hub
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

public static class ApexHubExtensions
{
    public static async Task SendTaskUpdate(this IHubContext<ApexHub> hub, int sessionId, string status, string detail)
    {
        await hub.Clients.Group($"session-{sessionId}").SendAsync("TaskUpdate", status, detail);
    }

    /// <summary>
    /// Broadcasts to all dashboard clients when a session's consolidation status changes.
    /// </summary>
    public static async Task SendSessionUpdate(this IHubContext<ApexHub> hub, int sessionId, string status, string? summary = null)
    {
        await hub.Clients.All.SendAsync("SessionUpdate", sessionId, status, summary ?? "");
    }

    public static async Task SendExperimentUpdate(this IHubContext<ApexHub> hub, string tier, string verdict)
    {
        await hub.Clients.All.SendAsync("ExperimentUpdate", tier, verdict);
    }
}
