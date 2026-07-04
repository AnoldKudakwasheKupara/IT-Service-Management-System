using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models.Efm;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Services.Efm
{
    /// <summary>File-completeness result for one employee.</summary>
    public record CompletenessResult(int Percent, int RequiredCount, int PresentCount, List<string> MissingCategories);

    /// <summary>
    /// Expiry alerting, retention enforcement and file-completeness for Employee File Management.
    /// Invoked periodically by <see cref="DocumentMaintenanceHostedService"/> and on demand by HR.
    /// </summary>
    public class DocumentMaintenanceService
    {
        private readonly ApplicationDbContext _db;
        private readonly ConfigurationService _config;
        private readonly EmailDispatcher _email;
        private readonly ILogger<DocumentMaintenanceService> _logger;

        private static readonly int[] Thresholds = { 30, 60, 90, 180 };

        public DocumentMaintenanceService(ApplicationDbContext db, ConfigurationService config,
            EmailDispatcher email, ILogger<DocumentMaintenanceService> logger)
        {
            _db = db;
            _config = config;
            _email = email;
            _logger = logger;
        }

        /// <summary>Seeds a sensible default set of required documents on first run (idempotent).</summary>
        public async Task SeedRequiredDocumentsAsync()
        {
            if (await _db.RequiredDocuments.AnyAsync()) return;
            // Category ids from the schema seed: Contract, National ID, Tax Cert, NSSA, Medical Aid, Degree.
            foreach (var catId in new[] { 8, 2, 12, 11, 10, 13 })
                if (await _db.DocumentCategories.AnyAsync(c => c.Id == catId))
                    _db.RequiredDocuments.Add(new RequiredDocument { CategoryId = catId, IsMandatory = true, IsActive = true });
            await _db.SaveChangesAsync();
            _logger.LogInformation("Seeded default required documents.");
        }

        /// <summary>Creates expiry alerts (30/60/90/180d + expired) and marks past-due documents Expired.</summary>
        public async Task<(int AlertsCreated, int ExpiredMarked)> RunExpiryScanAsync()
        {
            var today = DateTime.Today;
            int alerts = 0, expired = 0;

            var docs = await _db.EmployeeDocuments
                .Where(d => d.ExpiryDate != null && !d.IsArchived)
                .Select(d => new { d.Id, d.EmployeeId, d.Title, d.ExpiryDate, d.Status })
                .ToListAsync();
            if (docs.Count == 0) return (0, 0);

            var existing = (await _db.ExpiryAlerts
                    .Select(a => new { a.EmployeeDocumentId, a.ThresholdDays }).ToListAsync())
                .Select(e => (e.EmployeeDocumentId, e.ThresholdDays)).ToHashSet();

            // Employee contacts, so we can remind owners about their own expiring documents.
            var empIds = docs.Select(d => d.EmployeeId).Distinct().ToList();
            var employees = await _db.Users.Where(u => empIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u);

            var digest = new List<string>();

            foreach (var d in docs)
            {
                var days = (d.ExpiryDate!.Value.Date - today).Days;
                int? bucket = days < 0 ? 0 : Thresholds.Cast<int?>().FirstOrDefault(t => days <= t);
                if (bucket == null) continue;

                if (days < 0 && d.Status != DocumentStatus.Expired)
                {
                    var doc = await _db.EmployeeDocuments.FindAsync(d.Id);
                    if (doc != null) { doc.Status = DocumentStatus.Expired; expired++; }
                }

                if (existing.Contains((d.Id, bucket.Value))) continue;

                _db.ExpiryAlerts.Add(new ExpiryAlert { EmployeeDocumentId = d.Id, ThresholdDays = bucket.Value, AlertedAt = DateTime.Now });

                var title = days < 0 ? "Document expired" : $"Document expiring in {days} day(s)";
                var msg = days < 0
                    ? $"'{d.Title}' has expired ({d.ExpiryDate:MMM dd, yyyy})."
                    : $"'{d.Title}' expires on {d.ExpiryDate:MMM dd, yyyy} ({days} days).";
                var type = days < 0 ? DocumentNotificationType.DocumentExpired : DocumentNotificationType.DocumentExpiring;
                _db.DocumentNotifications.Add(new DocumentNotification
                {
                    Type = type,
                    EmployeeDocumentId = d.Id,
                    EmployeeId = d.EmployeeId,
                    Title = title,
                    Message = msg,
                    CreatedAt = DateTime.Now
                });

                // Also remind the owning employee directly (own bell + email) about their document.
                _db.DocumentNotifications.Add(new DocumentNotification
                {
                    Type = type,
                    EmployeeDocumentId = d.Id,
                    EmployeeId = d.EmployeeId,
                    RecipientUserId = d.EmployeeId,
                    Title = title,
                    Message = msg,
                    CreatedAt = DateTime.Now
                });
                if (employees.TryGetValue(d.EmployeeId, out var emp) && !string.IsNullOrWhiteSpace(emp.Email))
                {
                    var body = $"<p>Hi {System.Net.WebUtility.HtmlEncode(emp.FirstName)},</p>" +
                               $"<p>{System.Net.WebUtility.HtmlEncode(msg)}</p>" +
                               "<p>Please provide an up-to-date copy to HR.</p>" +
                               "<p style='color:#6b7280;font-size:0.85rem;'>Axis IT — Employee File Management</p>";
                    _email.Queue(emp.Email, emp.FullName, $"[Axis IT] {title}: {d.Title}", body);
                }

                digest.Add(msg);
                alerts++;
            }

            await _db.SaveChangesAsync();
            if (digest.Count > 0) EmailDigest("Document expiry alerts", digest);
            _logger.LogInformation("Expiry scan: {Alerts} new alert(s), {Expired} marked expired.", alerts, expired);
            return (alerts, expired);
        }

        /// <summary>Applies retention policies: computes retention dates and archives/deletes/flags due documents.</summary>
        public async Task<(int Archived, int Deleted, int Flagged)> RunRetentionScanAsync()
        {
            var today = DateTime.Today;
            int archived = 0, deleted = 0, flagged = 0;

            var policies = await _db.RetentionPolicies
                .Where(p => p.IsActive && p.RetentionYears != null).ToListAsync();
            if (policies.Count == 0) return (0, 0, 0);

            var docs = await _db.EmployeeDocuments.Where(d => !d.IsArchived).ToListAsync();
            foreach (var d in docs)
            {
                var policy = policies.FirstOrDefault(p => p.CategoryId == d.CategoryId)
                             ?? policies.FirstOrDefault(p => p.FolderId == d.FolderId);
                if (policy?.RetentionYears == null) continue;

                // NOTE: base date is issue/created date (no termination date is tracked).
                d.RetentionUntil = (d.IssueDate ?? d.CreatedAt).AddYears(policy.RetentionYears.Value);
                if (d.RetentionUntil > today) continue;

                switch (policy.Action)
                {
                    case RetentionAction.Archive:
                        d.IsArchived = true; d.ArchivedAt = DateTime.Now; d.Status = DocumentStatus.Archived; archived++;
                        break;
                    case RetentionAction.Delete:
                        d.IsDeleted = true; deleted++;
                        break;
                    default:
                        _db.DocumentNotifications.Add(new DocumentNotification
                        {
                            Type = DocumentNotificationType.MissingDocument,
                            EmployeeDocumentId = d.Id, EmployeeId = d.EmployeeId,
                            Title = "Retention date reached",
                            Message = $"'{d.Title}' has reached its retention date.",
                            CreatedAt = DateTime.Now
                        });
                        flagged++;
                        break;
                }
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation("Retention scan: {A} archived, {D} deleted, {F} flagged.", archived, deleted, flagged);
            return (archived, deleted, flagged);
        }

        /// <summary>Computes an employee's file completeness against the required-document set.</summary>
        public async Task<CompletenessResult> GetCompletenessAsync(int employeeId)
        {
            var emp = await _db.Users.FindAsync(employeeId);
            var roleStr = emp?.Role.ToString();

            var required = await _db.RequiredDocuments
                .Where(r => r.IsActive && r.IsMandatory
                    && (r.AppliesToRole == null || r.AppliesToRole == roleStr)
                    && (r.AppliesToDepartmentId == null || r.AppliesToDepartmentId == emp!.DepartmentId))
                .Select(r => r.CategoryId).Distinct().ToListAsync();

            if (required.Count == 0) return new CompletenessResult(100, 0, 0, new());

            var present = await _db.EmployeeDocuments
                .Where(d => d.EmployeeId == employeeId && !d.IsArchived)
                .Select(d => d.CategoryId).Distinct().ToListAsync();

            var missing = required.Except(present).ToList();
            var missingNames = await _db.DocumentCategories
                .Where(c => missing.Contains(c.Id)).Select(c => c.Name).ToListAsync();

            var percent = (int)Math.Round((required.Count - missing.Count) * 100.0 / required.Count);
            return new CompletenessResult(percent, required.Count, required.Count - missing.Count, missingNames);
        }

        private void EmailDigest(string subject, List<string> lines)
        {
            var recipients = _config.Get().AlertEmailRecipients;
            if (string.IsNullOrWhiteSpace(recipients)) return;

            var body = "<p>The following document alerts were raised:</p><ul>" +
                       string.Join("", lines.Take(50).Select(l => $"<li>{System.Net.WebUtility.HtmlEncode(l)}</li>")) +
                       "</ul>";
            foreach (var to in recipients.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                _email.Queue(to, "HR", $"[Axis IT] {subject}", body);
        }
    }
}
