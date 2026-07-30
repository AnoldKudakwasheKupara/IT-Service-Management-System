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
    /// Objectives &amp; KPIs (ISO 9001 cl. 6.2 / ISO 27001 cl. 6.2) — the register of measurable quality and
    /// security objectives, each with a target, KPI direction and periodic measurements that drive the
    /// current value and progress toward target.
    /// </summary>
    [IT_Service_Management_System.Filters.RoleAuthorize("Admin", "SystemsAdmin", "QualityManager", "DocumentController", "DepartmentManager", "Auditor", "ExternalAuditor")]
    public class ObjectivesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditService _audit;

        public ObjectivesController(ApplicationDbContext db, AuditService audit)
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
        public async Task<IActionResult> Index(ObjectiveStatus? status, int? departmentId, string? q)
        {
            var query = _db.Objectives
                .Include(o => o.Department)
                .Include(o => o.Owner)
                .AsQueryable();

            if (status.HasValue) query = query.Where(o => o.Status == status.Value);
            if (departmentId.HasValue) query = query.Where(o => o.DepartmentId == departmentId.Value);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(o => o.Title.Contains(term) || (o.Description != null && o.Description.Contains(term)));
            }

            var list = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();

            ViewBag.Status = status;
            ViewBag.DepartmentId = departmentId;
            ViewBag.Query = q;
            ViewBag.Total = list.Count;
            ViewBag.Active = list.Count(o => o.Status is ObjectiveStatus.Active or ObjectiveStatus.OnTrack or ObjectiveStatus.AtRisk);
            ViewBag.AtRisk = list.Count(o => o.Status == ObjectiveStatus.AtRisk);
            ViewBag.Achieved = list.Count(o => o.Status == ObjectiveStatus.Achieved);
            ViewBag.Departments = _db.Departments.OrderBy(d => d.Name).ToList();
            ViewBag.CanEdit = Can(ImsPermission.ManageObjectives);

            return View(list);
        }

        // ── DETAILS ────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Details(int id)
        {
            var obj = await _db.Objectives
                .Include(o => o.Department)
                .Include(o => o.Owner)
                .Include(o => o.CreatedBy)
                .Include(o => o.Measurements).ThenInclude(m => m.RecordedBy)
                .FirstOrDefaultAsync(o => o.Id == id);
            if (obj == null) return NotFound();

            ViewBag.CanManage = Can(ImsPermission.ManageObjectives);
            LoadLookups();
            return View(obj);
        }

        // ── CREATE ───────────────────────────────────────────────────────────────
        public IActionResult Create()
        {
            if (!Can(ImsPermission.ManageObjectives)) return Denied();
            LoadLookups();
            return View(new Objective { StartDate = DateTime.Now });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Objective model)
        {
            if (!Can(ImsPermission.ManageObjectives)) return Denied();

            if (!ModelState.IsValid)
            {
                LoadLookups();
                return View(model);
            }

            model.CreatedById = Uid;
            model.CreatedAt = DateTime.Now;
            _db.Objectives.Add(model);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Created", "Objective", model.Id, $"{model.Reference} — {model.Title}");
            TempData["Success"] = $"Objective {model.Reference} created.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        // ── EDIT ───────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Edit(int id)
        {
            if (!Can(ImsPermission.ManageObjectives)) return Denied();
            var obj = await _db.Objectives.FindAsync(id);
            if (obj == null) return NotFound();
            LoadLookups();
            return View(obj);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Objective model)
        {
            if (!Can(ImsPermission.ManageObjectives)) return Denied();
            var obj = await _db.Objectives.FindAsync(id);
            if (obj == null) return NotFound();

            if (!ModelState.IsValid) { LoadLookups(); return View(model); }

            obj.Title = model.Title;
            obj.Description = model.Description;
            obj.Standard = model.Standard;
            obj.DepartmentId = model.DepartmentId;
            obj.OwnerId = model.OwnerId;
            obj.TargetValue = model.TargetValue;
            obj.Unit = model.Unit;
            obj.BaselineValue = model.BaselineValue;
            obj.CurrentValue = model.CurrentValue;
            obj.Direction = model.Direction;
            obj.Frequency = model.Frequency;
            obj.StartDate = model.StartDate;
            obj.DueDate = model.DueDate;
            obj.Status = model.Status;
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Updated", "Objective", obj.Id, $"{obj.Reference} — {obj.Title}");
            TempData["Success"] = "Objective updated.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── RECORD MEASUREMENT ─────────────────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordMeasurement(int id, ObjectiveMeasurement m)
        {
            if (!Can(ImsPermission.ManageObjectives)) return Denied();
            var obj = await _db.Objectives.FindAsync(id);
            if (obj == null) return NotFound();

            if (string.IsNullOrWhiteSpace(m.PeriodLabel))
            {
                TempData["Error"] = "A period label is required to record a measurement.";
                return RedirectToAction(nameof(Details), new { id });
            }

            m.Id = 0;
            m.ObjectiveId = id;
            m.RecordedById = Uid;
            if (m.RecordedDate == default) m.RecordedDate = DateTime.Now;
            _db.ObjectiveMeasurements.Add(m);

            obj.CurrentValue = m.Value;
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Measured", "Objective", id, $"{obj.Reference}: {m.PeriodLabel} = {m.Value}");
            TempData["Success"] = $"Measurement recorded for {m.PeriodLabel}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── DELETE ─────────────────────────────────────────────────────────────────
        // Confirmation page — deleting is destructive, so it gets the same explicit
        // acknowledgement step as tickets rather than a browser confirm() popup.
        public async Task<IActionResult> Delete(int id)
        {
            if (!Can(ImsPermission.ManageObjectives)) return Denied();
            var entity = await _db.Objectives.Include(o => o.Owner).Include(o => o.Department)
                .FirstOrDefaultAsync(o => o.Id == id);
            if (entity == null) return NotFound();

            var vm = new ViewModels.DeleteConfirmationVm
            {
                EntityName = "Objective",
                Icon = "fa-bullseye",
                RecordTitle = entity.Title,
                Reference = entity.Reference,
                Controller = "Objectives",
                Id = entity.Id
            };
            vm.Add("Status", entity.Status.ToString());
            vm.Add("Owner", entity.Owner?.FullName);
            vm.Add("Department", entity.Department?.Name);
            vm.Add("Target", entity.TargetValue.HasValue ? $"{entity.TargetValue} {entity.Unit}".Trim() : null);
            vm.Add("Due Date", entity.DueDate?.ToString("dd MMM yyyy"));
            vm.Consequences.Add("The objective, its KPI target and progress will be removed from the register.");
            vm.Consequences.Add("Every recorded measurement for this objective is deleted with it.");
            vm.Consequences.Add("It will no longer appear in objective dashboards or ISO reports.");
            return View("DeleteConfirm", vm);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!Can(ImsPermission.ManageObjectives)) return Denied();
            var obj = await _db.Objectives.Include(o => o.Measurements).FirstOrDefaultAsync(o => o.Id == id);
            if (obj == null) return NotFound();
            _db.ObjectiveMeasurements.RemoveRange(obj.Measurements);
            _db.Objectives.Remove(obj);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Deleted", "Objective", id, $"{obj.Reference} deleted.");
            TempData["Success"] = "Objective deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
