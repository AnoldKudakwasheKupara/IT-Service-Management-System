using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Models;
using Microsoft.EntityFrameworkCore;
using static IT_Service_Management_System.Models.Ticket;

namespace IT_Service_Management_System.Services.Notifications
{
    /// <summary>
    /// Composes and sends the daily digest: what happened to tickets yesterday, what work was
    /// logged, and what is coming due. Two flavours go out — a team digest covering everything,
    /// to the operations recipients, and (optionally) a personal one to each agent covering only
    /// their own queue and their own logged work.
    ///
    /// One send per day per audience, enforced through <see cref="NotificationLog"/>, so a restart
    /// or an extra scheduler tick cannot produce a second copy.
    /// </summary>
    public class DailySummaryService
    {
        private readonly ApplicationDbContext _db;
        private readonly ConfigurationService _config;
        private readonly NotificationRecipients _recipients;
        private readonly EmailDispatcher _email;
        private readonly AppLinks _links;
        private readonly ILogger<DailySummaryService> _logger;

        public DailySummaryService(ApplicationDbContext db, ConfigurationService config,
            NotificationRecipients recipients, EmailDispatcher email, AppLinks links,
            ILogger<DailySummaryService> logger)
        {
            _db = db;
            _config = config;
            _recipients = recipients;
            _email = email;
            _links = links;
            _logger = logger;
        }

        /// <summary>
        /// Sends the digest for the day that just ended, if it is due and has not gone out.
        /// Returns the number of digest emails queued.
        /// </summary>
        public async Task<int> SendIfDueAsync(DateTime now, CancellationToken ct = default)
        {
            var cfg = _config.Get();
            if (!cfg.DailySummaryEnabled) return 0;

            // Wait until the configured hour, then report on the day that has just completed.
            if (now.Hour < Math.Clamp(cfg.DailySummaryHour, 0, 23)) return 0;

            var forDate = now.Date.AddDays(-1);
            var queued = 0;

            if (!await AlreadySentAsync(entityId: 0, forDate, ct))
            {
                queued += await SendTeamDigestAsync(forDate, now, ct);
            }

            if (cfg.DailySummaryPerAgent)
                queued += await SendAgentDigestsAsync(forDate, now, ct);

            if (queued > 0)
            {
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Queued {Count} daily summary email(s) for {Date:yyyy-MM-dd}.", queued, forDate);
            }

            return queued;
        }

        // ── team digest ──────────────────────────────────────────────────────────────
        private async Task<int> SendTeamDigestAsync(DateTime forDate, DateTime now, CancellationToken ct)
        {
            var to = await _recipients.OperationsAsync(ct);
            if (to.Count == 0)
            {
                _logger.LogWarning("Daily summary is enabled but no recipients resolved; nothing sent.");
                return 0;
            }

            var from = forDate;
            var until = forDate.AddDays(1);

            var created = await _db.Tickets.AsNoTracking()
                .Where(t => t.CreatedAt >= from && t.CreatedAt < until)
                .OrderBy(t => t.CreatedAt).ToListAsync(ct);

            var closedOut = await _db.Tickets.AsNoTracking()
                .Where(t => (t.ResolvedAt >= from && t.ResolvedAt < until) ||
                            (t.ClosedAt >= from && t.ClosedAt < until))
                .OrderBy(t => t.Id).ToListAsync(ct);

            var stillOpen = await _db.Tickets.AsNoTracking()
                .Where(t => t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed)
                .OrderBy(t => t.DueAt ?? DateTime.MaxValue).ToListAsync(ct);

            var overdue = stillOpen.Where(t => t.IsSlaBreachedAt(now)).ToList();
            var work = await WorkLoggedAsync(from, until, ct);
            var maintenanceDone = await _db.MaintenanceRecords.AsNoTracking()
                .Where(m => m.MaintenanceDate >= from && m.MaintenanceDate < until)
                .ToListAsync(ct);

            // Creator names come from the tickets themselves. Reusing the activity-author lookup
            // here would leave most tickets attributed to "a user", since the people who raise
            // tickets are rarely the same people who log activity.
            var creatorIds = created.Select(t => t.CreatedById).Distinct().ToList();
            var creatorNames = await _db.Users.AsNoTracking()
                .Where(u => creatorIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

            var figures = new List<KeyValuePair<string, string>>
            {
                new("New tickets", created.Count.ToString()),
                new("Resolved", closedOut.Count.ToString()),
                new("Still open", stillOpen.Count.ToString()),
                new("Overdue", overdue.Count.ToString()),
                new("Hours logged", work.TotalHours.ToString("0.#"))
            };

            var sections = new List<(string, IEnumerable<string>)>
            {
                ("New tickets", created.Select(t => $"{t.Reference} — {t.Title} ({t.Priority}, by {NameOf(t.CreatedById, creatorNames)})")),
                ("Resolved or closed", closedOut.Select(t => $"{t.Reference} — {t.Title} ({t.Status})")),
                ("Open and past SLA", overdue.Select(t => $"{t.Reference} — {t.Title} (due {t.DueAt:dd MMM HH:mm})")),
                ("Work logged", work.Lines),
                ("Maintenance carried out", maintenanceDone.Select(m =>
                    $"{m.AssetName ?? "Unnamed asset"} — {m.MaintenanceType}" +
                    (string.IsNullOrWhiteSpace(m.WorkDone) ? "" : $": {m.WorkDone}"))),
                ("Coming due", await UpcomingAsync(now, ct))
            };

            var html = EmailTemplates.DailySummary("Team", forDate, figures, sections, _links.Dashboard());
            foreach (var r in to)
                _email.Queue(r.Email, r.Name, $"[Daily Summary] {forDate:ddd dd MMM yyyy}", html);

            Record(entityId: 0, forDate, now, to.Count);
            return to.Count;
        }

        // ── per-agent digest ─────────────────────────────────────────────────────────
        private async Task<int> SendAgentDigestsAsync(DateTime forDate, DateTime now, CancellationToken ct)
        {
            var from = forDate;
            var until = forDate.AddDays(1);
            var staff = await _recipients.HelpdeskStaffAsync(ct);
            var queued = 0;

            foreach (var agent in staff)
            {
                if (await AlreadySentAsync(agent.Id, forDate, ct)) continue;

                var assigned = await _db.Tickets.AsNoTracking()
                    .Where(t => t.AssignedToId == agent.Id &&
                                t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed)
                    .OrderBy(t => t.DueAt ?? DateTime.MaxValue).ToListAsync(ct);

                var handled = await _db.Tickets.AsNoTracking()
                    .Where(t => t.AssignedToId == agent.Id &&
                                ((t.ResolvedAt >= from && t.ResolvedAt < until) ||
                                 (t.ClosedAt >= from && t.ClosedAt < until)))
                    .ToListAsync(ct);

                var uid = agent.Id.ToString();
                var activities = await _db.Activities.AsNoTracking().Include(a => a.Category)
                    .Where(a => a.UserId == uid && a.StartTime >= from && a.StartTime < until)
                    .OrderBy(a => a.StartTime).ToListAsync(ct);

                var hours = activities.Where(a => a.Duration.HasValue).Sum(a => a.Duration!.Value.TotalHours);
                var overdue = assigned.Where(t => t.IsSlaBreachedAt(now)).ToList();

                // An agent with nothing assigned and nothing logged gets no mail — a digest that
                // says "nothing" every morning trains people to ignore the ones that matter.
                if (assigned.Count == 0 && handled.Count == 0 && activities.Count == 0) continue;

                var figures = new List<KeyValuePair<string, string>>
                {
                    new("Your open", assigned.Count.ToString()),
                    new("You closed", handled.Count.ToString()),
                    new("Overdue", overdue.Count.ToString()),
                    new("Hours logged", hours.ToString("0.#"))
                };

                var sections = new List<(string, IEnumerable<string>)>
                {
                    ("Assigned to you and still open", assigned.Select(t =>
                        $"{t.Reference} — {t.Title} ({t.Priority}, {t.Status}" +
                        (t.DueAt.HasValue ? $", due {t.DueAt:dd MMM HH:mm}" : "") + ")")),
                    ("You resolved or closed", handled.Select(t => $"{t.Reference} — {t.Title}")),
                    ("Work you logged", activities.Select(a =>
                        $"{a.Title}{(a.Category != null ? $" [{a.Category.Name}]" : "")}" +
                        $"{(a.Duration.HasValue ? $" — {a.Duration.Value.TotalHours:0.#} h" : "")}"))
                };

                var html = EmailTemplates.DailySummary(agent.FirstName, forDate, figures, sections, _links.Dashboard());
                _email.Queue(agent.Email, agent.FirstName, $"[Your Day] {forDate:ddd dd MMM yyyy}", html);

                Record(agent.Id, forDate, now, 1);
                queued++;
            }

            return queued;
        }

        // ── shared ───────────────────────────────────────────────────────────────────
        /// <summary>Activity logged across the org, with the hours totalled and names resolved.</summary>
        private async Task<(double TotalHours, List<string> Lines)>
            WorkLoggedAsync(DateTime from, DateTime until, CancellationToken ct)
        {
            var activities = await _db.Activities.AsNoTracking().Include(a => a.Category)
                .Where(a => a.StartTime >= from && a.StartTime < until)
                .OrderBy(a => a.StartTime).ToListAsync(ct);

            // Activity.UserId is a string column, so the join back to Users happens in memory.
            var ids = activities.Select(a => int.TryParse(a.UserId, out var i) ? i : 0)
                .Where(i => i > 0).Distinct().ToList();
            var names = await _db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

            var lines = activities.Select(a =>
            {
                var who = int.TryParse(a.UserId, out var i) && names.TryGetValue(i, out var n) ? n : "Unknown";
                var duration = a.Duration.HasValue ? $" — {a.Duration.Value.TotalHours:0.#} h" : "";
                var category = a.Category != null ? $" [{a.Category.Name}]" : "";
                return $"{who}: {a.Title}{category}{duration}";
            }).ToList();

            var hours = activities.Where(a => a.Duration.HasValue).Sum(a => a.Duration!.Value.TotalHours);
            return (hours, lines);
        }

        /// <summary>The next week of certificates, maintenance and payments, as digest lines.</summary>
        private async Task<List<string>> UpcomingAsync(DateTime now, CancellationToken ct)
        {
            var horizon = now.Date.AddDays(7);
            var lines = new List<string>();

            lines.AddRange((await _db.SSLCertificates.AsNoTracking()
                    .Where(c => !c.IsRenewed && c.ExpiryDate <= horizon)
                    .OrderBy(c => c.ExpiryDate).ToListAsync(ct))
                .Select(c => $"Certificate {c.SystemName} expires {c.ExpiryDate:dd MMM}"));

            lines.AddRange((await _db.MaintenanceRecords.AsNoTracking()
                    .Where(m => m.NextMaintenanceDate != null && m.NextMaintenanceDate <= horizon)
                    .OrderBy(m => m.NextMaintenanceDate).ToListAsync(ct))
                .Select(m => $"Maintenance on {m.AssetName ?? "unnamed asset"} due {m.NextMaintenanceDate:dd MMM}"));

            lines.AddRange((await _db.Payments.AsNoTracking()
                    .Where(p => p.PaidDate == null && p.DueDate <= horizon)
                    .OrderBy(p => p.DueDate).ToListAsync(ct))
                .Select(p => $"Payment {p.ServiceName} ({p.Amount:N2}) due {p.DueDate:dd MMM}"));

            return lines;
        }

        private static string NameOf(int userId, Dictionary<int, string> names) =>
            names.TryGetValue(userId, out var n) ? n : "a user";

        private Task<bool> AlreadySentAsync(int entityId, DateTime forDate, CancellationToken ct) =>
            _db.NotificationLogs.AsNoTracking().AnyAsync(n =>
                n.Subject == NotificationSubject.DailySummary &&
                n.EntityId == entityId && n.DueOn == forDate, ct);

        private void Record(int entityId, DateTime forDate, DateTime now, int recipientCount) =>
            _db.NotificationLogs.Add(new NotificationLog
            {
                Subject = NotificationSubject.DailySummary,
                EntityId = entityId,
                TriggerKey = forDate.ToString("yyyy-MM-dd"),
                DueOn = forDate,
                SentAt = now,
                RecipientCount = recipientCount
            });
    }
}
