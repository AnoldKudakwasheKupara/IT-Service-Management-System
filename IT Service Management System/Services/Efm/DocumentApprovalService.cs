using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models.Efm;
using IT_Service_Management_System.Services;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Services.Efm
{
    /// <summary>Result of an approval decision, surfaced back to the controller for messaging.</summary>
    public record ApprovalDecisionResult(bool Ok, string Message, int EmployeeId, int? FolderId);

    /// <summary>
    /// Drives the (optionally multi-level) approval workflow for employee documents. Employee
    /// self-uploads enter as PendingApproval with a level-1 HR approval step; HR approves/rejects
    /// with a reason, the document status transitions, and the employee is emailed the outcome.
    /// </summary>
    public class DocumentApprovalService
    {
        private readonly ApplicationDbContext _db;
        private readonly DocumentService _docs;
        private readonly EmailDispatcher _email;
        private readonly ILogger<DocumentApprovalService> _logger;

        public DocumentApprovalService(ApplicationDbContext db, DocumentService docs,
            EmailDispatcher email, ILogger<DocumentApprovalService> logger)
        {
            _db = db;
            _docs = docs;
            _email = email;
            _logger = logger;
        }

        public Task<int> PendingCountAsync() =>
            _db.DocumentApprovals.CountAsync(a => a.Status == ApprovalStatus.Pending);

        /// <summary>
        /// Puts a document into the approval queue: marks it PendingApproval, records a level-1 HR
        /// approval step (unless one is already open) and notifies the HR group.
        /// </summary>
        public async Task SubmitForApprovalAsync(EmployeeDocument doc, CancellationToken ct = default)
        {
            doc.Status = DocumentStatus.PendingApproval;

            var hasOpen = await _db.DocumentApprovals
                .AnyAsync(a => a.EmployeeDocumentId == doc.Id && a.Status == ApprovalStatus.Pending, ct);
            if (!hasOpen)
            {
                _db.DocumentApprovals.Add(new DocumentApproval
                {
                    EmployeeDocumentId = doc.Id,
                    DocumentVersionId = doc.CurrentVersionId,
                    Level = 1,
                    ApproverRole = "HR",
                    Status = ApprovalStatus.Pending,
                    CreatedAt = DateTime.Now
                });
            }

            _db.DocumentNotifications.Add(new DocumentNotification
            {
                Type = DocumentNotificationType.ApprovalNeeded,
                EmployeeDocumentId = doc.Id,
                EmployeeId = doc.EmployeeId,
                RecipientUserId = null,            // HR group
                Title = "Document awaiting approval",
                Message = $"'{doc.Title}' was uploaded and needs HR approval.",
                CreatedAt = DateTime.Now
            });

            await _db.SaveChangesAsync(ct);
        }

        /// <summary>Approves or rejects the current pending step; transitions the document accordingly.</summary>
        public async Task<ApprovalDecisionResult> DecideAsync(int approvalId, bool approve, string? comments,
            int? deciderId, string? deciderName, CancellationToken ct = default)
        {
            var approval = await _db.DocumentApprovals
                .Include(a => a.Document)
                .FirstOrDefaultAsync(a => a.Id == approvalId, ct);

            if (approval?.Document == null)
                return new ApprovalDecisionResult(false, "Approval step not found.", 0, null);
            if (approval.Status != ApprovalStatus.Pending)
                return new ApprovalDecisionResult(false, "This step has already been decided.",
                    approval.Document.EmployeeId, approval.Document.FolderId);

            var doc = approval.Document;

            approval.Status = approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
            approval.DecidedById = deciderId;
            approval.DecidedByName = deciderName;
            approval.DecidedAt = DateTime.Now;
            approval.Comments = comments;

            string message;
            if (!approve)
            {
                doc.Status = DocumentStatus.Rejected;
                message = $"'{doc.Title}' was rejected.";
                await NotifyEmployeeAsync(doc, approved: false, comments, ct);
                await _docs.LogAsync(DocumentAuditAction.Rejected, doc.Id, doc.EmployeeId,
                    $"Rejected '{doc.Title}'" + (string.IsNullOrWhiteSpace(comments) ? "" : $" — {comments}"), ct);
            }
            else
            {
                // Multi-level: only clear the document once no pending steps remain.
                var stillPending = await _db.DocumentApprovals.AnyAsync(a =>
                    a.EmployeeDocumentId == doc.Id && a.Id != approval.Id && a.Status == ApprovalStatus.Pending, ct);

                if (stillPending)
                {
                    doc.Status = DocumentStatus.PendingApproval;
                    message = $"Level {approval.Level} approved — '{doc.Title}' still needs further approval.";
                }
                else
                {
                    doc.Status = DocumentStatus.Active;
                    doc.UpdatedAt = DateTime.Now;
                    message = $"'{doc.Title}' approved and is now active.";
                    await NotifyEmployeeAsync(doc, approved: true, comments, ct);
                }
                await _docs.LogAsync(DocumentAuditAction.Approved, doc.Id, doc.EmployeeId,
                    $"Approved '{doc.Title}' (level {approval.Level})", ct);
            }

            await _db.SaveChangesAsync(ct);
            return new ApprovalDecisionResult(true, message, doc.EmployeeId, doc.FolderId);
        }

        /// <summary>Notifies the owning employee of an approval decision (in-app + email).</summary>
        private async Task NotifyEmployeeAsync(EmployeeDocument doc, bool approved, string? comments, CancellationToken ct)
        {
            var title = approved ? "Document approved" : "Document rejected";
            var reason = string.IsNullOrWhiteSpace(comments) ? "" : $" Reason: {comments}";
            var body = approved
                ? $"Your document '{doc.Title}' has been approved by HR and is now on file."
                : $"Your document '{doc.Title}' was not approved.{reason} Please re-upload a corrected copy.";

            _db.DocumentNotifications.Add(new DocumentNotification
            {
                Type = approved ? DocumentNotificationType.DocumentApproved : DocumentNotificationType.DocumentRejected,
                EmployeeDocumentId = doc.Id,
                EmployeeId = doc.EmployeeId,
                RecipientUserId = doc.EmployeeId,   // to the employee personally
                Title = title,
                Message = body,
                CreatedAt = DateTime.Now
            });

            var employee = await _db.Users.FindAsync(new object[] { doc.EmployeeId }, ct);
            if (employee != null && !string.IsNullOrWhiteSpace(employee.Email))
            {
                var html = $"<p>Hi {System.Net.WebUtility.HtmlEncode(employee.FirstName)},</p>" +
                           $"<p>{System.Net.WebUtility.HtmlEncode(body)}</p>" +
                           "<p style='color:#6b7280;font-size:0.85rem;'>Axis IT — Employee File Management</p>";
                _email.Queue(employee.Email, employee.FullName, $"[Axis IT] {title}: {doc.Title}", html);
            }
        }
    }
}
