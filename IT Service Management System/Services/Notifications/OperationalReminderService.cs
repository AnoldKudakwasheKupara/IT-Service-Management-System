using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Services.Notifications
{
    /// <summary>
    /// The pure part of the reminder rules: given a due date and the configured lead times, which
    /// reminder (if any) is owed today. Separated from the service so the decision is testable
    /// without a database, a clock, or a mail server.
    /// </summary>
    public static class ReminderRules
    {
        /// <summary>Default lead times, used when the configured list is blank or unparseable.</summary>
        public static readonly int[] DefaultLeadDays = { 30, 14, 7, 1 };

        public static int[] ParseLeadDays(string? csv)
        {
            var parsed = (csv ?? string.Empty)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(p => int.TryParse(p, out var d) ? d : -1)
                .Where(d => d >= 0)
                .Distinct()
                .OrderByDescending(d => d)
                .ToArray();

            return parsed.Length > 0 ? parsed : DefaultLeadDays;
        }

        /// <summary>
        /// The reminder owed for a record due on <paramref name="dueOn"/>, or null for silence.
        /// Picks the tightest lead time already crossed, so a scan gap collapses into one email
        /// rather than a burst of every threshold that elapsed meanwhile. Past-due records get one
        /// reminder per day, which is what makes chasing visible without becoming noise.
        /// </summary>
        public static string? TriggerFor(DateTime dueOn, DateTime today, int[] leadDays, bool chaseOverdue)
        {
            var daysUntil = (dueOn.Date - today.Date).Days;

            if (daysUntil < 0)
                return chaseOverdue ? $"overdue-{today:yyyy-MM-dd}" : null;

            var crossed = leadDays.Where(d => daysUntil <= d).ToArray();
            return crossed.Length == 0 ? null : $"d{crossed.Min()}";
        }

        public static int DaysUntil(DateTime dueOn, DateTime today) => (dueOn.Date - today.Date).Days;
    }

    /// <summary>
    /// Scans for things falling due — expiring SSL certificates, upcoming asset maintenance and
    /// unpaid service payments — and emails the operations recipients about each one.
    ///
    /// The scan runs several times a day, so every send is recorded in <see cref="NotificationLog"/>
    /// and checked against it first: one email per record per lead time, never a repeat on the next
    /// cycle. Because the log key includes the due date, moving a due date genuinely re-arms the
    /// earlier reminders instead of staying silent.
    /// </summary>
    public class OperationalReminderService
    {
        private readonly ApplicationDbContext _db;
        private readonly ConfigurationService _config;
        private readonly NotificationRecipients _recipients;
        private readonly EmailDispatcher _email;
        private readonly AppLinks _links;
        private readonly ILogger<OperationalReminderService> _logger;

        public OperationalReminderService(ApplicationDbContext db, ConfigurationService config,
            NotificationRecipients recipients, EmailDispatcher email, AppLinks links,
            ILogger<OperationalReminderService> logger)
        {
            _db = db;
            _config = config;
            _recipients = recipients;
            _email = email;
            _links = links;
            _logger = logger;
        }

        /// <summary>Runs every enabled scan. Returns how many reminder emails were queued.</summary>
        public async Task<int> ProcessAsync(DateTime now, CancellationToken ct = default)
        {
            var cfg = _config.Get();
            if (!cfg.NotifyOnCertificateExpiry && !cfg.NotifyOnMaintenanceDue && !cfg.NotifyOnPaymentDue)
                return 0;

            var to = await _recipients.OperationsAsync(ct);
            if (to.Count == 0)
            {
                _logger.LogWarning(
                    "Operational reminders are enabled but no recipients resolved. Set Configuration " +
                    "-> Operational Reminders -> recipients, or activate at least one administrator.");
                return 0;
            }

            var leadDays = ReminderRules.ParseLeadDays(cfg.ReminderLeadDays);
            var sent = 0;

            if (cfg.NotifyOnCertificateExpiry) sent += await CertificatesAsync(now, leadDays, cfg, to, ct);
            if (cfg.NotifyOnMaintenanceDue) sent += await MaintenanceAsync(now, leadDays, cfg, to, ct);
            if (cfg.NotifyOnPaymentDue) sent += await PaymentsAsync(now, leadDays, cfg, to, ct);

            if (sent > 0)
            {
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Queued {Count} operational reminder email(s).", sent);
            }

            return sent;
        }

        // ── certificates ─────────────────────────────────────────────────────────────
        private async Task<int> CertificatesAsync(DateTime now, int[] leadDays, AppConfiguration cfg,
            List<Recipient> to, CancellationToken ct)
        {
            var horizon = now.Date.AddDays(leadDays.Max());
            var certificates = await _db.SSLCertificates.AsNoTracking()
                .Where(c => !c.IsRenewed && c.ExpiryDate <= horizon)
                .ToListAsync(ct);

            var link = _links.To("SSLCertificates", "Index");
            var sent = 0;

            foreach (var cert in certificates)
            {
                var trigger = ReminderRules.TriggerFor(cert.ExpiryDate, now, leadDays, cfg.NotifyOnOverdue);
                if (trigger == null) continue;
                if (await AlreadySentAsync(NotificationSubject.Certificate, cert.Id, trigger, cert.ExpiryDate, ct)) continue;

                var days = ReminderRules.DaysUntil(cert.ExpiryDate, now);
                var subject = days < 0
                    ? $"[Action needed] SSL certificate expired: {cert.SystemName}"
                    : $"[Reminder] SSL certificate expires in {days} day(s): {cert.SystemName}";

                Queue(to, subject, EmailTemplates.CertificateExpiring(
                    cert.SystemName, cert.URL, cert.ExpiryDate, days, link));

                Record(NotificationSubject.Certificate, cert.Id, trigger, cert.ExpiryDate, now, to.Count);
                sent++;
            }

            return sent;
        }

        // ── maintenance ──────────────────────────────────────────────────────────────
        private async Task<int> MaintenanceAsync(DateTime now, int[] leadDays, AppConfiguration cfg,
            List<Recipient> to, CancellationToken ct)
        {
            var horizon = now.Date.AddDays(leadDays.Max());
            var records = await _db.MaintenanceRecords.AsNoTracking()
                .Where(m => m.NextMaintenanceDate != null && m.NextMaintenanceDate <= horizon)
                .ToListAsync(ct);

            var link = _links.To("MaintenanceRecords", "Index");
            var sent = 0;

            foreach (var record in records)
            {
                var due = record.NextMaintenanceDate!.Value;
                var trigger = ReminderRules.TriggerFor(due, now, leadDays, cfg.NotifyOnOverdue);
                if (trigger == null) continue;
                if (await AlreadySentAsync(NotificationSubject.Maintenance, record.Id, trigger, due, ct)) continue;

                var days = ReminderRules.DaysUntil(due, now);
                var asset = string.IsNullOrWhiteSpace(record.AssetName) ? "Unnamed asset" : record.AssetName!;
                var subject = days < 0
                    ? $"[Overdue] Maintenance due for {asset}"
                    : $"[Reminder] Maintenance due in {days} day(s): {asset}";

                Queue(to, subject, EmailTemplates.MaintenanceDue(
                    asset, record.MaintenanceType.ToString(), due, days, record.WorkDone, link));

                Record(NotificationSubject.Maintenance, record.Id, trigger, due, now, to.Count);
                sent++;
            }

            return sent;
        }

        // ── payments ─────────────────────────────────────────────────────────────────
        private async Task<int> PaymentsAsync(DateTime now, int[] leadDays, AppConfiguration cfg,
            List<Recipient> to, CancellationToken ct)
        {
            var horizon = now.Date.AddDays(leadDays.Max());
            var payments = await _db.Payments.AsNoTracking()
                .Where(p => p.PaidDate == null && p.DueDate <= horizon)
                .ToListAsync(ct);

            var link = _links.To("Payments", "Index");
            var sent = 0;

            foreach (var payment in payments)
            {
                var trigger = ReminderRules.TriggerFor(payment.DueDate, now, leadDays, cfg.NotifyOnOverdue);
                if (trigger == null) continue;
                if (await AlreadySentAsync(NotificationSubject.Payment, payment.Id, trigger, payment.DueDate, ct)) continue;

                var days = ReminderRules.DaysUntil(payment.DueDate, now);
                var subject = days < 0
                    ? $"[Overdue] Payment for {payment.ServiceName}"
                    : $"[Reminder] Payment due in {days} day(s): {payment.ServiceName}";

                Queue(to, subject, EmailTemplates.PaymentDue(
                    payment.ServiceName, payment.Amount, payment.DueDate, days,
                    string.IsNullOrWhiteSpace(payment.Status) ? "Unpaid" : payment.Status, link));

                Record(NotificationSubject.Payment, payment.Id, trigger, payment.DueDate, now, to.Count);
                sent++;
            }

            return sent;
        }

        // ── shared ───────────────────────────────────────────────────────────────────
        private Task<bool> AlreadySentAsync(NotificationSubject subject, int entityId, string trigger,
            DateTime dueOn, CancellationToken ct) =>
            _db.NotificationLogs.AsNoTracking().AnyAsync(n =>
                n.Subject == subject && n.EntityId == entityId &&
                n.TriggerKey == trigger && n.DueOn == dueOn, ct);

        private void Record(NotificationSubject subject, int entityId, string trigger, DateTime dueOn,
            DateTime now, int recipientCount) =>
            _db.NotificationLogs.Add(new NotificationLog
            {
                Subject = subject,
                EntityId = entityId,
                TriggerKey = trigger,
                DueOn = dueOn,
                SentAt = now,
                RecipientCount = recipientCount
            });

        private void Queue(List<Recipient> to, string subject, string html)
        {
            foreach (var r in to) _email.Queue(r.Email, r.Name, subject, html);
        }
    }
}
