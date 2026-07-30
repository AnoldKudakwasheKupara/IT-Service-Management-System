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
    /// Non-Conformance Register (ISO 9001 cl. 10.2 / ISO 27001 cl. 10.1) — records detected
    /// non-conformities, their severity and source, assigns investigation, captures the root
    /// cause and drives them through to closure. May be linked onward to a CAPA for resolution.
    /// </summary>
    [IT_Service_Management_System.Filters.RoleAuthorize("Admin", "SystemsAdmin", "QualityManager", "DocumentController", "DepartmentManager", "Auditor", "ExternalAuditor")]
    public class NonConformancesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditService _audit;

        public NonConformancesController(ApplicationDbContext db, AuditService audit)
        {
            _db = db;
            _audit = audit;
        }

        private int? Uid => HttpContext.Session.GetInt32("UserId");
        private string? Role => HttpContext.Session.GetString("UserRole");
        private bool Can(ImsPermission p) => ImsAccess.Can(Role, p);

        private IActionResult Denied() => RedirectToAction("AccessDenied", "Home");

        /// <summary>Investigation / edit / close is allowed for CAPA investigators, or, failing that, anyone who may raise NCs.</summary>
        private bool CanInvestigate() => Can(ImsPermission.InvestigateCapa) || Can(ImsPermission.RaiseNonConformance);

        private void LoadLookups()
        {
            ViewBag.Departments = _db.Departments.OrderBy(d => d.Name).ToList();
            ViewBag.Users = _db.Users.Where(u => u.IsActive).OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToList();
        }

        // ── LIST ─────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Index(NcStatus? status)
        {
            var query = _db.NonConformances
                .Include(n => n.Department)
                .Include(n => n.RaisedBy)
                .Include(n => n.AssignedTo)
                .AsQueryable();

            if (status.HasValue) query = query.Where(n => n.Status == status.Value);

            var list = await query.OrderByDescending(n => n.CreatedAt).ToListAsync();

            ViewBag.Status = status;
            ViewBag.Total = list.Count;
            ViewBag.Open = list.Count(n => n.Status == NcStatus.Open);
            ViewBag.UnderInvestigation = list.Count(n => n.Status == NcStatus.UnderInvestigation);
            ViewBag.Closed = list.Count(n => n.Status == NcStatus.Closed);
            ViewBag.CanEdit = Can(ImsPermission.RaiseNonConformance);

            return View(list);
        }

        // ── DETAILS ────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Details(int id)
        {
            var nc = await _db.NonConformances
                .Include(n => n.Department)
                .Include(n => n.RaisedBy)
                .Include(n => n.AssignedTo)
                .Include(n => n.CreatedBy)
                .Include(n => n.Capas)
                .FirstOrDefaultAsync(n => n.Id == id);
            if (nc == null) return NotFound();

            ViewBag.CanManage = CanInvestigate();
            ViewBag.CanClose = CanInvestigate();
            return View(nc);
        }

        // ── CREATE ───────────────────────────────────────────────────────────────
        public IActionResult Create()
        {
            if (!Can(ImsPermission.RaiseNonConformance)) return Denied();
            LoadLookups();
            return View(new NonConformance());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(NonConformance model)
        {
            if (!Can(ImsPermission.RaiseNonConformance)) return Denied();

            if (!ModelState.IsValid)
            {
                LoadLookups();
                return View(model);
            }

            if (model.RaisedById == null) model.RaisedById = Uid;
            model.CreatedById = Uid;
            model.CreatedAt = DateTime.Now;
            _db.NonConformances.Add(model);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Created", "NonConformance", model.Id, $"{model.Reference} — {model.Title}");
            TempData["Success"] = $"Non-conformance {model.Reference} raised.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        // ── EDIT ───────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Edit(int id)
        {
            if (!CanInvestigate()) return Denied();
            var nc = await _db.NonConformances.FindAsync(id);
            if (nc == null) return NotFound();
            LoadLookups();
            return View(nc);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, NonConformance model)
        {
            if (!CanInvestigate()) return Denied();
            var nc = await _db.NonConformances.FindAsync(id);
            if (nc == null) return NotFound();

            if (!ModelState.IsValid) { LoadLookups(); return View(model); }

            nc.Title = model.Title;
            nc.Description = model.Description;
            nc.Severity = model.Severity;
            nc.Source = model.Source;
            nc.Standard = model.Standard;
            nc.DepartmentId = model.DepartmentId;
            nc.RaisedById = model.RaisedById;
            nc.AssignedToId = model.AssignedToId;
            nc.DetectedDate = model.DetectedDate;
            nc.RootCause = model.RootCause;
            nc.Evidence = model.Evidence;
            nc.Status = model.Status;
            if (model.Status == NcStatus.Closed && nc.ClosedAt == null) nc.ClosedAt = DateTime.Now;
            if (model.Status != NcStatus.Closed) nc.ClosedAt = null;
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Updated", "NonConformance", nc.Id, $"{nc.Reference} — {nc.Title}");
            TempData["Success"] = "Non-conformance updated.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── CLOSE ──────────────────────────────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Close(int id, string? rootCause)
        {
            if (!CanInvestigate()) return Denied();
            var nc = await _db.NonConformances.FindAsync(id);
            if (nc == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(rootCause)) nc.RootCause = rootCause.Trim();
            nc.Status = NcStatus.Closed;
            nc.ClosedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Closed", "NonConformance", id, $"{nc.Reference} closed.");
            TempData["Success"] = $"Non-conformance {nc.Reference} closed.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── DELETE ───────────────────────────────────────────────────────────────
        // Confirmation page — deleting is destructive, so it gets the same explicit
        // acknowledgement step as tickets rather than a browser confirm() popup.
        public async Task<IActionResult> Delete(int id)
        {
            if (!CanInvestigate()) return Denied();
            var entity = await _db.NonConformances.Include(n => n.Department).Include(n => n.RaisedBy)
                .FirstOrDefaultAsync(n => n.Id == id);
            if (entity == null) return NotFound();

            var vm = new ViewModels.DeleteConfirmationVm
            {
                EntityName = "Non-conformance",
                Icon = "fa-bug",
                RecordTitle = entity.Title,
                Reference = entity.Reference,
                Controller = "NonConformances",
                Id = entity.Id
            };
            vm.Add("Severity", entity.Severity.ToString());
            vm.Add("Status", entity.Status.ToString());
            vm.Add("Department", entity.Department?.Name);
            vm.Add("Raised By", entity.RaisedBy?.FullName);
            vm.Add("Detected", entity.DetectedDate.ToString("dd MMM yyyy"));
            vm.Consequences.Add("The non-conformance, its root cause and evidence will be removed from the register.");
            vm.Consequences.Add("It will no longer appear in CAPA tracking, dashboards or ISO reports.");
            return View("DeleteConfirm", vm);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!CanInvestigate()) return Denied();
            var nc = await _db.NonConformances.FindAsync(id);
            if (nc == null) return NotFound();
            _db.NonConformances.Remove(nc);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Deleted", "NonConformance", id, $"{nc.Reference} deleted.");
            TempData["Success"] = "Non-conformance deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
