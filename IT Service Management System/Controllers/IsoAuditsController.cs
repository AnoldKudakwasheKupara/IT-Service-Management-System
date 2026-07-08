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
    /// Internal Audits (ISO 9001/27001 cl. 9.2) — audit programmes, audits with teams and checklists,
    /// findings/observations, and automatic CAPA generation from non-conformities.
    /// </summary>
    [RoleAuthorize("Admin", "SystemsAdmin", "QualityManager", "DocumentController", "DepartmentManager", "Auditor", "ExternalAuditor")]
    public class IsoAuditsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditService _audit;
        private readonly ImsNotificationService _notify;

        public IsoAuditsController(ApplicationDbContext db, AuditService audit, ImsNotificationService notify)
        {
            _db = db;
            _audit = audit;
            _notify = notify;
        }

        private int? Uid => HttpContext.Session.GetInt32("UserId");
        private string? Role => HttpContext.Session.GetString("UserRole");
        private bool Can(ImsPermission p) => ImsAccess.Can(Role, p);
        private IActionResult Denied() => RedirectToAction("AccessDenied", "Home");

        private void LoadLookups()
        {
            ViewBag.Users = _db.Users.Where(u => u.IsActive).OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToList();
            ViewBag.Departments = _db.Departments.OrderBy(d => d.Name).ToList();
            ViewBag.Programmes = _db.AuditProgrammes.OrderByDescending(p => p.Year).ToList();
        }

        // ── AUDITS ──────────────────────────────────────────────────────────────
        public async Task<IActionResult> Index(AuditStatus? status)
        {
            var query = _db.Audits.Include(a => a.LeadAuditor).Include(a => a.Department).Include(a => a.Findings).AsQueryable();
            if (status.HasValue) query = query.Where(a => a.Status == status.Value);
            var list = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();

            ViewBag.Status = status;
            ViewBag.Total = list.Count;
            ViewBag.Open = list.Count(a => a.Status != AuditStatus.Closed && a.Status != AuditStatus.Cancelled);
            ViewBag.Completed = list.Count(a => a.Status is AuditStatus.Completed or AuditStatus.Closed);
            ViewBag.OpenFindings = await _db.AuditFindings.CountAsync(f => f.Status != FindingStatus.Closed);
            ViewBag.CanEdit = Can(ImsPermission.ManageAuditProgramme) || Can(ImsPermission.ConductAudit);
            return View(list);
        }

        public async Task<IActionResult> Details(int id)
        {
            var audit = await _db.Audits
                .Include(a => a.LeadAuditor)
                .Include(a => a.Department)
                .Include(a => a.AuditProgramme)
                .Include(a => a.TeamMembers).ThenInclude(t => t.User)
                .Include(a => a.ChecklistItems)
                .Include(a => a.Findings).ThenInclude(f => f.AssignedTo)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (audit == null) return NotFound();

            ViewBag.CanEdit = Can(ImsPermission.ConductAudit) || Can(ImsPermission.ManageAuditProgramme);
            ViewBag.CanRaiseFinding = Can(ImsPermission.RaiseFinding);
            LoadLookups();
            return View(audit);
        }

        public IActionResult Create()
        {
            if (!Can(ImsPermission.ManageAuditProgramme) && !Can(ImsPermission.ConductAudit)) return Denied();
            LoadLookups();
            return View(new Audit());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Audit model)
        {
            if (!Can(ImsPermission.ManageAuditProgramme) && !Can(ImsPermission.ConductAudit)) return Denied();
            if (!ModelState.IsValid) { LoadLookups(); return View(model); }
            model.CreatedById = Uid;
            model.CreatedAt = DateTime.Now;
            _db.Audits.Add(model);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Created", "Audit", model.Id, $"{model.Reference} — {model.Title}");
            if (model.LeadAuditorId.HasValue)
                await _notify.NotifyUserAsync(model.LeadAuditorId.Value, IsoNotificationType.AuditScheduled,
                    $"Audit assigned: {model.Reference}", model.Title, $"/IsoAudits/Details/{model.Id}", "info", "Audit", model.Id);
            TempData["Success"] = $"{model.Reference} created.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        public async Task<IActionResult> Edit(int id)
        {
            if (!Can(ImsPermission.ConductAudit) && !Can(ImsPermission.ManageAuditProgramme)) return Denied();
            var audit = await _db.Audits.FindAsync(id);
            if (audit == null) return NotFound();
            LoadLookups();
            return View(audit);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Audit model)
        {
            if (!Can(ImsPermission.ConductAudit) && !Can(ImsPermission.ManageAuditProgramme)) return Denied();
            var audit = await _db.Audits.FindAsync(id);
            if (audit == null) return NotFound();
            if (!ModelState.IsValid) { LoadLookups(); return View(model); }

            audit.Title = model.Title;
            audit.Type = model.Type;
            audit.Standard = model.Standard;
            audit.Scope = model.Scope;
            audit.Objectives = model.Objectives;
            audit.Criteria = model.Criteria;
            audit.AuditProgrammeId = model.AuditProgrammeId;
            audit.DepartmentId = model.DepartmentId;
            audit.LeadAuditorId = model.LeadAuditorId;
            audit.PlannedStartDate = model.PlannedStartDate;
            audit.PlannedEndDate = model.PlannedEndDate;
            audit.ActualStartDate = model.ActualStartDate;
            audit.ActualEndDate = model.ActualEndDate;
            audit.Status = model.Status;
            audit.Summary = model.Summary;
            audit.Conclusion = model.Conclusion;
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Updated", "Audit", id, $"{audit.Reference} updated.");
            TempData["Success"] = "Audit updated.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!Can(ImsPermission.ManageConfiguration)) return Denied();
            var audit = await _db.Audits.FindAsync(id);
            if (audit == null) return NotFound();
            _db.Audits.Remove(audit);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Deleted", "Audit", id, $"{audit.Reference} deleted.");
            TempData["Success"] = "Audit deleted.";
            return RedirectToAction(nameof(Index));
        }

        // ── TEAM & CHECKLIST ─────────────────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTeamMember(int id, int userId, AuditTeamRole roleOnTeam)
        {
            if (!Can(ImsPermission.ConductAudit)) return Denied();
            if (!await _db.Audits.AnyAsync(a => a.Id == id)) return NotFound();
            _db.AuditTeamMembers.Add(new AuditTeamMember { AuditId = id, UserId = userId, RoleOnTeam = roleOnTeam });
            await _db.SaveChangesAsync();
            TempData["Success"] = "Team member added.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddChecklistItem(int id, string question, string? clauseReference)
        {
            if (!Can(ImsPermission.ConductAudit)) return Denied();
            if (!await _db.Audits.AnyAsync(a => a.Id == id)) return NotFound();
            if (string.IsNullOrWhiteSpace(question)) { TempData["Error"] = "Question is required."; return RedirectToAction(nameof(Details), new { id }); }
            var seq = await _db.AuditChecklistItems.CountAsync(c => c.AuditId == id);
            _db.AuditChecklistItems.Add(new AuditChecklistItem { AuditId = id, Question = question, ClauseReference = clauseReference, Sequence = seq + 1 });
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SetChecklistResult(int id, int itemId, ChecklistResult result, string? evidence)
        {
            if (!Can(ImsPermission.ConductAudit)) return Denied();
            var item = await _db.AuditChecklistItems.FirstOrDefaultAsync(c => c.Id == itemId && c.AuditId == id);
            if (item == null) return NotFound();
            item.Result = result;
            item.Evidence = evidence;
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── FINDINGS ─────────────────────────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddFinding(int id, AuditFinding finding)
        {
            if (!Can(ImsPermission.RaiseFinding)) return Denied();
            var audit = await _db.Audits.FindAsync(id);
            if (audit == null) return NotFound();
            finding.AuditId = id;
            finding.RaisedById = Uid;
            finding.CreatedAt = DateTime.Now;
            finding.Status = FindingStatus.Open;
            _db.AuditFindings.Add(finding);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Finding", "Audit", id, $"{finding.Reference} ({finding.Type}) raised on {audit.Reference}");
            TempData["Success"] = $"Finding {finding.Reference} raised.";
            return RedirectToAction(nameof(Details), new { id });
        }

        public async Task<IActionResult> Findings(FindingStatus? status, FindingType? type)
        {
            var query = _db.AuditFindings.Include(f => f.Audit).Include(f => f.AssignedTo).Include(f => f.Department).AsQueryable();
            if (status.HasValue) query = query.Where(f => f.Status == status.Value);
            if (type.HasValue) query = query.Where(f => f.Type == type.Value);
            var list = await query.OrderByDescending(f => f.CreatedAt).ToListAsync();
            ViewBag.Status = status;
            ViewBag.Type = type;
            ViewBag.Total = list.Count;
            ViewBag.Open = list.Count(f => f.Status != FindingStatus.Closed);
            ViewBag.Ncs = list.Count(f => f.IsNonConformance);
            ViewBag.CanRaiseCapa = Can(ImsPermission.RaiseCapa);
            return View(list);
        }

        public async Task<IActionResult> FindingDetails(int id)
        {
            var finding = await _db.AuditFindings
                .Include(f => f.Audit).Include(f => f.AssignedTo).Include(f => f.RaisedBy).Include(f => f.Department).Include(f => f.Capa)
                .FirstOrDefaultAsync(f => f.Id == id);
            if (finding == null) return NotFound();
            ViewBag.CanRaiseCapa = Can(ImsPermission.RaiseCapa);
            ViewBag.CanEdit = Can(ImsPermission.RaiseFinding);
            return View(finding);
        }

        /// <summary>Automatically generates a corrective-action (CAPA) record from a finding and links them.</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateCapa(int id)
        {
            if (!Can(ImsPermission.RaiseCapa)) return Denied();
            var finding = await _db.AuditFindings.Include(f => f.Audit).FirstOrDefaultAsync(f => f.Id == id);
            if (finding == null) return NotFound();
            if (finding.CapaId.HasValue) { TempData["Info"] = "A CAPA already exists for this finding."; return RedirectToAction(nameof(FindingDetails), new { id }); }

            var capa = new Capa
            {
                Title = $"Corrective action for {finding.Reference}",
                Type = CapaType.Corrective,
                Source = finding.Audit?.Type == AuditType.External ? CapaSource.ExternalAudit : CapaSource.InternalAudit,
                SourceReference = finding.Reference,
                Standard = finding.Audit?.Standard ?? IsoStandard.Both,
                Description = finding.Description,
                RootCause = finding.Evidence,
                DepartmentId = finding.DepartmentId,
                ResponsibleId = finding.AssignedToId,
                DueDate = finding.DueDate,
                Status = CapaStatus.Open,
                CreatedById = Uid,
                CreatedAt = DateTime.Now
            };
            _db.Capas.Add(capa);
            await _db.SaveChangesAsync();

            finding.CapaId = capa.Id;
            finding.Status = FindingStatus.CapaRaised;
            await _db.SaveChangesAsync();

            await _audit.LogAsync("CapaGenerated", "AuditFinding", finding.Id, $"{capa.Reference} generated from {finding.Reference}");
            if (capa.ResponsibleId.HasValue)
                await _notify.NotifyUserAsync(capa.ResponsibleId.Value, IsoNotificationType.CapaAssigned,
                    $"CAPA assigned: {capa.Reference}", capa.Title, $"/Capa/Details/{capa.Id}", "warning", "Capa", capa.Id);

            TempData["Success"] = $"{capa.Reference} generated from {finding.Reference}.";
            return RedirectToAction("Details", "Capa", new { id = capa.Id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CloseFinding(int id)
        {
            if (!Can(ImsPermission.RaiseFinding)) return Denied();
            var finding = await _db.AuditFindings.FindAsync(id);
            if (finding == null) return NotFound();
            finding.Status = FindingStatus.Closed;
            finding.ClosedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Closed", "AuditFinding", id, $"{finding.Reference} closed.");
            TempData["Success"] = "Finding closed.";
            return RedirectToAction(nameof(FindingDetails), new { id });
        }

        // ── PROGRAMMES ───────────────────────────────────────────────────────────
        public async Task<IActionResult> Programmes()
        {
            var list = await _db.AuditProgrammes.Include(p => p.Audits).OrderByDescending(p => p.Year).ToListAsync();
            ViewBag.CanEdit = Can(ImsPermission.ManageAuditProgramme);
            return View(list);
        }

        public IActionResult CreateProgramme()
        {
            if (!Can(ImsPermission.ManageAuditProgramme)) return Denied();
            return View(new AuditProgramme());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProgramme(AuditProgramme model)
        {
            if (!Can(ImsPermission.ManageAuditProgramme)) return Denied();
            if (!ModelState.IsValid) return View(model);
            model.CreatedById = Uid;
            model.CreatedAt = DateTime.Now;
            _db.AuditProgrammes.Add(model);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Created", "AuditProgramme", model.Id, $"{model.Year} — {model.Title}");
            TempData["Success"] = "Audit programme created.";
            return RedirectToAction(nameof(Programmes));
        }
    }
}
