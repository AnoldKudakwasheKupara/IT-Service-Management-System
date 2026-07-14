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
    /// Incident Management (ISO 9001/27001 cl. 10 Improvement). Digitises the Axis "Incident
    /// Investigation Report" form (Sections A–O): report → investigate → root cause → remedial
    /// actions → sign-off → close. Narrative sections are captured on the Create/Edit form; the
    /// investigation team, damage and remedial-action tables are managed from the Details page.
    /// </summary>
    [RoleAuthorize("Admin", "SystemsAdmin", "QualityManager", "DocumentController", "DepartmentManager", "Auditor", "ExternalAuditor")]
    public class IncidentsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditService _audit;
        private readonly IsoDocumentService _docs;

        private static readonly string[] AllowedExtensions =
            { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg", ".txt", ".csv", ".msg", ".eml" };
        private const long MaxFileBytes = 25 * 1024 * 1024; // 25 MB/file

        public IncidentsController(ApplicationDbContext db, AuditService audit, IsoDocumentService docs)
        {
            _db = db;
            _audit = audit;
            _docs = docs;
        }

        private bool ValidateFile(IFormFile file, out string? error)
        {
            error = null;
            if (file.Length == 0) { error = $"'{file.FileName}' is empty."; return false; }
            if (file.Length > MaxFileBytes) { error = $"'{file.FileName}' exceeds the 25 MB limit."; return false; }
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext)) { error = $"File type {ext} is not allowed."; return false; }
            return true;
        }

        // Persists an uploaded file to shared storage and returns the attachment row (not yet saved).
        private async Task<IncidentAttachment> StoreAsync(int incidentId, IFormFile file, IncidentAttachmentKind kind,
            string? description, string? category = null)
        {
            using var stream = file.OpenReadStream();
            var stored = await _docs.SaveFileAsync(stream, file.FileName, file.ContentType ?? "application/octet-stream");
            return new IncidentAttachment
            {
                IncidentId = incidentId,
                Kind = kind,
                Category = string.IsNullOrWhiteSpace(category) ? null : category,
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                StoredFileName = stored.StoredKey,
                OriginalFileName = file.FileName,
                ContentType = stored.ContentType,
                FileSize = stored.SizeBytes,
                StorageProvider = Models.Efm.StorageProviderType.LocalDisk.ToString(),
                UploadedById = Uid,
                UploadedAt = DateTime.Now
            };
        }

        // Gathers the per-item files (Section F documents + Section D evidence lines) posted on the form,
        // paired with the category label to store them under.
        private List<(IFormFile file, string category)> CollectCategoryFiles()
        {
            var list = new List<(IFormFile, string)>();
            foreach (var (slug, label) in IncidentFileCategories.All)
                foreach (var f in Request.Form.Files.GetFiles(slug))
                    if (f != null && f.Length > 0) list.Add((f, label));
            return list;
        }

        private int? Uid => HttpContext.Session.GetInt32("UserId");
        private string? Role => HttpContext.Session.GetString("UserRole");
        private bool Can(ImsPermission p) => ImsAccess.Can(Role, p);
        private bool CanManage => Can(ImsPermission.ManageIncidents);
        private IActionResult Denied() => RedirectToAction("AccessDenied", "Home");

        private void LoadLookups()
        {
            ViewBag.Departments = _db.Departments.OrderBy(d => d.Name).ToList();
            ViewBag.Users = _db.Users.Where(u => u.IsActive).OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToList();
        }

        // ── LIST ─────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Index(IncidentStatus? status, IncidentSeverity? severity, string? q)
        {
            var query = _db.Incidents.Include(i => i.Department).Include(i => i.CreatedBy).AsQueryable();

            if (status.HasValue) query = query.Where(i => i.Status == status.Value);
            if (severity.HasValue) query = query.Where(i => i.Severity == severity.Value);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(i => i.Title.Contains(term)
                    || (i.ReportedByName != null && i.ReportedByName.Contains(term))
                    || (i.Category != null && i.Category.Contains(term))
                    || (i.LocationOfIncident != null && i.LocationOfIncident.Contains(term)));
            }

            var list = await query.OrderByDescending(i => i.Year).ThenByDescending(i => i.IncidentNo).ToListAsync();

            ViewBag.Status = status;
            ViewBag.Severity = severity;
            ViewBag.Q = q;
            ViewBag.Total = await _db.Incidents.CountAsync();
            ViewBag.Open = await _db.Incidents.CountAsync(i => i.Status != IncidentStatus.Closed);
            ViewBag.UnderInvestigation = await _db.Incidents.CountAsync(i => i.Status == IncidentStatus.UnderInvestigation);
            ViewBag.Major = await _db.Incidents.CountAsync(i => i.Severity == IncidentSeverity.Major && i.Status != IncidentStatus.Closed);
            ViewBag.Closed = await _db.Incidents.CountAsync(i => i.Status == IncidentStatus.Closed);
            ViewBag.CanManage = CanManage;
            return View(list);
        }

        // ── DETAILS ────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Details(int id)
        {
            var incident = await LoadFullAsync(id);
            if (incident == null) return NotFound();
            ViewBag.CanManage = CanManage;
            ViewBag.Users = _db.Users.Where(u => u.IsActive).OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToList();
            return View(incident);
        }

        // Standalone print/export view — reproduces the paper form for PDF/printing.
        public async Task<IActionResult> Print(int id)
        {
            var incident = await LoadFullAsync(id);
            if (incident == null) return NotFound();
            return View(incident);
        }

        private Task<Incident?> LoadFullAsync(int id) => _db.Incidents
            .Include(i => i.Department)
            .Include(i => i.CreatedBy)
            .Include(i => i.Capa)
            .Include(i => i.Investigators)
            .Include(i => i.Damages)
            .Include(i => i.Actions)
            .Include(i => i.Attachments).ThenInclude(a => a.UploadedBy)
            .FirstOrDefaultAsync(i => i.Id == id);

        // ── CREATE ───────────────────────────────────────────────────────────────
        public IActionResult Create()
        {
            if (!CanManage) return Denied();
            LoadLookups();
            var model = new Incident
            {
                DateReported = DateTime.Today,
                ReportedByName = HttpContext.Session.GetString("UserName")
            };
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(long.MaxValue)]
        public async Task<IActionResult> Create(Incident model, IFormFile? policeReport, List<IFormFile>? attachments)
        {
            if (!CanManage) return Denied();

            // Police report is mandatory when the incident was reported to the police.
            if (model.ReportedToPolice == true && (policeReport == null || policeReport.Length == 0))
                ModelState.AddModelError(nameof(model.ReportedToPolice),
                    "A police report file must be uploaded when the incident was reported to the police.");

            var files = CollectFiles(policeReport, attachments);
            foreach (var f in files)
                if (!ValidateFile(f.file, out var err)) ModelState.AddModelError("", err!);

            var categoryFiles = CollectCategoryFiles();
            foreach (var f in categoryFiles)
                if (!ValidateFile(f.file, out var cerr)) ModelState.AddModelError("", cerr!);

            if (!ModelState.IsValid) { LoadLookups(); return View(model); }

            var year = (model.DateOfIncident ?? DateTime.Now).Year;
            var lastNo = await _db.Incidents.Where(i => i.Year == year).MaxAsync(i => (int?)i.IncidentNo) ?? 0;
            model.Year = year;
            model.IncidentNo = lastNo + 1;
            model.CreatedById = Uid;
            model.CreatedAt = DateTime.Now;

            _db.Incidents.Add(model);
            await _db.SaveChangesAsync();

            foreach (var f in files)
                _db.IncidentAttachments.Add(await StoreAsync(model.Id, f.file, f.kind, null));
            foreach (var f in categoryFiles)
                _db.IncidentAttachments.Add(await StoreAsync(model.Id, f.file, IncidentAttachmentKind.Supporting, null, f.category));
            if (files.Count > 0 || categoryFiles.Count > 0) await _db.SaveChangesAsync();

            await _audit.LogAsync("Created", "Incident", model.Id, $"{model.Reference} — {model.Title}");
            TempData["Success"] = $"Incident {model.Reference} logged. Add the investigation team, damage and remedial actions below.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        // Pairs each uploaded file with its kind (the police report is tracked separately).
        private static List<(IFormFile file, IncidentAttachmentKind kind)> CollectFiles(IFormFile? policeReport, List<IFormFile>? attachments)
        {
            var list = new List<(IFormFile, IncidentAttachmentKind)>();
            if (policeReport != null && policeReport.Length > 0)
                list.Add((policeReport, IncidentAttachmentKind.PoliceReport));
            if (attachments != null)
                foreach (var f in attachments.Where(f => f != null && f.Length > 0))
                    list.Add((f, IncidentAttachmentKind.Supporting));
            return list;
        }

        // ── EDIT ───────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Edit(int id)
        {
            if (!CanManage) return Denied();
            var incident = await _db.Incidents.Include(i => i.Attachments).FirstOrDefaultAsync(i => i.Id == id);
            if (incident == null) return NotFound();
            LoadLookups();
            return View(incident);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(long.MaxValue)]
        public async Task<IActionResult> Edit(int id, Incident model, IFormFile? policeReport)
        {
            if (!CanManage) return Denied();
            var i = await _db.Incidents.FindAsync(id);
            if (i == null) return NotFound();

            // Police report mandatory when reported to police — unless one is already on file or is being uploaded now.
            var hasPoliceReport = await _db.IncidentAttachments
                .AnyAsync(a => a.IncidentId == id && a.Kind == IncidentAttachmentKind.PoliceReport);
            if (model.ReportedToPolice == true && !hasPoliceReport && (policeReport == null || policeReport.Length == 0))
                ModelState.AddModelError(nameof(model.ReportedToPolice),
                    "A police report file must be uploaded when the incident was reported to the police.");
            if (policeReport != null && policeReport.Length > 0 && !ValidateFile(policeReport, out var prErr))
                ModelState.AddModelError("", prErr!);

            var categoryFiles = CollectCategoryFiles();
            foreach (var f in categoryFiles)
                if (!ValidateFile(f.file, out var cerr)) ModelState.AddModelError("", cerr!);

            if (!ModelState.IsValid) { LoadLookups(); return View(model); }

            // Section A
            i.Title = model.Title;
            i.Standard = model.Standard;
            i.DateOfIncident = model.DateOfIncident;
            i.TimeOfIncident = model.TimeOfIncident;
            i.LocationOfIncident = model.LocationOfIncident;
            i.DepartmentId = model.DepartmentId;
            i.ReportedByName = model.ReportedByName;
            i.DateReported = model.DateReported;
            // Section B
            i.BriefDescription = model.BriefDescription;
            // Section C (police)
            i.ReportedToPolice = model.ReportedToPolice;
            i.ReportedToPoliceAt = model.ReportedToPoliceAt;
            i.PoliceDetailsTel = model.PoliceDetailsTel;
            i.CaseNumber = model.CaseNumber;
            // Section D
            i.DetailedDescription = model.DetailedDescription;
            i.EvidencePeople = model.EvidencePeople;
            i.EvidencePaper = model.EvidencePaper;
            i.EvidenceParts = model.EvidenceParts;
            i.EvidencePositions = model.EvidencePositions;
            // Section E
            i.Category = model.Category;
            i.Severity = model.Severity;
            i.Probability = model.Probability;
            i.ReportStatus = model.ReportStatus;
            // Section F
            i.DocPollutionReport = model.DocPollutionReport;
            i.DocSketchDiagram = model.DocSketchDiagram;
            i.DocWrittenStatements = model.DocWrittenStatements;
            i.DocMotorInsurance = model.DocMotorInsurance;
            i.DocDeptOfLabour = model.DocDeptOfLabour;
            i.DocDriversDetails = model.DocDriversDetails;
            i.DocInternalAudit = model.DocInternalAudit;
            i.DocWorkmenCompensation = model.DocWorkmenCompensation;
            i.DocOther = model.DocOther;
            i.DocOtherText = model.DocOtherText;
            // Section H
            i.Preventable = model.Preventable;
            i.PreventableNotes = model.PreventableNotes;
            i.Claimable = model.Claimable;
            i.ClaimedFromInsurance = model.ClaimedFromInsurance;
            i.ClaimNotes = model.ClaimNotes;
            // Section I
            i.CriticalFactors = model.CriticalFactors;
            // Section J
            i.ImmediateCause = model.ImmediateCause;
            i.BasicCause = model.BasicCause;
            i.RootCause = model.RootCause;
            // Section L
            i.LessonsLearned = model.LessonsLearned;
            // Sections M/N/O
            i.DeptManagerComments = model.DeptManagerComments;
            i.DeptManagerCommentDate = model.DeptManagerCommentDate;
            i.QaComments = model.QaComments;
            i.QaCommentDate = model.QaCommentDate;
            i.GmComments = model.GmComments;
            i.GmCommentDate = model.GmCommentDate;

            if (policeReport != null && policeReport.Length > 0)
                _db.IncidentAttachments.Add(await StoreAsync(id, policeReport, IncidentAttachmentKind.PoliceReport, "Police report"));
            foreach (var f in categoryFiles)
                _db.IncidentAttachments.Add(await StoreAsync(id, f.file, IncidentAttachmentKind.Supporting, null, f.category));

            await _db.SaveChangesAsync();
            await _audit.LogAsync("Updated", "Incident", i.Id, $"{i.Reference} — {i.Title}");
            TempData["Success"] = "Incident updated.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── STATUS ───────────────────────────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatus(int id, IncidentStatus status)
        {
            if (!CanManage) return Denied();
            var i = await _db.Incidents.FindAsync(id);
            if (i == null) return NotFound();

            i.Status = status;
            i.ClosedAt = status == IncidentStatus.Closed ? DateTime.Now : null;
            await _db.SaveChangesAsync();
            await _audit.LogAsync("StatusChanged", "Incident", id, $"{i.Reference} → {status}");
            TempData["Success"] = $"Incident marked {status}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!CanManage) return Denied();
            var i = await _db.Incidents.FindAsync(id);
            if (i == null) return NotFound();
            var reference = i.Reference;
            _db.Incidents.Remove(i);   // children cascade
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Deleted", "Incident", id, $"{reference} deleted.");
            TempData["Success"] = $"Incident {reference} deleted.";
            return RedirectToAction(nameof(Index));
        }

        // ── Section C — investigation team ─────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddInvestigator(int id, IncidentInvestigator investigator)
        {
            if (!CanManage) return Denied();
            if (!await _db.Incidents.AnyAsync(i => i.Id == id)) return NotFound();
            if (string.IsNullOrWhiteSpace(investigator.Name))
            { TempData["Error"] = "Investigator name is required."; return RedirectToAction(nameof(Details), new { id }); }

            investigator.Id = 0;   // the route's "id" must not bind to the child identity column
            investigator.IncidentId = id;
            investigator.InvestigationDate ??= DateTime.Today;
            _db.IncidentInvestigators.Add(investigator);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Investigation team member added.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveInvestigator(int id, int memberId)
        {
            if (!CanManage) return Denied();
            var m = await _db.IncidentInvestigators.FirstOrDefaultAsync(x => x.Id == memberId && x.IncidentId == id);
            if (m != null) { _db.IncidentInvestigators.Remove(m); await _db.SaveChangesAsync(); }
            TempData["Success"] = "Team member removed.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── Section G — damage ─────────────────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddDamage(int id, IncidentDamage damage)
        {
            if (!CanManage) return Denied();
            if (!await _db.Incidents.AnyAsync(i => i.Id == id)) return NotFound();
            damage.Id = 0;   // the route's "id" must not bind to the child identity column
            damage.IncidentId = id;
            _db.IncidentDamages.Add(damage);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Damage line added.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveDamage(int id, int damageId)
        {
            if (!CanManage) return Denied();
            var d = await _db.IncidentDamages.FirstOrDefaultAsync(x => x.Id == damageId && x.IncidentId == id);
            if (d != null) { _db.IncidentDamages.Remove(d); await _db.SaveChangesAsync(); }
            TempData["Success"] = "Damage line removed.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── Section K — remedial actions ───────────────────────────────────────────
        // NB: the parameter must NOT be named "action" — that collides with the {action} route
        // token, so the binder would look for "action.Description" and never find the posted field.
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAction(int id, IncidentAction remedialAction)
        {
            if (!CanManage) return Denied();
            if (!await _db.Incidents.AnyAsync(i => i.Id == id)) return NotFound();
            if (string.IsNullOrWhiteSpace(remedialAction.Description))
            { TempData["Error"] = "An action description is required."; return RedirectToAction(nameof(Details), new { id }); }

            remedialAction.Id = 0;   // the route's "id" must not bind to the child identity column
            remedialAction.IncidentId = id;
            _db.IncidentActions.Add(remedialAction);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Remedial action added.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateActionStatus(int id, int actionId, IncidentActionStatus status)
        {
            if (!CanManage) return Denied();
            var a = await _db.IncidentActions.FirstOrDefaultAsync(x => x.Id == actionId && x.IncidentId == id);
            if (a == null) return NotFound();
            a.Status = status;
            a.CompletedDate = status == IncidentActionStatus.Completed ? DateTime.Today : null;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Action status updated.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveAction(int id, int actionId)
        {
            if (!CanManage) return Denied();
            var a = await _db.IncidentActions.FirstOrDefaultAsync(x => x.Id == actionId && x.IncidentId == id);
            if (a != null) { _db.IncidentActions.Remove(a); await _db.SaveChangesAsync(); }
            TempData["Success"] = "Action removed.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── Attachments — upload as many as needed, download, remove ────────────────
        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(long.MaxValue)]
        public async Task<IActionResult> UploadAttachments(int id, List<IFormFile> files, IncidentAttachmentKind kind, string? description)
        {
            if (!CanManage) return Denied();
            if (!await _db.Incidents.AnyAsync(i => i.Id == id)) return NotFound();

            var valid = (files ?? new()).Where(f => f != null && f.Length > 0).ToList();
            if (valid.Count == 0)
            { TempData["Error"] = "Please choose at least one file."; return RedirectToAction(nameof(Details), new { id }); }

            var skipped = new List<string>();
            int saved = 0;
            foreach (var f in valid)
            {
                if (!ValidateFile(f, out var err)) { skipped.Add(err!); continue; }
                _db.IncidentAttachments.Add(await StoreAsync(id, f, kind, description));
                saved++;
            }
            if (saved > 0) await _db.SaveChangesAsync();
            await _audit.LogAsync("AttachmentUploaded", "Incident", id, $"{saved} file(s) attached.");

            if (skipped.Count > 0) TempData["Error"] = "Some files were rejected: " + string.Join("; ", skipped);
            if (saved > 0) TempData["Success"] = $"{saved} file(s) attached.";
            return RedirectToAction(nameof(Details), new { id });
        }

        public async Task<IActionResult> DownloadAttachment(int id, int attachmentId)
        {
            var a = await _db.IncidentAttachments.FirstOrDefaultAsync(x => x.Id == attachmentId && x.IncidentId == id);
            if (a == null || string.IsNullOrEmpty(a.StoredFileName)) return NotFound();
            var stream = await _docs.OpenFileAsync(a.StoredFileName);
            return File(stream, a.ContentType ?? "application/octet-stream", a.OriginalFileName);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveAttachment(int id, int attachmentId)
        {
            if (!CanManage) return Denied();
            var a = await _db.IncidentAttachments.FirstOrDefaultAsync(x => x.Id == attachmentId && x.IncidentId == id);
            if (a != null) { _db.IncidentAttachments.Remove(a); await _db.SaveChangesAsync(); }
            TempData["Success"] = "Attachment removed.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
