using IT_Service_Management_System.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace IT_Service_Management_System.Services.Realtime
{
    /// <summary>A live notice pushed to the browser bell/toast.</summary>
    public record RealtimeNotice(string Title, string Message, string? Url = null, string Level = "info");

    public interface IRealtimeNotifier
    {
        Task NotifyUserAsync(int userId, RealtimeNotice notice);
        Task NotifyStaffAsync(RealtimeNotice notice);
    }

    /// <summary>Fans notices out to SignalR groups. Failures are swallowed — a dropped live
    /// notification must never break the request that produced it (the DB record still persists).</summary>
    public class RealtimeNotifier : IRealtimeNotifier
    {
        private readonly IHubContext<NotificationHub> _hub;
        private readonly ILogger<RealtimeNotifier> _logger;

        public RealtimeNotifier(IHubContext<NotificationHub> hub, ILogger<RealtimeNotifier> logger)
        {
            _hub = hub;
            _logger = logger;
        }

        public Task NotifyUserAsync(int userId, RealtimeNotice notice) =>
            SendAsync(NotificationHub.UserGroup(userId), notice);

        public Task NotifyStaffAsync(RealtimeNotice notice) =>
            SendAsync(NotificationHub.StaffGroup, notice);

        private async Task SendAsync(string group, RealtimeNotice notice)
        {
            try
            {
                await _hub.Clients.Group(group).SendAsync("notification", notice);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Realtime notify to {Group} failed (ignored).", group);
            }
        }
    }
}
