using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using Microsoft.EntityFrameworkCore;
using static IT_Service_Management_System.Models.Ticket;

namespace IT_Service_Management_System.Services.Notifications
{
    /// <summary>One recipient of an operational email.</summary>
    public record Recipient(string Email, string Name);

    /// <summary>
    /// Works out who operational reminders and digests go to. Administrators configure an explicit
    /// list on the Configuration screen; when that is blank we fall back to every active
    /// administrator's own address, so turning the feature on never silently sends to nobody.
    /// </summary>
    public class NotificationRecipients
    {
        private readonly ApplicationDbContext _db;
        private readonly ConfigurationService _config;

        public NotificationRecipients(ApplicationDbContext db, ConfigurationService config)
        {
            _db = db;
            _config = config;
        }

        /// <summary>Recipients for reminders and the team digest.</summary>
        public async Task<List<Recipient>> OperationsAsync(CancellationToken ct = default)
        {
            var configured = Split(_config.Get().OperationsEmailRecipients);
            if (configured.Count > 0)
                return configured.Select(e => new Recipient(e, "Team")).ToList();

            return (await AdministratorsAsync(ct))
                .Select(u => new Recipient(u.Email, u.FirstName))
                .ToList();
        }

        /// <summary>Active administrators, used as the fallback recipient list.</summary>
        public Task<List<User>> AdministratorsAsync(CancellationToken ct = default) =>
            _db.Users.AsNoTracking()
                .Where(u => u.IsActive && u.Email != null && u.Email != "" &&
                            (u.Role == UserRole.Admin || u.Role == UserRole.SystemsAdmin))
                .ToListAsync(ct);

        /// <summary>Active helpdesk staff, for the per-agent daily digest.</summary>
        public Task<List<User>> HelpdeskStaffAsync(CancellationToken ct = default) =>
            _db.Users.AsNoTracking()
                .Where(u => u.IsActive && u.Email != null && u.Email != "" &&
                            (u.Role == UserRole.Admin || u.Role == UserRole.SystemsAdmin ||
                             u.Role == UserRole.SupportAgent))
                .ToListAsync(ct);

        private static List<string> Split(string? csv) =>
            (csv ?? string.Empty)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
    }
}
