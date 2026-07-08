using System.Security.Cryptography;
using System.Text;
using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Filters;
using IT_Service_Management_System.Helpers.Ims;
using IT_Service_Management_System.Models.Ims;
using IT_Service_Management_System.Services;
using IT_Service_Management_System.Services.Ims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>
    /// Document Control (ISO 9001/27001 cl. 7.5) — the controlled-document register and its full
    /// lifecycle: draft, multi-stage approval workflow, publishing, version history &amp; rollback,
    /// distribution, employee acknowledgement with electronic signature, and review scheduling.
    /// The same controller powers the Policies / Procedures / Work Instructions / Forms / Records
    /// views via the <c>type</c> filter.
    /// </summary>
    [RoleAuthorize("Admin", "SystemsAdmin", "QualityManager", "DocumentController", "DepartmentManager", "Auditor", "ExternalAuditor", "Employee")]
    public class IsoDocumentsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IsoDocumentService _docs;
        private readonly AuditService _audit;

        private static readonly string[] AllowedExtensions =
            { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".rtf", ".odt", ".png", ".jpg", ".jpeg" };
        private const long MaxFileBytes = 25 * 1024 * 1024; // 25 MB

        public IsoDocumentsController(ApplicationDbContext db, IsoDocumentService docs, AuditService audit)
        {
            _db = db;
            _docs = docs;
            _audit = audit;
        }

        private int? Uid => HttpContext.Session.GetInt32("UserId");
        private string? Role => HttpContext.Session.GetString("UserRole");
        private bool Can(ImsPermission p) => ImsAccess.Can(Role, p);

        private IActionResult Denied() => RedirectToAction("AccessDenied", "Home");

        private void LoadLookups()
        {
            ViewBag.Categories = _db.IsoDocumentCategories.Where(c => c.IsActive).OrderBy(c => c.Name).ToList();
            ViewBag.Departments = _db.Departments.OrderBy(d => d.Name).ToList();
            ViewBag.Users = _db.Users.Where(u => u.IsActive).OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToList();
            ViewBag.Clauses = _db.IsoClauses.OrderBy(c => c.Standard).ThenBy(c => c.ClauseNumber).ToList();
        }

        // ── LIST ─────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Index(DocumentType? type, DocumentStatus? status, int? categoryId, string? q)
        {
            var query = _db.IsoDocuments
                .Include(d => d.Category)
                .Include(d => d.Department)
                .Include(d => d.Owner)
                .AsQueryable();

            if (type.HasValue) query = query.Where(d => d.Type == type.Value);
            if (status.HasValue) query = query.Where(d => d.Status == status.Value);
            if (categoryId.HasValue) query = query.Where(d => d.CategoryId == categoryId.Value);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(d => d.Title.Contains(term) || d.DocumentNumber.Contains(term)
                    || (d.Keywords != null && d.Keywords.Contains(term)) || (d.Summary != null && d.Summary.Contains(term)));
            }

            var list = await query.OrderByDescending(d => d.UpdatedAt ?? d.CreatedAt).ToListAsync();

            ViewBag.Type = type;
            ViewBag.Status = status;
            ViewBag.CategoryId = categoryId;
            ViewBag.Query = q;
            ViewBag.Total = list.Count;
            ViewBag.Published = list.Count(d => d.Status == DocumentStatus.Published);
            ViewBag.InWorkflow = list.Count(d => d.IsInWorkflow);
            ViewBag.DueReview = list.Count(d => d.IsReviewDue);
            ViewBag.Categories = _db.IsoDocumentCategories.Where(c => c.IsActive).OrderBy(c => c.Name).ToList();
            ViewBag.CanEdit = Can(ImsPermission.CreateDocument);

            return View(list);
        }

        // ── DETAILS ────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Details(int id)
        {
            var doc = await _db.IsoDocuments
                .Include(d => d.Category)
                .Include(d => d.Department)
                .Include(d => d.Owner)
                .Include(d => d.Approver)
                .Include(d => d.Versions).ThenInclude(v => v.CreatedBy)
                .Include(d => d.Approvals).ThenInclude(a => a.Approver)
                .Include(d => d.Distributions).ThenInclude(x => x.Department)
                .Include(d => d.Distributions).ThenInclude(x => x.User)
                .Include(d => d.Reviews).ThenInclude(r => r.Reviewer)
                .FirstOrDefaultAsync(d => d.Id == id);
            if (doc == null) return NotFound();

            var acks = await _db.IsoDocumentAcknowledgements.Where(a => a.IsoDocumentId == id).ToListAsync();
            ViewBag.AckTotal = acks.Count;
            ViewBag.AckDone = acks.Count(a => a.Status == AcknowledgementStatus.Acknowledged);
            ViewBag.CanManage = Can(ImsPermission.EditDocument);
            ViewBag.CanApprove = Can(ImsPermission.ManagementApprove) || Can(ImsPermission.QualityReview) || Can(ImsPermission.DepartmentReview);
            LoadLookups();

            return View(doc);
        }

        // ── CREATE ───────────────────────────────────────────────────────────────
        public IActionResult Create(DocumentType? type)
        {
            if (!Can(ImsPermission.CreateDocument)) return Denied();
            LoadLookups();
            return View(new IsoDocument { Type = type ?? DocumentType.Policy });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(IsoDocument model, IFormFile? file)
        {
            if (!Can(ImsPermission.CreateDocument)) return Denied();

            if (file != null && !ValidateFile(file, out var fileError))
                ModelState.AddModelError("", fileError!);

            if (!ModelState.IsValid)
            {
                LoadLookups();
                return View(model);
            }

            if (string.IsNullOrWhiteSpace(model.DocumentNumber))
                model.DocumentNumber = await GenerateDocumentNumberAsync(model.Type, model.CategoryId);

            model.Status = DocumentStatus.Draft;
            model.CurrentVersion = "0.1";
            model.CreatedById = Uid;
            model.CreatedAt = DateTime.Now;
            _db.IsoDocuments.Add(model);
            await _db.SaveChangesAsync();

            Services.Efm.StoredFileResult? stored = null;
            string? originalName = null;
            if (file != null)
            {
                using var stream = file.OpenReadStream();
                stored = await _docs.SaveFileAsync(stream, file.FileName, file.ContentType ?? "application/octet-stream");
                originalName = file.FileName;
            }
            var version = await _docs.AddVersionAsync(model, stored, originalName, "0.1", "Initial draft.", Uid);
            model.CurrentVersionId = version.Id;
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Created", "IsoDocument", model.Id, $"{model.DocumentNumber} — {model.Title}");
            TempData["Success"] = $"Document {model.DocumentNumber} created as draft.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        // ── EDIT ───────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Edit(int id)
        {
            if (!Can(ImsPermission.EditDocument)) return Denied();
            var doc = await _db.IsoDocuments.FindAsync(id);
            if (doc == null) return NotFound();
            LoadLookups();
            return View(doc);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, IsoDocument model)
        {
            if (!Can(ImsPermission.EditDocument)) return Denied();
            var doc = await _db.IsoDocuments.FindAsync(id);
            if (doc == null) return NotFound();

            if (!ModelState.IsValid) { LoadLookups(); return View(model); }

            doc.Title = model.Title;
            doc.Type = model.Type;
            doc.CategoryId = model.CategoryId;
            doc.DepartmentId = model.DepartmentId;
            doc.OwnerId = model.OwnerId;
            doc.ApproverId = model.ApproverId;
            doc.Standard = model.Standard;
            doc.IsoClause = model.IsoClause;
            doc.Classification = model.Classification;
            doc.ReviewFrequency = model.ReviewFrequency;
            doc.IssueDate = model.IssueDate;
            doc.EffectiveDate = model.EffectiveDate;
            doc.ReviewDate = model.ReviewDate;
            doc.ExpiryDate = model.ExpiryDate;
            doc.Keywords = model.Keywords;
            doc.Summary = model.Summary;
            doc.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Updated", "IsoDocument", doc.Id, $"{doc.DocumentNumber} — {doc.Title}");
            TempData["Success"] = "Document updated.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── WORKFLOW ────────────────────────────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(int id)
        {
            if (!Can(ImsPermission.SubmitForReview)) return Denied();
            var doc = await _db.IsoDocuments.FindAsync(id);
            if (doc == null) return NotFound();
            if (doc.Status is not (DocumentStatus.Draft or DocumentStatus.Revision or DocumentStatus.Rejected))
            {
                TempData["Error"] = "Only a draft or in-revision document can be submitted.";
                return RedirectToAction(nameof(Details), new { id });
            }
            await _docs.SubmitForReviewAsync(doc, Uid);
            await _audit.LogAsync("Submitted", "IsoDocument", id, $"{doc.DocumentNumber} submitted for review.");
            TempData["Success"] = "Document submitted for Department Review.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Decision(int id, ApprovalStage stage, ApprovalDecision decision, string? comments)
        {
            var permission = stage switch
            {
                ApprovalStage.DepartmentReview => ImsPermission.DepartmentReview,
                ApprovalStage.QualityReview => ImsPermission.QualityReview,
                _ => ImsPermission.ManagementApprove
            };
            if (!Can(permission)) return Denied();

            var doc = await _db.IsoDocuments.FindAsync(id);
            if (doc == null) return NotFound();
            if (IsoDocumentService.StageForCurrent(doc) != stage)
            {
                TempData["Error"] = "This document is not awaiting a decision at that stage.";
                return RedirectToAction(nameof(Details), new { id });
            }

            await _docs.RecordDecisionAsync(doc, stage, decision, Uid, Role, comments);
            await _audit.LogAsync("Decision", "IsoDocument", id, $"{doc.DocumentNumber}: {stage} → {decision}");
            TempData["Success"] = $"{stage} decision recorded: {decision}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Publish(int id)
        {
            if (!Can(ImsPermission.PublishDocument)) return Denied();
            var doc = await _db.IsoDocuments.FindAsync(id);
            if (doc == null) return NotFound();
            await _docs.PublishAsync(doc, Uid);
            await _audit.LogAsync("Published", "IsoDocument", id, $"{doc.DocumentNumber} v{doc.CurrentVersion} published.");
            TempData["Success"] = "Document published and distributed for acknowledgement.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Revise(int id)
        {
            if (!Can(ImsPermission.ReviseDocument)) return Denied();
            var doc = await _db.IsoDocuments.FindAsync(id);
            if (doc == null) return NotFound();
            await _docs.ReviseAsync(doc, Uid);
            await _audit.LogAsync("Revised", "IsoDocument", id, $"{doc.DocumentNumber} opened for revision (v{doc.CurrentVersion}).");
            TempData["Success"] = "Document opened for revision.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Archive(int id)
        {
            if (!Can(ImsPermission.ArchiveDocument)) return Denied();
            var doc = await _db.IsoDocuments.FindAsync(id);
            if (doc == null) return NotFound();
            await _docs.ArchiveAsync(doc);
            await _audit.LogAsync("Archived", "IsoDocument", id, $"{doc.DocumentNumber} archived.");
            TempData["Success"] = "Document archived.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── VERSIONS ────────────────────────────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddVersion(int id, IFormFile? file, string? notes, bool major = false)
        {
            if (!Can(ImsPermission.EditDocument)) return Denied();
            var doc = await _db.IsoDocuments.FindAsync(id);
            if (doc == null) return NotFound();

            if (file != null && !ValidateFile(file, out var fileError))
            {
                TempData["Error"] = fileError;
                return RedirectToAction(nameof(Details), new { id });
            }

            Services.Efm.StoredFileResult? stored = null;
            string? originalName = null;
            if (file != null)
            {
                using var stream = file.OpenReadStream();
                stored = await _docs.SaveFileAsync(stream, file.FileName, file.ContentType ?? "application/octet-stream");
                originalName = file.FileName;
            }
            var next = IsoDocumentService.NextVersionNumber(doc.CurrentVersion, major);
            var version = await _docs.AddVersionAsync(doc, stored, originalName, next, notes, Uid);
            doc.CurrentVersionId = version.Id;
            await _db.SaveChangesAsync();

            await _audit.LogAsync("NewVersion", "IsoDocument", id, $"{doc.DocumentNumber} v{next}");
            TempData["Success"] = $"Version {next} added.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreVersion(int id, int versionId)
        {
            if (!Can(ImsPermission.RestoreVersion)) return Denied();
            var doc = await _db.IsoDocuments.FindAsync(id);
            var source = await _db.IsoDocumentVersions.FirstOrDefaultAsync(v => v.Id == versionId && v.IsoDocumentId == id);
            if (doc == null || source == null) return NotFound();
            await _docs.RestoreVersionAsync(doc, source, Uid);
            await _audit.LogAsync("RestoreVersion", "IsoDocument", id, $"{doc.DocumentNumber} restored from v{source.VersionNumber}");
            TempData["Success"] = $"Restored from version {source.VersionNumber}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        public async Task<IActionResult> Download(int versionId)
        {
            var version = await _db.IsoDocumentVersions.Include(v => v.Document).FirstOrDefaultAsync(v => v.Id == versionId);
            if (version?.Document == null) return NotFound();
            if (!Can(ImsPermission.ViewDocuments)) return Denied();
            if (string.IsNullOrEmpty(version.StoredFileName)) { TempData["Error"] = "This version has no file attached."; return RedirectToAction(nameof(Details), new { id = version.IsoDocumentId }); }

            var stream = await _docs.OpenFileAsync(version.StoredFileName);
            return File(stream, version.ContentType ?? "application/octet-stream",
                version.OriginalFileName ?? $"{version.Document.DocumentNumber}_v{version.VersionNumber}");
        }

        // ── DISTRIBUTION ──────────────────────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddDistribution(int id, DistributionTargetType targetType, int? userId, int? departmentId, string? roleName)
        {
            if (!Can(ImsPermission.ManageDistribution)) return Denied();
            var doc = await _db.IsoDocuments.FindAsync(id);
            if (doc == null) return NotFound();

            _db.IsoDocumentDistributions.Add(new IsoDocumentDistribution
            {
                IsoDocumentId = id,
                TargetType = targetType,
                UserId = targetType == DistributionTargetType.User ? userId : null,
                DepartmentId = targetType == DistributionTargetType.Department ? departmentId : null,
                RoleName = targetType == DistributionTargetType.Role ? roleName : null,
                CreatedById = Uid
            });
            await _db.SaveChangesAsync();

            if (doc.Status == DocumentStatus.Published)
                await _docs.GenerateAcknowledgementsAsync(doc);

            TempData["Success"] = "Distribution target added.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveDistribution(int id, int distributionId)
        {
            if (!Can(ImsPermission.ManageDistribution)) return Denied();
            var dist = await _db.IsoDocumentDistributions.FirstOrDefaultAsync(d => d.Id == distributionId && d.IsoDocumentId == id);
            if (dist == null) return NotFound();
            _db.IsoDocumentDistributions.Remove(dist);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Distribution target removed.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── REVIEW SCHEDULING ────────────────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ScheduleReview(int id, DateTime scheduledDate, int? reviewerId)
        {
            if (!Can(ImsPermission.EditDocument)) return Denied();
            var doc = await _db.IsoDocuments.FindAsync(id);
            if (doc == null) return NotFound();
            _db.IsoDocumentReviews.Add(new IsoDocumentReview { IsoDocumentId = id, ScheduledDate = scheduledDate, ReviewerId = reviewerId });
            doc.ReviewDate = scheduledDate;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Review scheduled.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordReview(int id, int reviewId, ReviewOutcome outcome, string? notes)
        {
            if (!Can(ImsPermission.EditDocument)) return Denied();
            var review = await _db.IsoDocumentReviews.Include(r => r.Document).FirstOrDefaultAsync(r => r.Id == reviewId && r.IsoDocumentId == id);
            if (review?.Document == null) return NotFound();
            review.Outcome = outcome;
            review.ActualDate = DateTime.Now;
            review.ReviewerId = Uid;
            review.Notes = notes;
            review.NextReviewDate = IsoDocumentService.NextReviewDate(DateTime.Now, review.Document.ReviewFrequency);
            review.Document.ReviewDate = review.NextReviewDate;
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Reviewed", "IsoDocument", id, $"{review.Document.DocumentNumber}: {outcome}");
            TempData["Success"] = "Review recorded.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── EMPLOYEE ACKNOWLEDGEMENT ─────────────────────────────────────────────
        public async Task<IActionResult> MyDocuments()
        {
            var uid = Uid;
            if (uid == null) return RedirectToAction("Login", "Account");
            var acks = await _db.IsoDocumentAcknowledgements
                .Include(a => a.Document)
                .Where(a => a.UserId == uid)
                .OrderBy(a => a.Status == AcknowledgementStatus.Acknowledged)
                .ThenByDescending(a => a.AssignedAt)
                .ToListAsync();
            return View(acks);
        }

        public async Task<IActionResult> Read(int id)
        {
            var uid = Uid;
            if (uid == null) return RedirectToAction("Login", "Account");
            var ack = await _db.IsoDocumentAcknowledgements
                .Include(a => a.Document)
                .FirstOrDefaultAsync(a => a.IsoDocumentId == id && a.UserId == uid);

            var doc = ack?.Document ?? await _db.IsoDocuments.FindAsync(id);
            if (doc == null) return NotFound();

            if (ack != null && ack.OpenedAt == null)
            {
                ack.OpenedAt = DateTime.Now;
                if (ack.Status == AcknowledgementStatus.Pending) ack.Status = AcknowledgementStatus.Opened;
                await _db.SaveChangesAsync();
            }
            ViewBag.Ack = ack;
            return View(doc);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Acknowledge(int id, string signatureName, bool accept)
        {
            var uid = Uid;
            if (uid == null) return RedirectToAction("Login", "Account");
            var ack = await _db.IsoDocumentAcknowledgements.Include(a => a.Document)
                .FirstOrDefaultAsync(a => a.IsoDocumentId == id && a.UserId == uid);
            if (ack == null) { TempData["Error"] = "No acknowledgement is assigned to you for this document."; return RedirectToAction(nameof(MyDocuments)); }

            if (!accept || string.IsNullOrWhiteSpace(signatureName))
            {
                TempData["Error"] = "You must accept and type your name as an electronic signature.";
                return RedirectToAction(nameof(Read), new { id });
            }

            ack.Status = AcknowledgementStatus.Acknowledged;
            ack.Accepted = true;
            ack.AcknowledgedAt = DateTime.Now;
            ack.SignatureName = signatureName.Trim();
            ack.SignedIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            ack.SignatureHash = Sha256($"{uid}|{id}|{ack.IsoDocumentVersionId}|{signatureName}|{ack.AcknowledgedAt:O}");
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Acknowledged", "IsoDocument", id, $"{ack.Document?.DocumentNumber} acknowledged by user {uid}");
            TempData["Success"] = "Thank you — your acknowledgement has been recorded.";
            return RedirectToAction(nameof(MyDocuments));
        }

        public async Task<IActionResult> AckReport(int id)
        {
            if (!Can(ImsPermission.EditDocument)) return Denied();
            var doc = await _db.IsoDocuments.FindAsync(id);
            if (doc == null) return NotFound();
            var acks = await _db.IsoDocumentAcknowledgements
                .Include(a => a.User).ThenInclude(u => u!.Department)
                .Where(a => a.IsoDocumentId == id)
                .OrderBy(a => a.Status == AcknowledgementStatus.Acknowledged)
                .ToListAsync();
            ViewBag.Document = doc;
            return View(acks);
        }

        // ── DELETE (soft) ────────────────────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!Can(ImsPermission.DeleteDocument)) return Denied();
            var doc = await _db.IsoDocuments.FindAsync(id);
            if (doc == null) return NotFound();
            doc.IsDeleted = true;
            doc.DeletedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Deleted", "IsoDocument", id, $"{doc.DocumentNumber} deleted.");
            TempData["Success"] = "Document deleted.";
            return RedirectToAction(nameof(Index));
        }

        // ── Helpers ────────────────────────────────────────────────────────────────
        private bool ValidateFile(IFormFile file, out string? error)
        {
            error = null;
            if (file.Length == 0) { error = "The uploaded file is empty."; return false; }
            if (file.Length > MaxFileBytes) { error = "File exceeds the 25 MB limit."; return false; }
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext)) { error = $"File type {ext} is not allowed."; return false; }
            return true;
        }

        private async Task<string> GenerateDocumentNumberAsync(DocumentType type, int? categoryId)
        {
            var prefix = type switch
            {
                DocumentType.Policy => "POL",
                DocumentType.Procedure => "PRO",
                DocumentType.WorkInstruction => "WI",
                DocumentType.Form => "FRM",
                DocumentType.Record => "REC",
                DocumentType.Manual => "MAN",
                DocumentType.Plan => "PLN",
                DocumentType.Register => "REG",
                DocumentType.Guideline => "GDL",
                _ => "DOC"
            };
            var catCode = categoryId.HasValue
                ? (await _db.IsoDocumentCategories.Where(c => c.Id == categoryId).Select(c => c.Code).FirstOrDefaultAsync() ?? "GEN")
                : "GEN";
            var baseNumber = $"{catCode}-{prefix}";
            var count = await _db.IsoDocuments.CountAsync(d => d.Type == type);
            string candidate;
            do { candidate = $"{baseNumber}-{++count:D4}"; }
            while (await _db.IsoDocuments.AnyAsync(d => d.DocumentNumber == candidate));
            return candidate;
        }

        private static string Sha256(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes);
        }
    }
}
