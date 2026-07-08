using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models.Ims;
using IT_Service_Management_System.Services.Realtime;

namespace IT_Service_Management_System.Services.Ims
{
    /// <summary>
    /// Creates persistent IMS notifications (backing the bell badge) and pushes them live over SignalR.
    /// Mirrors the EFM notification pattern but for the ISO module.
    /// </summary>
    public class ImsNotificationService
    {
        private readonly ApplicationDbContext _db;
        private readonly IRealtimeNotifier _rt;

        public ImsNotificationService(ApplicationDbContext db, IRealtimeNotifier rt)
        {
            _db = db;
            _rt = rt;
        }

        /// <summary>Persist and live-push a notification to a single user.</summary>
        public async Task NotifyUserAsync(int userId, IsoNotificationType type, string title, string message,
            string? url = null, string level = "info", string? entityType = null, int? entityId = null)
        {
            _db.IsoNotifications.Add(new IsoNotification
            {
                RecipientUserId = userId,
                Type = type,
                Title = title,
                Message = message,
                Url = url,
                RelatedEntityType = entityType,
                RelatedEntityId = entityId
            });
            await _db.SaveChangesAsync();
            await _rt.NotifyUserAsync(userId, new RealtimeNotice(title, message, url, level));
        }

        /// <summary>Persist a broadcast notification for the IMS managers group and live-push to staff.</summary>
        public async Task NotifyManagersAsync(IsoNotificationType type, string title, string message,
            string? url = null, string level = "info", string? entityType = null, int? entityId = null)
        {
            _db.IsoNotifications.Add(new IsoNotification
            {
                RecipientUserId = null,
                Type = type,
                Title = title,
                Message = message,
                Url = url,
                RelatedEntityType = entityType,
                RelatedEntityId = entityId
            });
            await _db.SaveChangesAsync();
            await _rt.NotifyStaffAsync(new RealtimeNotice(title, message, url, level));
        }
    }
}
