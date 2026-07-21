using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using IT_Service_Management_System.Models.Ims;
using IT_Service_Management_System.Services.Efm;
using Microsoft.EntityFrameworkCore;
using static IT_Service_Management_System.Models.Ticket;

namespace IT_Service_Management_System.Services.Ims
{
    /// <summary>
    /// Document-control engine: file storage (via the shared EFM storage), version history, the
    /// Draft → Department → Quality → Management → Published workflow, distribution-driven
    /// acknowledgement generation, review scheduling and version rollback.
    /// </summary>
    public class IsoDocumentService
    {
        private readonly ApplicationDbContext _db;
        private readonly IDocumentStorage _storage;
        private readonly ImsNotificationService _notify;

        public IsoDocumentService(ApplicationDbContext db, IDocumentStorage storage, ImsNotificationService notify)
        {
            _db = db;
            _storage = storage;
            _notify = notify;
        }

        // ── Files & versions ────────────────────────────────────────────────────

        /// <summary>Persists an uploaded file to storage and returns the stored-file metadata.</summary>
        public Task<StoredFileResult> SaveFileAsync(Stream content, string fileName, string contentType, CancellationToken ct = default)
            => _storage.SaveAsync(content, fileName, contentType, ct);

        public Task<Stream> OpenFileAsync(string storedKey, CancellationToken ct = default)
            => _storage.OpenReadAsync(storedKey, ct);

        public Task<bool> DeleteFileAsync(string storedKey, CancellationToken ct = default)
            => _storage.DeleteAsync(storedKey, ct);

        /// <summary>Appends a new (immutable) version to a document. Never overwrites prior versions.</summary>
        public async Task<IsoDocumentVersion> AddVersionAsync(IsoDocument doc, StoredFileResult? file,
            string? originalFileName, string versionNumber, string? revisionNotes, int? userId)
        {
            var version = new IsoDocumentVersion
            {
                IsoDocumentId = doc.Id,
                VersionNumber = versionNumber,
                RevisionNotes = revisionNotes,
                Status = doc.Status,
                CreatedById = userId,
                StoredFileName = file?.StoredKey,
                OriginalFileName = originalFileName,
                ContentType = file?.ContentType,
                FileSize = file?.SizeBytes ?? 0,
                StorageProvider = file != null ? _storage.ProviderType.ToString() : null
            };
            _db.IsoDocumentVersions.Add(version);
            doc.CurrentVersion = versionNumber;
            await _db.SaveChangesAsync();
            return version;
        }

        /// <summary>Computes the next version number (major bumps to N+1.0; minor bumps the decimal part).</summary>
        public static string NextVersionNumber(string? current, bool major)
        {
            if (string.IsNullOrWhiteSpace(current)) return major ? "1.0" : "0.1";
            var parts = current.Split('.');
            int.TryParse(parts.ElementAtOrDefault(0), out var maj);
            int.TryParse(parts.ElementAtOrDefault(1), out var min);
            return major ? $"{maj + 1}.0" : $"{maj}.{min + 1}";
        }

        /// <summary>Computes the next review date from a base date and the review frequency.</summary>
        public static DateTime? NextReviewDate(DateTime from, ReviewFrequency frequency) => frequency switch
        {
            ReviewFrequency.Monthly => from.AddMonths(1),
            ReviewFrequency.Quarterly => from.AddMonths(3),
            ReviewFrequency.SemiAnnual => from.AddMonths(6),
            ReviewFrequency.Annual => from.AddYears(1),
            ReviewFrequency.Biennial => from.AddYears(2),
            ReviewFrequency.Triennial => from.AddYears(3),
            _ => null
        };

        // ── Workflow ─────────────────────────────────────────────────────────────

        /// <summary>The ordered approval stages a document passes through.</summary>
        public static readonly ApprovalStage[] Stages =
        {
            ApprovalStage.DepartmentReview,
            ApprovalStage.QualityReview,
            ApprovalStage.ManagementApproval
        };

        public static DocumentStatus StatusFor(ApprovalStage stage) => stage switch
        {
            ApprovalStage.DepartmentReview => DocumentStatus.DepartmentReview,
            ApprovalStage.QualityReview => DocumentStatus.QualityReview,
            _ => DocumentStatus.ManagementApproval
        };

        /// <summary>The approval stage a document is currently awaiting a decision on (null if not in workflow).</summary>
        public static ApprovalStage? StageForCurrent(IsoDocument doc) => doc.Status switch
        {
            DocumentStatus.DepartmentReview => ApprovalStage.DepartmentReview,
            DocumentStatus.QualityReview => ApprovalStage.QualityReview,
            DocumentStatus.ManagementApproval => ApprovalStage.ManagementApproval,
            _ => (ApprovalStage?)null
        };

        /// <summary>Moves a Draft/Revision document into the first review stage and opens an approval record.</summary>
        public async Task SubmitForReviewAsync(IsoDocument doc, int? userId)
        {
            doc.Status = DocumentStatus.DepartmentReview;
            doc.UpdatedAt = DateTime.Now;
            _db.IsoDocumentApprovals.Add(new IsoDocumentApproval
            {
                IsoDocumentId = doc.Id,
                IsoDocumentVersionId = doc.CurrentVersionId,
                Stage = ApprovalStage.DepartmentReview,
                Sequence = 1,
                Decision = ApprovalDecision.Pending
            });
            await _db.SaveChangesAsync();
            await _notify.NotifyManagersAsync(IsoNotificationType.General,
                $"Document submitted: {doc.DocumentNumber}",
                $"\"{doc.Title}\" entered Department Review.",
                $"/IsoDocuments/Details/{doc.Id}", "info", "IsoDocument", doc.Id);
        }

        /// <summary>
        /// Records an approver's decision at the current stage. On approval the document advances to the next
        /// stage (or Published after Management Approval); a rejection sends it back to Draft.
        /// </summary>
        public async Task RecordDecisionAsync(IsoDocument doc, ApprovalStage stage, ApprovalDecision decision,
            int? approverId, string? approverRole, string? comments)
        {
            var approval = await _db.IsoDocumentApprovals
                .Where(a => a.IsoDocumentId == doc.Id && a.Stage == stage && a.Decision == ApprovalDecision.Pending)
                .OrderByDescending(a => a.Id)
                .FirstOrDefaultAsync()
                ?? new IsoDocumentApproval { IsoDocumentId = doc.Id, Stage = stage, Sequence = (int)stage + 1 };

            if (approval.Id == 0) _db.IsoDocumentApprovals.Add(approval);
            approval.ApproverId = approverId;
            approval.ApproverRole = approverRole;
            approval.Decision = decision;
            approval.Comments = comments;
            approval.DecisionAt = DateTime.Now;
            doc.UpdatedAt = DateTime.Now;

            if (decision == ApprovalDecision.Approved)
            {
                var idx = Array.IndexOf(Stages, stage);
                if (idx >= 0 && idx < Stages.Length - 1)
                {
                    var next = Stages[idx + 1];
                    doc.Status = StatusFor(next);
                    _db.IsoDocumentApprovals.Add(new IsoDocumentApproval
                    {
                        IsoDocumentId = doc.Id,
                        IsoDocumentVersionId = doc.CurrentVersionId,
                        Stage = next,
                        Sequence = idx + 2,
                        Decision = ApprovalDecision.Pending
                    });
                    await _db.SaveChangesAsync();
                }
                else
                {
                    await PublishAsync(doc, approverId);
                }
            }
            else if (decision is ApprovalDecision.Rejected or ApprovalDecision.ReturnedForChanges)
            {
                doc.Status = decision == ApprovalDecision.Rejected ? DocumentStatus.Rejected : DocumentStatus.Draft;
                await _db.SaveChangesAsync();
            }
            else
            {
                await _db.SaveChangesAsync();
            }
        }

        /// <summary>Publishes a document: marks the current version authoritative, sets dates, and expands the
        /// distribution list into acknowledgement tasks for every targeted employee.</summary>
        public async Task PublishAsync(IsoDocument doc, int? userId)
        {
            doc.Status = DocumentStatus.Published;
            doc.PublishedAt = DateTime.Now;
            doc.IssueDate ??= DateTime.Now;
            doc.EffectiveDate ??= DateTime.Now;
            doc.UpdatedAt = DateTime.Now;
            doc.ReviewDate ??= NextReviewDate(doc.EffectiveDate ?? DateTime.Now, doc.ReviewFrequency);

            var latest = await _db.IsoDocumentVersions
                .Where(v => v.IsoDocumentId == doc.Id)
                .OrderByDescending(v => v.Id)
                .FirstOrDefaultAsync();
            if (latest != null)
            {
                foreach (var v in _db.IsoDocumentVersions.Where(v => v.IsoDocumentId == doc.Id && v.IsCurrent))
                    v.IsCurrent = false;
                latest.IsCurrent = true;
                latest.Status = DocumentStatus.Published;
                latest.ApprovedAt = DateTime.Now;
                latest.ApprovedById = userId;
                doc.CurrentVersionId = latest.Id;
                doc.CurrentVersion = latest.VersionNumber;
            }

            await _db.SaveChangesAsync();

            var created = await GenerateAcknowledgementsAsync(doc, latest?.Id);
            await _notify.NotifyManagersAsync(IsoNotificationType.DocumentPublished,
                $"Published: {doc.DocumentNumber}",
                $"\"{doc.Title}\" v{doc.CurrentVersion} published to {created} recipient(s).",
                $"/IsoDocuments/Details/{doc.Id}", "success", "IsoDocument", doc.Id);
        }

        /// <summary>Expands the document's distribution list into per-user acknowledgement rows (idempotent).</summary>
        public async Task<int> GenerateAcknowledgementsAsync(IsoDocument doc, int? versionId = null)
        {
            versionId ??= doc.CurrentVersionId;

            var distributions = await _db.IsoDocumentDistributions
                .Where(d => d.IsoDocumentId == doc.Id && d.RequiresAcknowledgement)
                .ToListAsync();

            var userIds = new HashSet<int>();
            foreach (var dist in distributions)
            {
                switch (dist.TargetType)
                {
                    case DistributionTargetType.User when dist.UserId.HasValue:
                        userIds.Add(dist.UserId.Value);
                        break;
                    case DistributionTargetType.Department when dist.DepartmentId.HasValue:
                        foreach (var id in await _db.Users
                            .Where(u => u.IsActive && u.DepartmentId == dist.DepartmentId)
                            .Select(u => u.Id).ToListAsync())
                            userIds.Add(id);
                        break;
                    case DistributionTargetType.Role when !string.IsNullOrEmpty(dist.RoleName)
                        && Enum.TryParse<UserRole>(dist.RoleName, out var role):
                        foreach (var id in await _db.Users
                            .Where(u => u.IsActive && u.Role == role)
                            .Select(u => u.Id).ToListAsync())
                            userIds.Add(id);
                        break;
                    case DistributionTargetType.AllStaff:
                        foreach (var id in await _db.Users.Where(u => u.IsActive).Select(u => u.Id).ToListAsync())
                            userIds.Add(id);
                        break;
                }
            }

            var existing = await _db.IsoDocumentAcknowledgements
                .Where(a => a.IsoDocumentId == doc.Id && a.IsoDocumentVersionId == versionId)
                .Select(a => a.UserId)
                .ToListAsync();

            var created = 0;
            foreach (var uid in userIds.Except(existing))
            {
                _db.IsoDocumentAcknowledgements.Add(new IsoDocumentAcknowledgement
                {
                    IsoDocumentId = doc.Id,
                    IsoDocumentVersionId = versionId,
                    UserId = uid,
                    Status = AcknowledgementStatus.Pending
                });
                created++;
                await _notify.NotifyUserAsync(uid, IsoNotificationType.AcknowledgementRequired,
                    $"Please acknowledge: {doc.Title}",
                    $"{doc.DocumentNumber} v{doc.CurrentVersion} requires your acknowledgement.",
                    $"/IsoDocuments/Read/{doc.Id}", "warning", "IsoDocument", doc.Id);
            }
            if (created > 0) await _db.SaveChangesAsync();
            return created;
        }

        /// <summary>Opens a document for revision — creates a fresh draft version copied from the current one.</summary>
        public async Task ReviseAsync(IsoDocument doc, int? userId)
        {
            var next = NextVersionNumber(doc.CurrentVersion, major: false);
            doc.Status = DocumentStatus.Revision;
            doc.UpdatedAt = DateTime.Now;
            _db.IsoDocumentVersions.Add(new IsoDocumentVersion
            {
                IsoDocumentId = doc.Id,
                VersionNumber = next,
                RevisionNotes = "Opened for revision.",
                Status = DocumentStatus.Revision,
                CreatedById = userId
            });
            doc.CurrentVersion = next;
            await _db.SaveChangesAsync();
        }

        public async Task ArchiveAsync(IsoDocument doc)
        {
            doc.Status = DocumentStatus.Archived;
            doc.IsArchived = true;
            doc.ArchivedAt = DateTime.Now;
            doc.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
        }

        /// <summary>Rolls back to a prior version by appending it as a new current version (history preserved).</summary>
        public async Task RestoreVersionAsync(IsoDocument doc, IsoDocumentVersion source, int? userId)
        {
            var next = NextVersionNumber(doc.CurrentVersion, major: false);
            var restored = new IsoDocumentVersion
            {
                IsoDocumentId = doc.Id,
                VersionNumber = next,
                RevisionNotes = $"Restored from v{source.VersionNumber}.",
                Status = doc.Status,
                CreatedById = userId,
                StoredFileName = source.StoredFileName,
                OriginalFileName = source.OriginalFileName,
                ContentType = source.ContentType,
                FileSize = source.FileSize,
                StorageProvider = source.StorageProvider
            };
            _db.IsoDocumentVersions.Add(restored);
            doc.CurrentVersion = next;
            doc.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
        }
    }
}
