using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Filters;
using IT_Service_Management_System.Helpers.Ims;
using IT_Service_Management_System.Models.Ims;
using IT_Service_Management_System.Services;
using IT_Service_Management_System.Services.Ims;
using IT_Service_Management_System.Services.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>
    /// Incident Management (ISO 9001/27001 cl. 10 Improvement). Digitises the Axis "Incident
    /// Investigation Report" form (Sections A–O): report → investigate → root cause → remedial
    /// actions → sign-off → close. Narrative sections are captured on the Create/Edit form; the
    /// investigation team, damage and remedial-action tables are managed from the Details page.
    /// </summary>
    [RoleAuthorize("Admin", "SystemsAdmin", "QualityManager", "GeneralManager", "DocumentController", "DepartmentManager", "Auditor", "ExternalAuditor")]
    public class IncidentsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditService _audit;
        private readonly IsoDocumentService _docs;
        private readonly IMalwareScanner _scanner;

        private static readonly string[] AllowedExtensions =
            { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg", ".txt", ".csv", ".msg", ".eml" };
        private const long MaxFileBytes = 25 * 1024 * 1024; // 25 MB/file

        public IncidentsController(ApplicationDbContext db, AuditService audit, IsoDocumentService docs,
            IMalwareScanner scanner)
        {
            _db = db;
            _audit = audit;
            _docs = docs;
            _scanner = scanner;
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

        private async Task<(byte[]? Content, string? Error)> VetFileAsync(IFormFile file)
        {
            if (!ValidateFile(file, out var validationError)) return (null, validationError);

            byte[] content;
            await using (var input = file.OpenReadStream())
            using (var buffer = new MemoryStream())
            {
                await input.CopyToAsync(buffer);
                content = buffer.ToArray();
            }

            var scan = await _scanner.ScanAsync(content, file.FileName);
            return scan.IsClean
                ? (content, null)
                : (null, $"'{file.FileName}' was rejected: malware detected ({scan.Threat}).");
        }

        // Persists already-vetted bytes to shared storage and returns the attachment row (not yet saved).
        private async Task<IncidentAttachment> StoreAsync(int incidentId, IFormFile file, byte[] content,
            IncidentAttachmentKind kind, string? description, string? category = null)
        {
            using var stream = new MemoryStream(content, writable: false);
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
        private bool IsDepartmentManager => ImsAccess.IsDepartmentManager(Role);
        private bool IsAdministrator => ImsAccess.IsAdministrator(Role);
        private bool CanSignQuality => IsAdministrator || ImsAccess.IsQualityManager(Role);
        private bool CanSignGeneral => IsAdministrator || ImsAccess.IsGeneralManager(Role);
        private IActionResult Denied() => RedirectToAction("AccessDenied", "Home");

        private Task<int?> CurrentDepartmentIdAsync()
        {
            if (Uid == null) return Task.FromResult<int?>(null);
            return _db.Users.AsNoTracking().Where(u => u.Id == Uid.Value)
                .Select(u => u.DepartmentId).FirstOrDefaultAsync();
        }

        private async Task<bool> IsInDepartmentScopeAsync(int incidentId)
        {
            if (!IsDepartmentManager) return true;
            var departmentId = await CurrentDepartmentIdAsync();
            return departmentId.HasValue && await _db.Incidents
                .AnyAsync(i => i.Id == incidentId && i.DepartmentId == departmentId.Value);
        }

        private async Task<bool> CanManageIncidentAsync(int incidentId) =>
            CanManage && await IsInDepartmentScopeAsync(incidentId);

        private async Task<bool> CanSignDepartmentAsync(int incidentId) =>
            IsAdministrator || (IsDepartmentManager && await IsInDepartmentScopeAsync(incidentId));

        private async Task LoadLookupsAsync()
        {
            var departments = _db.Departments.AsNoTracking().AsQueryable();
            if (IsDepartmentManager)
            {
                var departmentId = await CurrentDepartmentIdAsync();
                departments = departmentId.HasValue
                    ? departments.Where(d => d.Id == departmentId.Value)
                    : departments.Where(d => false);
            }

            ViewBag.Departments = await departments.OrderBy(d => d.Name).ToListAsync();
            ViewBag.Users = await _db.Users.AsNoTracking().Where(u => u.IsActive)
                .OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToListAsync();
        }

        // ── LIST ─────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Index(IncidentStatus? status, IncidentSeverity? severity, string? q)
        {
            var baseQuery = _db.Incidents.AsQueryable();
            if (IsDepartmentManager)
            {
                var departmentId = await CurrentDepartmentIdAsync();
                baseQuery = departmentId.HasValue
                    ? baseQuery.Where(i => i.DepartmentId == departmentId.Value)
                    : baseQuery.Where(i => false);
            }

            IQueryable<Incident> query = baseQuery.Include(i => i.Department).Include(i => i.CreatedBy);

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
            ViewBag.Total = await baseQuery.CountAsync();
            ViewBag.Open = await baseQuery.CountAsync(i => i.Status != IncidentStatus.Closed);
            ViewBag.UnderInvestigation = await baseQuery.CountAsync(i => i.Status == IncidentStatus.UnderInvestigation);
            ViewBag.Major = await baseQuery.CountAsync(i => i.Severity == IncidentSeverity.Major && i.Status != IncidentStatus.Closed);
            ViewBag.Closed = await baseQuery.CountAsync(i => i.Status == IncidentStatus.Closed);
            ViewBag.CanManage = CanManage;
            return View(list);
        }

        // ── DETAILS ────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Details(int id)
        {
            var incident = await LoadFullAsync(id);
            if (incident == null) return NotFound();
            if (!await IsInDepartmentScopeAsync(id)) return Denied();
            ViewBag.CanManage = await CanManageIncidentAsync(id);
            ViewBag.CanSignDepartment = await CanSignDepartmentAsync(id);
            ViewBag.CanSignQuality = CanSignQuality;
            ViewBag.CanSignGeneral = CanSignGeneral;
            ViewBag.Users = await _db.Users.AsNoTracking().Where(u => u.IsActive)
                .OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToListAsync();
            return View(incident);
        }

        // Standalone print/export view — reproduces the paper form for PDF/printing.
        public async Task<IActionResult> Print(int id)
        {
            var incident = await LoadFullAsync(id);
            if (incident == null) return NotFound();
            if (!await IsInDepartmentScopeAsync(id)) return Denied();
            return View(incident);
        }

        private Task<Incident?> LoadFullAsync(int id) => _db.Incidents
            .Include(i => i.Department)
            .Include(i => i.CreatedBy)
            .Include(i => i.Capa)
            .Include(i => i.DeptManagerSignedBy)
            .Include(i => i.QaSignedBy)
            .Include(i => i.GmSignedBy)
            .Include(i => i.Investigators)
            .Include(i => i.Damages)
            .Include(i => i.Actions)
            .Include(i => i.Attachments).ThenInclude(a => a.UploadedBy)
            .FirstOrDefaultAsync(i => i.Id == id);

        // ── CREATE ───────────────────────────────────────────────────────────────
        public async Task<IActionResult> Create()
        {
            if (!CanManage) return Denied();
            await LoadLookupsAsync();
            var model = new Incident
            {
                DateReported = DateTime.Today,
                ReportedByName = HttpContext.Session.GetString("UserName")
            };
            if (IsDepartmentManager)
                model.DepartmentId = await CurrentDepartmentIdAsync();
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(long.MaxValue)]
        public async Task<IActionResult> Create(Incident model, IFormFile? policeReport, List<IFormFile>? attachments)
        {
            if (!CanManage) return Denied();

            if (IsDepartmentManager)
            {
                var departmentId = await CurrentDepartmentIdAsync();
                if (!departmentId.HasValue) return Denied();
                model.DepartmentId = departmentId.Value;
            }

            // Workflow and sign-off state is always established by server-controlled actions.
            model.Status = IncidentStatus.Reported;
            model.ClosedAt = null;
            model.CapaId = null;
            model.DeptManagerComments = null;
            model.DeptManagerCommentDate = null;
            model.DeptManagerSignedById = null;
            model.QaComments = null;
            model.QaCommentDate = null;
            model.QaSignedById = null;
            model.GmComments = null;
            model.GmCommentDate = null;
            model.GmSignedById = null;

            // Police report is mandatory when the incident was reported to the police.
            if (model.ReportedToPolice == true && (policeReport == null || policeReport.Length == 0))
                ModelState.AddModelError(nameof(model.ReportedToPolice),
                    "A police report file must be uploaded when the incident was reported to the police.");

            var files = CollectFiles(policeReport, attachments);
            var vettedFiles = new List<(IFormFile File, IncidentAttachmentKind Kind, byte[] Content)>();
            foreach (var f in files)
            {
                var vetted = await VetFileAsync(f.file);
                if (vetted.Error != null) ModelState.AddModelError("", vetted.Error);
                else vettedFiles.Add((f.file, f.kind, vetted.Content!));
            }

            var categoryFiles = CollectCategoryFiles();
            var vettedCategoryFiles = new List<(IFormFile File, string Category, byte[] Content)>();
            foreach (var f in categoryFiles)
            {
                var vetted = await VetFileAsync(f.file);
                if (vetted.Error != null) ModelState.AddModelError("", vetted.Error);
                else vettedCategoryFiles.Add((f.file, f.category, vetted.Content!));
            }

            if (!ModelState.IsValid) { await LoadLookupsAsync(); return View(model); }

            var year = (model.DateOfIncident ?? DateTime.Now).Year;
            model.Year = year;
            model.CreatedById = Uid;
            model.CreatedAt = DateTime.Now;

            // Serializable allocation plus the unique database index guarantees one reference per year.
            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                var lastNo = await _db.Incidents.Where(i => i.Year == year)
                    .MaxAsync(i => (int?)i.IncidentNo) ?? 0;
                model.IncidentNo = lastNo + 1;
                _db.Incidents.Add(model);
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
            });

            foreach (var f in vettedFiles)
                _db.IncidentAttachments.Add(await StoreAsync(model.Id, f.File, f.Content, f.Kind, null));
            foreach (var f in vettedCategoryFiles)
                _db.IncidentAttachments.Add(await StoreAsync(model.Id, f.File, f.Content,
                    IncidentAttachmentKind.Supporting, null, f.Category));
            if (vettedFiles.Count > 0 || vettedCategoryFiles.Count > 0) await _db.SaveChangesAsync();

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
            if (!await CanManageIncidentAsync(id)) return Denied();
            var incident = await _db.Incidents.Include(i => i.Attachments).FirstOrDefaultAsync(i => i.Id == id);
            if (incident == null) return NotFound();
            await LoadLookupsAsync();
            return View(incident);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(long.MaxValue)]
        public async Task<IActionResult> Edit(int id, Incident model, IFormFile? policeReport)
        {
            if (!await CanManageIncidentAsync(id)) return Denied();
            var i = await _db.Incidents.FindAsync(id);
            if (i == null) return NotFound();
            if (IsDepartmentManager) model.DepartmentId = i.DepartmentId;

            // Police report mandatory when reported to police — unless one is already on file or is being uploaded now.
            var hasPoliceReport = await _db.IncidentAttachments
                .AnyAsync(a => a.IncidentId == id && a.Kind == IncidentAttachmentKind.PoliceReport);
            if (model.ReportedToPolice == true && !hasPoliceReport && (policeReport == null || policeReport.Length == 0))
                ModelState.AddModelError(nameof(model.ReportedToPolice),
                    "A police report file must be uploaded when the incident was reported to the police.");
            byte[]? policeReportContent = null;
            if (policeReport != null && policeReport.Length > 0)
            {
                var vetted = await VetFileAsync(policeReport);
                if (vetted.Error != null) ModelState.AddModelError("", vetted.Error);
                else policeReportContent = vetted.Content;
            }

            var categoryFiles = CollectCategoryFiles();
            var vettedCategoryFiles = new List<(IFormFile File, string Category, byte[] Content)>();
            foreach (var f in categoryFiles)
            {
                var vetted = await VetFileAsync(f.file);
                if (vetted.Error != null) ModelState.AddModelError("", vetted.Error);
                else vettedCategoryFiles.Add((f.file, f.category, vetted.Content!));
            }

            if (!ModelState.IsValid)
            {
                model.Id = i.Id;
                model.Year = i.Year;
                model.IncidentNo = i.IncidentNo;
                model.Attachments = await _db.IncidentAttachments.AsNoTracking()
                    .Where(a => a.IncidentId == id).ToListAsync();
                await LoadLookupsAsync();
                return View(model);
            }

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
            if (policeReport != null && policeReportContent != null)
                _db.IncidentAttachments.Add(await StoreAsync(id, policeReport, policeReportContent,
                    IncidentAttachmentKind.PoliceReport, "Police report"));
            foreach (var f in vettedCategoryFiles)
                _db.IncidentAttachments.Add(await StoreAsync(id, f.File, f.Content,
                    IncidentAttachmentKind.Supporting, null, f.Category));

            await _db.SaveChangesAsync();
            await _audit.LogAsync("Updated", "Incident", i.Id, $"{i.Reference} — {i.Title}");
            TempData["Success"] = "Incident updated.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── STATUS ───────────────────────────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatus(int id, IncidentStatus status)
        {
            if (!await CanManageIncidentAsync(id)) return Denied();
            if (!Enum.IsDefined(status)) return BadRequest();
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
        public async Task<IActionResult> SignDepartment(int id, string comments)
        {
            if (!await CanSignDepartmentAsync(id) || Uid == null) return Denied();
            if (string.IsNullOrWhiteSpace(comments))
            { TempData["Error"] = "Department sign-off comments are required."; return RedirectToAction(nameof(Details), new { id }); }

            var incident = await _db.Incidents.FindAsync(id);
            if (incident == null) return NotFound();
            incident.DeptManagerComments = comments.Trim();
            incident.DeptManagerCommentDate = DateTime.Today;
            incident.DeptManagerSignedById = Uid.Value;
            await _db.SaveChangesAsync();
            await _audit.LogAsync("DepartmentSignOff", "Incident", id, $"{incident.Reference} department sign-off recorded.");
            TempData["Success"] = "Department manager sign-off recorded.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SignQuality(int id, string comments)
        {
            if (!CanSignQuality || Uid == null) return Denied();
            if (string.IsNullOrWhiteSpace(comments))
            { TempData["Error"] = "Quality sign-off comments are required."; return RedirectToAction(nameof(Details), new { id }); }

            var incident = await _db.Incidents.FindAsync(id);
            if (incident == null) return NotFound();
            incident.QaComments = comments.Trim();
            incident.QaCommentDate = DateTime.Today;
            incident.QaSignedById = Uid.Value;
            await _db.SaveChangesAsync();
            await _audit.LogAsync("QualitySignOff", "Incident", id, $"{incident.Reference} quality sign-off recorded.");
            TempData["Success"] = "Quality assurance sign-off recorded.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SignGeneral(int id, string comments)
        {
            if (!CanSignGeneral || Uid == null) return Denied();
            if (string.IsNullOrWhiteSpace(comments))
            { TempData["Error"] = "General manager sign-off comments are required."; return RedirectToAction(nameof(Details), new { id }); }

            var incident = await _db.Incidents.FindAsync(id);
            if (incident == null) return NotFound();
            if (incident.Severity != IncidentSeverity.Major)
            { TempData["Error"] = "General manager sign-off applies only to major incidents."; return RedirectToAction(nameof(Details), new { id }); }

            incident.GmComments = comments.Trim();
            incident.GmCommentDate = DateTime.Today;
            incident.GmSignedById = Uid.Value;
            await _db.SaveChangesAsync();
            await _audit.LogAsync("GeneralManagerSignOff", "Incident", id, $"{incident.Reference} general manager sign-off recorded.");
            TempData["Success"] = "General manager sign-off recorded.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Confirmation page — deleting is destructive, so it gets the same explicit
        // acknowledgement step as tickets rather than a browser confirm() popup.
        public async Task<IActionResult> Delete(int id)
        {
            if (!await CanManageIncidentAsync(id)) return Denied();
            var incident = await _db.Incidents.Include(x => x.Department)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (incident == null) return NotFound();

            var vm = new ViewModels.DeleteConfirmationVm
            {
                EntityName = "Incident",
                Icon = "fa-triangle-exclamation",
                RecordTitle = incident.Title,
                Reference = incident.Reference,
                Controller = "Incidents",
                Id = incident.Id
            };
            vm.Add("Severity", incident.Severity.ToString());
            vm.Add("Status", incident.Status.ToString());
            vm.Add("Date of Incident", incident.DateOfIncident?.ToString("dd MMM yyyy"));
            vm.Add("Department", incident.Department?.Name);
            vm.Add("Location", incident.LocationOfIncident);
            vm.Consequences.Add("The full investigation report — description, evidence, critical factors and root-cause analysis — will be removed.");
            vm.Consequences.Add("Its investigation team, damage lines, remedial actions and uploaded files will be deleted with it.");
            vm.Consequences.Add("It will no longer appear in incident reporting or ISO improvement evidence.");
            return View("DeleteConfirm", vm);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!await CanManageIncidentAsync(id)) return Denied();
            var i = await _db.Incidents.Include(x => x.Attachments).FirstOrDefaultAsync(x => x.Id == id);
            if (i == null) return NotFound();
            var reference = i.Reference;
            var storedKeys = i.Attachments.Where(a => !string.IsNullOrEmpty(a.StoredFileName))
                .Select(a => a.StoredFileName).ToList();
            _db.Incidents.Remove(i);   // children cascade
            await _db.SaveChangesAsync();
            var cleanupFailed = false;
            foreach (var key in storedKeys)
                cleanupFailed |= !await _docs.DeleteFileAsync(key);
            await _audit.LogAsync("Deleted", "Incident", id, $"{reference} deleted.");
            TempData["Success"] = $"Incident {reference} deleted.";
            if (cleanupFailed) TempData["Error"] = "The incident was deleted, but one or more stored files could not be removed. Administrators have been notified in the logs.";
            return RedirectToAction(nameof(Index));
        }

        // ── Section C — investigation team ─────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddInvestigator(int id, IncidentInvestigator investigator)
        {
            if (!await CanManageIncidentAsync(id)) return Denied();
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
            if (!await CanManageIncidentAsync(id)) return Denied();
            var m = await _db.IncidentInvestigators.FirstOrDefaultAsync(x => x.Id == memberId && x.IncidentId == id);
            if (m != null) { _db.IncidentInvestigators.Remove(m); await _db.SaveChangesAsync(); }
            TempData["Success"] = "Team member removed.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── Section G — damage ─────────────────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddDamage(int id, IncidentDamage damage)
        {
            if (!await CanManageIncidentAsync(id)) return Denied();
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
            if (!await CanManageIncidentAsync(id)) return Denied();
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
            if (!await CanManageIncidentAsync(id)) return Denied();
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
            if (!await CanManageIncidentAsync(id)) return Denied();
            if (!Enum.IsDefined(status)) return BadRequest();
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
            if (!await CanManageIncidentAsync(id)) return Denied();
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
            if (!await CanManageIncidentAsync(id)) return Denied();
            if (!Enum.IsDefined(kind)) return BadRequest();
            if (!await _db.Incidents.AnyAsync(i => i.Id == id)) return NotFound();

            var valid = (files ?? new()).Where(f => f != null && f.Length > 0).ToList();
            if (valid.Count == 0)
            { TempData["Error"] = "Please choose at least one file."; return RedirectToAction(nameof(Details), new { id }); }

            var skipped = new List<string>();
            int saved = 0;
            foreach (var f in valid)
            {
                var vetted = await VetFileAsync(f);
                if (vetted.Error != null) { skipped.Add(vetted.Error); continue; }
                _db.IncidentAttachments.Add(await StoreAsync(id, f, vetted.Content!, kind, description));
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
            if (!await IsInDepartmentScopeAsync(id)) return Denied();
            var a = await _db.IncidentAttachments.FirstOrDefaultAsync(x => x.Id == attachmentId && x.IncidentId == id);
            if (a == null || string.IsNullOrEmpty(a.StoredFileName)) return NotFound();
            var stream = await _docs.OpenFileAsync(a.StoredFileName);
            return File(stream, a.ContentType ?? "application/octet-stream", a.OriginalFileName);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveAttachment(int id, int attachmentId)
        {
            if (!await CanManageIncidentAsync(id)) return Denied();
            var a = await _db.IncidentAttachments.FirstOrDefaultAsync(x => x.Id == attachmentId && x.IncidentId == id);
            if (a == null) return NotFound();
            if (!string.IsNullOrEmpty(a.StoredFileName) && !await _docs.DeleteFileAsync(a.StoredFileName))
            {
                TempData["Error"] = "The stored file could not be removed; its record was retained so an administrator can retry.";
                return RedirectToAction(nameof(Details), new { id });
            }
            _db.IncidentAttachments.Remove(a);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("AttachmentRemoved", "Incident", id, $"Removed attachment '{a.OriginalFileName}'.");
            TempData["Success"] = "Attachment removed.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
