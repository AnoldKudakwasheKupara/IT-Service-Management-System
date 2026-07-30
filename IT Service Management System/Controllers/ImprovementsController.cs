using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Filters;
using IT_Service_Management_System.Helpers.Ims;
using IT_Service_Management_System.Models.Ims;
using IT_Service_Management_System.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>
    /// Continuous Improvement Register (ISO 9001 cl. 10.3 / ISO 27001 cl. 10.2) — captures
    /// improvement opportunities, suggestions, kaizen events and lessons learned, tracks them
    /// from proposal through implementation, and records the realised benefit.
    /// </summary>
    [IT_Service_Management_System.Filters.RoleAuthorize("Admin", "SystemsAdmin", "QualityManager", "DocumentController", "DepartmentManager", "Auditor", "ExternalAuditor")]
    public class ImprovementsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditService _audit;

        public ImprovementsController(ApplicationDbContext db, AuditService audit)
        {
            _db = db;
            _audit = audit;
        }

        private int? Uid => HttpContext.Session.GetInt32("UserId");
        private string? Role => HttpContext.Session.GetString("UserRole");
        private bool Can(ImsPermission p) => ImsAccess.Can(Role, p);

        private IActionResult Denied() => RedirectToAction("AccessDenied", "Home");

        private void LoadLookups()
        {
            ViewBag.Departments = _db.Departments.OrderBy(d => d.Name).ToList();
            ViewBag.Users = _db.Users.Where(u => u.IsActive).OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToList();
        }

        // ── LIST ─────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Index(ImprovementStatus? status)
        {
            var query = _db.Improvements
                .Include(i => i.Department)
                .Include(i => i.ProposedBy)
                .Include(i => i.Owner)
                .AsQueryable();

            if (status.HasValue) query = query.Where(i => i.Status == status.Value);

            var list = await query.OrderByDescending(i => i.CreatedAt).ToListAsync();

            ViewBag.Status = status;
            ViewBag.Total = list.Count;
            ViewBag.Open = list.Count(i => i.Status == ImprovementStatus.Proposed || i.Status == ImprovementStatus.UnderReview || i.Status == ImprovementStatus.Approved);
            ViewBag.InProgress = list.Count(i => i.Status == ImprovementStatus.InProgress);
            ViewBag.Implemented = list.Count(i => i.Status == ImprovementStatus.Implemented);
            ViewBag.CanEdit = Can(ImsPermission.ManageImprovements);

            return View(list);
        }

        // ── DETAILS ────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Details(int id)
        {
            var imp = await _db.Improvements
                .Include(i => i.Department)
                .Include(i => i.ProposedBy)
                .Include(i => i.Owner)
                .Include(i => i.CreatedBy)
                .FirstOrDefaultAsync(i => i.Id == id);
            if (imp == null) return NotFound();

            ViewBag.CanManage = Can(ImsPermission.ManageImprovements);
            return View(imp);
        }

        // ── CREATE ───────────────────────────────────────────────────────────────
        public IActionResult Create()
        {
            if (!Can(ImsPermission.ManageImprovements)) return Denied();
            LoadLookups();
            return View(new Improvement());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Improvement model)
        {
            if (!Can(ImsPermission.ManageImprovements)) return Denied();

            if (!ModelState.IsValid)
            {
                LoadLookups();
                return View(model);
            }

            if (model.ProposedById == null) model.ProposedById = Uid;
            model.CreatedById = Uid;
            model.CreatedAt = DateTime.Now;
            _db.Improvements.Add(model);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Created", "Improvement", model.Id, $"{model.Reference} — {model.Title}");
            TempData["Success"] = $"Improvement {model.Reference} logged.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        // ── EDIT ───────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Edit(int id)
        {
            if (!Can(ImsPermission.ManageImprovements)) return Denied();
            var imp = await _db.Improvements.FindAsync(id);
            if (imp == null) return NotFound();
            LoadLookups();
            return View(imp);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Improvement model)
        {
            if (!Can(ImsPermission.ManageImprovements)) return Denied();
            var imp = await _db.Improvements.FindAsync(id);
            if (imp == null) return NotFound();

            if (!ModelState.IsValid) { LoadLookups(); return View(model); }

            imp.Title = model.Title;
            imp.Type = model.Type;
            imp.Standard = model.Standard;
            imp.Description = model.Description;
            imp.ExpectedBenefit = model.ExpectedBenefit;
            imp.ProposedById = model.ProposedById;
            imp.OwnerId = model.OwnerId;
            imp.DepartmentId = model.DepartmentId;
            imp.Status = model.Status;
            imp.TargetDate = model.TargetDate;
            imp.CompletedDate = model.CompletedDate;
            imp.ActualBenefit = model.ActualBenefit;
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Updated", "Improvement", imp.Id, $"{imp.Reference} — {imp.Title}");
            TempData["Success"] = "Improvement updated.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── DELETE ───────────────────────────────────────────────────────────────
        // Confirmation page — deleting is destructive, so it gets the same explicit
        // acknowledgement step as tickets rather than a browser confirm() popup.
        public async Task<IActionResult> Delete(int id)
        {
            if (!Can(ImsPermission.ManageImprovements)) return Denied();
            var entity = await _db.Improvements.Include(i => i.Owner).Include(i => i.Department)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return NotFound();

            var vm = new ViewModels.DeleteConfirmationVm
            {
                EntityName = "Improvement",
                Icon = "fa-lightbulb",
                RecordTitle = entity.Title,
                Reference = entity.Reference,
                Controller = "Improvements",
                Id = entity.Id
            };
            vm.Add("Type", entity.Type.ToString());
            vm.Add("Status", entity.Status.ToString());
            vm.Add("Owner", entity.Owner?.FullName);
            vm.Add("Department", entity.Department?.Name);
            vm.Add("Target Date", entity.TargetDate?.ToString("dd MMM yyyy"));
            vm.Consequences.Add("The improvement, its expected and actual benefit and implementation notes will be removed from the register.");
            vm.Consequences.Add("It will no longer appear in continuous-improvement dashboards or ISO reports.");
            return View("DeleteConfirm", vm);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!Can(ImsPermission.ManageImprovements)) return Denied();
            var imp = await _db.Improvements.FindAsync(id);
            if (imp == null) return NotFound();
            _db.Improvements.Remove(imp);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Deleted", "Improvement", id, $"{imp.Reference} deleted.");
            TempData["Success"] = "Improvement deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
