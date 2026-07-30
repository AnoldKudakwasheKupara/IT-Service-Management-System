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
    /// Evidence Repository — the central register of objective evidence (records, photos, certificates,
    /// reports, screenshots…) supporting conformity to ISO 9001 / ISO 27001 clauses. Files are stored
    /// through the shared EFM document storage via <see cref="IsoDocumentService"/>.
    /// </summary>
    [IT_Service_Management_System.Filters.RoleAuthorize("Admin", "SystemsAdmin", "QualityManager", "DocumentController", "DepartmentManager", "Auditor", "ExternalAuditor")]
    public class EvidenceController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditService _audit;
        private readonly IsoDocumentService _docs;

        private static readonly string[] AllowedExtensions =
            { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg", ".txt" };
        private const long MaxFileBytes = 25 * 1024 * 1024; // 25 MB

        public EvidenceController(ApplicationDbContext db, AuditService audit, IsoDocumentService docs)
        {
            _db = db;
            _audit = audit;
            _docs = docs;
        }

        private int? Uid => HttpContext.Session.GetInt32("UserId");
        private string? Role => HttpContext.Session.GetString("UserRole");
        private bool Can(ImsPermission p) => ImsAccess.Can(Role, p);

        private IActionResult Denied() => RedirectToAction("AccessDenied", "Home");

        private void LoadLookups()
        {
            ViewBag.Users = _db.Users.Where(u => u.IsActive).OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToList();
            ViewBag.Departments = _db.Departments.OrderBy(d => d.Name).ToList();
        }

        // ── LIST ─────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Index(EvidenceType? type, IsoStandard? standard, string? clause, string? q)
        {
            var query = _db.IsoEvidences
                .Include(e => e.UploadedBy)
                .AsQueryable();

            if (type.HasValue) query = query.Where(e => e.Type == type.Value);
            if (standard.HasValue) query = query.Where(e => e.Standard == standard.Value);
            if (!string.IsNullOrWhiteSpace(clause)) query = query.Where(e => e.IsoClause != null && e.IsoClause.Contains(clause.Trim()));
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(e => e.Title.Contains(term)
                    || (e.Description != null && e.Description.Contains(term))
                    || (e.OriginalFileName != null && e.OriginalFileName.Contains(term)));
            }

            var list = await query.OrderByDescending(e => e.CreatedAt).ToListAsync();

            ViewBag.Type = type;
            ViewBag.Standard = standard;
            ViewBag.Clause = clause;
            ViewBag.Query = q;
            ViewBag.Total = list.Count;
            ViewBag.Documents = list.Count(e => e.Type == EvidenceType.Document || e.Type == EvidenceType.Record);
            ViewBag.Certificates = list.Count(e => e.Type == EvidenceType.Certificate);
            ViewBag.WithFile = list.Count(e => e.HasFile);
            ViewBag.CanManage = Can(ImsPermission.ManageEvidence);

            return View(list);
        }

        // ── DETAILS ──────────────────────────────────────────────────────────────
        public async Task<IActionResult> Details(int id)
        {
            var evidence = await _db.IsoEvidences
                .Include(e => e.UploadedBy)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (evidence == null) return NotFound();

            ViewBag.CanManage = Can(ImsPermission.ManageEvidence);
            return View(evidence);
        }

        // ── CREATE ───────────────────────────────────────────────────────────────
        public IActionResult Create()
        {
            if (!Can(ImsPermission.ManageEvidence)) return Denied();
            LoadLookups();
            return View(new IsoEvidence());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(IsoEvidence model, IFormFile? file)
        {
            if (!Can(ImsPermission.ManageEvidence)) return Denied();

            if (file != null && !ValidateFile(file, out var fileError))
                ModelState.AddModelError("", fileError!);

            if (!ModelState.IsValid)
            {
                LoadLookups();
                return View(model);
            }

            if (file != null)
            {
                using var stream = file.OpenReadStream();
                var stored = await _docs.SaveFileAsync(stream, file.FileName, file.ContentType ?? "application/octet-stream");
                model.StoredFileName = stored.StoredKey;
                model.OriginalFileName = file.FileName;
                model.ContentType = stored.ContentType;
                model.FileSize = stored.SizeBytes;
                model.StorageProvider = Models.Efm.StorageProviderType.LocalDisk.ToString();
            }

            model.UploadedById = Uid;
            model.CreatedAt = DateTime.Now;
            _db.IsoEvidences.Add(model);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Created", "IsoEvidence", model.Id, $"{model.Reference} — {model.Title}");
            TempData["Success"] = $"Evidence {model.Reference} captured.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        // ── EDIT (metadata only) ──────────────────────────────────────────────────
        public async Task<IActionResult> Edit(int id)
        {
            if (!Can(ImsPermission.ManageEvidence)) return Denied();
            var evidence = await _db.IsoEvidences.FindAsync(id);
            if (evidence == null) return NotFound();
            LoadLookups();
            return View(evidence);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, IsoEvidence model)
        {
            if (!Can(ImsPermission.ManageEvidence)) return Denied();
            var evidence = await _db.IsoEvidences.FindAsync(id);
            if (evidence == null) return NotFound();

            if (!ModelState.IsValid) { LoadLookups(); return View(model); }

            evidence.Title = model.Title;
            evidence.Description = model.Description;
            evidence.Type = model.Type;
            evidence.Standard = model.Standard;
            evidence.IsoClause = model.IsoClause;
            evidence.LinkedEntityType = model.LinkedEntityType;
            evidence.LinkedEntityId = model.LinkedEntityId;
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Updated", "IsoEvidence", evidence.Id, $"{evidence.Reference} — {evidence.Title}");
            TempData["Success"] = "Evidence updated.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── DELETE ───────────────────────────────────────────────────────────────
        // Confirmation page — deleting is destructive, so it gets the same explicit
        // acknowledgement step as tickets rather than a browser confirm() popup.
        public async Task<IActionResult> Delete(int id)
        {
            if (!Can(ImsPermission.ManageEvidence)) return Denied();
            var entity = await _db.IsoEvidences.Include(e => e.UploadedBy)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return NotFound();

            var vm = new ViewModels.DeleteConfirmationVm
            {
                EntityName = "Evidence",
                Icon = "fa-paperclip",
                RecordTitle = entity.Title,
                Reference = entity.Reference,
                Controller = "Evidence",
                Id = entity.Id
            };
            vm.Add("Type", entity.Type.ToString());
            vm.Add("ISO Clause", entity.IsoClause);
            vm.Add("Standard", entity.Standard.ToString());
            vm.Add("Uploaded By", entity.UploadedBy?.FullName);
            vm.Add("File", entity.OriginalFileName);
            vm.Consequences.Add("The evidence record and the file stored against it will be permanently removed.");
            vm.Consequences.Add("Any audit, finding, CAPA or risk relying on it will lose this objective evidence of conformity.");
            return View("DeleteConfirm", vm);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!Can(ImsPermission.ManageEvidence)) return Denied();
            var evidence = await _db.IsoEvidences.FindAsync(id);
            if (evidence == null) return NotFound();

            var reference = evidence.Reference;
            _db.IsoEvidences.Remove(evidence);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Deleted", "IsoEvidence", id, $"{reference} deleted.");
            TempData["Success"] = "Evidence deleted.";
            return RedirectToAction(nameof(Index));
        }

        // ── DOWNLOAD ─────────────────────────────────────────────────────────────
        public async Task<IActionResult> Download(int id)
        {
            var evidence = await _db.IsoEvidences.FindAsync(id);
            if (evidence == null) return NotFound();
            if (string.IsNullOrEmpty(evidence.StoredFileName))
            {
                TempData["Error"] = "This evidence record has no file attached.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var stream = await _docs.OpenFileAsync(evidence.StoredFileName);
            return File(stream, evidence.ContentType ?? "application/octet-stream",
                evidence.OriginalFileName ?? $"{evidence.Reference}");
        }

        // ── Helpers ──────────────────────────────────────────────────────────────
        private bool ValidateFile(IFormFile file, out string? error)
        {
            error = null;
            if (file.Length == 0) { error = "The uploaded file is empty."; return false; }
            if (file.Length > MaxFileBytes) { error = "File exceeds the 25 MB limit."; return false; }
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext)) { error = $"File type {ext} is not allowed."; return false; }
            return true;
        }
    }
}
