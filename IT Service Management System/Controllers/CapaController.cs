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
    /// Corrective &amp; Preventive Actions (ISO 9001/27001 cl. 10.2). Covers the CAPA and Preventive-Action
    /// modules — the register, the investigate → plan → verify → close lifecycle, effectiveness review
    /// and escalation with notifications.
    /// </summary>
    [RoleAuthorize("Admin", "SystemsAdmin", "QualityManager", "DocumentController", "DepartmentManager", "Auditor", "ExternalAuditor")]
    public class CapaController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditService _audit;
        private readonly ImsNotificationService _notify;

        public CapaController(ApplicationDbContext db, AuditService audit, ImsNotificationService notify)
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
            ViewBag.NonConformances = _db.NonConformances.OrderByDescending(n => n.Id).ToList();
        }

        public async Task<IActionResult> Index(CapaType? type, CapaStatus? status)
        {
            var query = _db.Capas.Include(c => c.Responsible).Include(c => c.Department).AsQueryable();
            if (type.HasValue) query = query.Where(c => c.Type == type.Value);
            if (status.HasValue) query = query.Where(c => c.Status == status.Value);

            var list = await query.OrderByDescending(c => c.CreatedAt).ToListAsync();

            ViewBag.Type = type;
            ViewBag.Status = status;
            ViewBag.Total = list.Count;
            ViewBag.Open = list.Count(c => !c.IsClosed);
            ViewBag.Overdue = list.Count(c => c.IsOverdue);
            ViewBag.Verified = list.Count(c => c.Status == CapaStatus.Verified);
            ViewBag.Closed = list.Count(c => c.Status == CapaStatus.Closed);
            ViewBag.CanCreate = Can(ImsPermission.RaiseCapa);
            return View(list);
        }

        public async Task<IActionResult> Details(int id)
        {
            var capa = await _db.Capas
                .Include(c => c.Responsible)
                .Include(c => c.Department)
                .Include(c => c.CreatedBy)
                .Include(c => c.VerifiedBy)
                .Include(c => c.NonConformance)
                .Include(c => c.Findings)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (capa == null) return NotFound();

            ViewBag.CanEdit = Can(ImsPermission.AssignCapa) || Can(ImsPermission.InvestigateCapa);
            ViewBag.CanVerify = Can(ImsPermission.VerifyCapa);
            ViewBag.CanClose = Can(ImsPermission.CloseCapa);
            return View(capa);
        }

        public IActionResult Create(CapaType type = CapaType.Corrective)
        {
            if (!Can(ImsPermission.RaiseCapa)) return Denied();
            LoadLookups();
            return View(new Capa { Type = type });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Capa model)
        {
            if (!Can(ImsPermission.RaiseCapa)) return Denied();
            if (!ModelState.IsValid) { LoadLookups(); return View(model); }

            model.CreatedById = Uid;
            model.CreatedAt = DateTime.Now;
            model.Status = CapaStatus.Open;
            _db.Capas.Add(model);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Created", "Capa", model.Id, $"{model.Reference} — {model.Title}");
            if (model.ResponsibleId.HasValue)
                await _notify.NotifyUserAsync(model.ResponsibleId.Value, IsoNotificationType.CapaAssigned,
                    $"CAPA assigned: {model.Reference}", model.Title, $"/Capa/Details/{model.Id}", "warning", "Capa", model.Id);

            TempData["Success"] = $"{model.Reference} raised.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        public async Task<IActionResult> Edit(int id)
        {
            if (!Can(ImsPermission.AssignCapa) && !Can(ImsPermission.InvestigateCapa)) return Denied();
            var capa = await _db.Capas.FindAsync(id);
            if (capa == null) return NotFound();
            LoadLookups();
            return View(capa);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Capa model)
        {
            if (!Can(ImsPermission.AssignCapa) && !Can(ImsPermission.InvestigateCapa)) return Denied();
            var capa = await _db.Capas.FindAsync(id);
            if (capa == null) return NotFound();
            if (!ModelState.IsValid) { LoadLookups(); return View(model); }

            var previousResponsible = capa.ResponsibleId;

            capa.Title = model.Title;
            capa.Type = model.Type;
            capa.Source = model.Source;
            capa.SourceReference = model.SourceReference;
            capa.Standard = model.Standard;
            capa.Description = model.Description;
            capa.Containment = model.Containment;
            capa.Correction = model.Correction;
            capa.RootCause = model.RootCause;
            capa.CorrectiveAction = model.CorrectiveAction;
            capa.PreventiveAction = model.PreventiveAction;
            capa.ResponsibleId = model.ResponsibleId;
            capa.DepartmentId = model.DepartmentId;
            capa.DueDate = model.DueDate;
            capa.Status = model.Status;
            capa.EffectivenessReview = model.EffectivenessReview;
            capa.EffectivenessReviewDate = model.EffectivenessReviewDate;
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Updated", "Capa", capa.Id, $"{capa.Reference} updated.");
            if (capa.ResponsibleId.HasValue && capa.ResponsibleId != previousResponsible)
                await _notify.NotifyUserAsync(capa.ResponsibleId.Value, IsoNotificationType.CapaAssigned,
                    $"CAPA assigned: {capa.Reference}", capa.Title, $"/Capa/Details/{capa.Id}", "warning", "Capa", capa.Id);

            TempData["Success"] = "CAPA updated.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Verify(int id, string? verificationNotes)
        {
            if (!Can(ImsPermission.VerifyCapa)) return Denied();
            var capa = await _db.Capas.FindAsync(id);
            if (capa == null) return NotFound();
            capa.VerificationNotes = verificationNotes;
            capa.VerifiedById = Uid;
            capa.VerifiedAt = DateTime.Now;
            capa.Status = CapaStatus.Verified;
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Verified", "Capa", id, $"{capa.Reference} verified effective.");
            TempData["Success"] = "CAPA verified.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Close(int id)
        {
            if (!Can(ImsPermission.CloseCapa)) return Denied();
            var capa = await _db.Capas.FindAsync(id);
            if (capa == null) return NotFound();
            capa.Status = CapaStatus.Closed;
            capa.ClosedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Closed", "Capa", id, $"{capa.Reference} closed.");
            TempData["Success"] = "CAPA closed.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Escalate(int id)
        {
            if (!Can(ImsPermission.AssignCapa)) return Denied();
            var capa = await _db.Capas.FindAsync(id);
            if (capa == null) return NotFound();
            capa.Escalated = true;
            capa.EscalatedAt = DateTime.Now;
            capa.Status = CapaStatus.Escalated;
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Escalated", "Capa", id, $"{capa.Reference} escalated.");
            await _notify.NotifyManagersAsync(IsoNotificationType.CapaEscalated,
                $"CAPA escalated: {capa.Reference}", capa.Title, $"/Capa/Details/{capa.Id}", "error", "Capa", capa.Id);
            TempData["Warning"] = "CAPA escalated to management.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!Can(ImsPermission.ManageConfiguration)) return Denied();
            var capa = await _db.Capas.FindAsync(id);
            if (capa == null) return NotFound();
            _db.Capas.Remove(capa);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Deleted", "Capa", id, $"{capa.Reference} deleted.");
            TempData["Success"] = "CAPA deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
