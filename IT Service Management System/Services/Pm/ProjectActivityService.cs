using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models.Pm;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Services.Pm
{
    /// <summary>
    /// Writes the project-management audit trail and raises notifications. Every mutating action in
    /// the module funnels through here so "who changed what, when, from where" is answerable for
    /// any record without each controller having to remember.
    /// </summary>
    public class ProjectActivityService
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpContextAccessor _http;
        private readonly ILogger<ProjectActivityService> _log;

        public ProjectActivityService(ApplicationDbContext db, IHttpContextAccessor http, ILogger<ProjectActivityService> log)
        {
            _db = db; _http = http; _log = log;
        }

        private int? CurrentUserId => _http.HttpContext?.Session.GetInt32("UserId");

        private string? CurrentIp => _http.HttpContext?.Connection.RemoteIpAddress?.ToString();

        /// <summary>Record an action against a project entity. Never throws — auditing must not break the request.</summary>
        public void Log(int? projectId, string entityType, int? entityId, string action, string? summary = null,
                        string? field = null, string? oldValue = null, string? newValue = null)
        {
            try
            {
                _db.ProjectActivityLogs.Add(new ProjectActivityLog
                {
                    ProjectId = projectId,
                    EntityType = entityType,
                    EntityId = entityId,
                    Action = action,
                    Summary = Truncate(summary, 500),
                    Field = Truncate(field, 200),
                    OldValue = Truncate(oldValue, 1000),
                    NewValue = Truncate(newValue, 1000),
                    UserId = CurrentUserId,
                    IpAddress = Truncate(CurrentIp, 64),
                    At = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Failed to write project activity log for {EntityType} {EntityId}", entityType, entityId);
            }
        }

        /// <summary>
        /// Record a field change, but only when the value actually moved — keeps the trail signal-heavy.
        /// </summary>
        public void LogChange(int? projectId, string entityType, int? entityId, string field, object? oldValue, object? newValue)
        {
            var oldText = oldValue?.ToString() ?? "";
            var newText = newValue?.ToString() ?? "";
            if (oldText == newText) return;
            Log(projectId, entityType, entityId, "Changed", $"{field}: {oldText} → {newText}", field, oldText, newText);
        }

        /// <summary>Queue an in-app notification. The email copy is dispatched separately by the digest job.</summary>
        public void Notify(int userId, PmNotificationType type, string title, string? message, string? url, int? projectId = null)
        {
            // Never notify someone about their own action — it is noise.
            if (userId <= 0 || userId == CurrentUserId) return;
            try
            {
                _db.PmNotifications.Add(new PmNotification
                {
                    UserId = userId,
                    ProjectId = projectId,
                    Type = type,
                    Title = Truncate(title, 200)!,
                    Message = Truncate(message, 1000),
                    Url = Truncate(url, 400),
                    CreatedAt = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Failed to queue PM notification for user {UserId}", userId);
            }
        }

        /// <summary>Notify several people at once, skipping duplicates and the acting user.</summary>
        public void NotifyMany(IEnumerable<int> userIds, PmNotificationType type, string title, string? message, string? url, int? projectId = null)
        {
            foreach (var id in userIds.Distinct())
                Notify(id, type, title, message, url, projectId);
        }

        /// <summary>Every user who should hear about activity on a project: the manager, sponsor and active team.</summary>
        public async Task<List<int>> ProjectAudienceAsync(int projectId)
        {
            var project = await _db.Projects.AsNoTracking()
                .Where(p => p.Id == projectId)
                .Select(p => new { p.ProjectManagerId, p.SponsorId })
                .FirstOrDefaultAsync();

            var team = await _db.ProjectTeamMembers.AsNoTracking()
                .Where(m => m.ProjectId == projectId && m.IsActive)
                .Select(m => m.UserId)
                .ToListAsync();

            if (project?.ProjectManagerId is int pm) team.Add(pm);
            if (project?.SponsorId is int sp) team.Add(sp);
            return team.Distinct().ToList();
        }

        /// <summary>
        /// Pull @mentions out of a comment body and return the ids of the users named. Matches
        /// "@First Last" and "@first.last" against active accounts.
        /// </summary>
        public async Task<List<int>> ResolveMentionsAsync(string? body)
        {
            if (string.IsNullOrWhiteSpace(body) || !body.Contains('@')) return new List<int>();

            var candidates = await _db.Users.AsNoTracking()
                .Where(u => u.IsActive)
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
                .ToListAsync();

            var hits = new List<int>();
            foreach (var u in candidates)
            {
                var handles = new[]
                {
                    $"@{u.FirstName} {u.LastName}",
                    $"@{u.FirstName}.{u.LastName}",
                    $"@{u.Email.Split('@')[0]}"
                };
                if (handles.Any(h => body.Contains(h, StringComparison.OrdinalIgnoreCase)))
                    hits.Add(u.Id);
            }
            return hits.Distinct().ToList();
        }

        private static string? Truncate(string? value, int max) =>
            string.IsNullOrEmpty(value) ? value : value.Length <= max ? value : value[..max];
    }
}
