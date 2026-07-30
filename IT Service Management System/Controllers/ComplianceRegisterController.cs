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
    /// Compliance Register (ISO 9001 cl. 9.1.2 / ISO 27001 cl. 9.1 &amp; A.18) — the register of legal,
    /// regulatory, contractual and standard obligations the organisation must meet, with owner, compliance
    /// status, assessment dates and evidence.
    /// </summary>
    [IT_Service_Management_System.Filters.RoleAuthorize("Admin", "SystemsAdmin", "QualityManager", "DocumentController", "DepartmentManager", "Auditor", "ExternalAuditor")]
    public class ComplianceRegisterController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditService _audit;

        public ComplianceRegisterController(ApplicationDbContext db, AuditService audit)
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
        public async Task<IActionResult> Index(ComplianceType? type, ComplianceStatus? status, string? q)
        {
            var query = _db.ComplianceObligations
                .Include(o => o.Department)
                .Include(o => o.Owner)
                .AsQueryable();

            if (type.HasValue) query = query.Where(o => o.Type == type.Value);
            if (status.HasValue) query = query.Where(o => o.Status == status.Value);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(o => o.Title.Contains(term)
                    || (o.Authority != null && o.Authority.Contains(term))
                    || (o.LegalReference != null && o.LegalReference.Contains(term)));
            }

            var list = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();

            ViewBag.Type = type;
            ViewBag.Status = status;
            ViewBag.Query = q;
            ViewBag.Total = list.Count;
            ViewBag.Compliant = list.Count(o => o.Status == ComplianceStatus.Compliant);
            ViewBag.NonCompliant = list.Count(o => o.Status == ComplianceStatus.NonCompliant);
            ViewBag.ReviewDue = list.Count(o => o.IsReviewDue);
            ViewBag.CanEdit = Can(ImsPermission.ManageCompliance);

            return View(list);
        }

        // ── DETAILS ────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Details(int id)
        {
            var obl = await _db.ComplianceObligations
                .Include(o => o.Department)
                .Include(o => o.Owner)
                .Include(o => o.CreatedBy)
                .FirstOrDefaultAsync(o => o.Id == id);
            if (obl == null) return NotFound();

            ViewBag.CanManage = Can(ImsPermission.ManageCompliance);
            LoadLookups();
            return View(obl);
        }

        // ── CREATE ───────────────────────────────────────────────────────────────
        public IActionResult Create()
        {
            if (!Can(ImsPermission.ManageCompliance)) return Denied();
            LoadLookups();
            return View(new ComplianceObligation());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ComplianceObligation model)
        {
            if (!Can(ImsPermission.ManageCompliance)) return Denied();

            if (!ModelState.IsValid)
            {
                LoadLookups();
                return View(model);
            }

            model.CreatedById = Uid;
            model.CreatedAt = DateTime.Now;
            _db.ComplianceObligations.Add(model);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Created", "ComplianceObligation", model.Id, $"{model.Reference} — {model.Title}");
            TempData["Success"] = $"Obligation {model.Reference} added to the register.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        // ── EDIT ───────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Edit(int id)
        {
            if (!Can(ImsPermission.ManageCompliance)) return Denied();
            var obl = await _db.ComplianceObligations.FindAsync(id);
            if (obl == null) return NotFound();
            LoadLookups();
            return View(obl);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ComplianceObligation model)
        {
            if (!Can(ImsPermission.ManageCompliance)) return Denied();
            var obl = await _db.ComplianceObligations.FindAsync(id);
            if (obl == null) return NotFound();

            if (!ModelState.IsValid) { LoadLookups(); return View(model); }

            obl.Title = model.Title;
            obl.Type = model.Type;
            obl.Standard = model.Standard;
            obl.Description = model.Description;
            obl.Requirement = model.Requirement;
            obl.Authority = model.Authority;
            obl.LegalReference = model.LegalReference;
            obl.OwnerId = model.OwnerId;
            obl.DepartmentId = model.DepartmentId;
            obl.Status = model.Status;
            obl.LastAssessedDate = model.LastAssessedDate;
            obl.NextReviewDate = model.NextReviewDate;
            obl.EvidenceNotes = model.EvidenceNotes;
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Updated", "ComplianceObligation", obl.Id, $"{obl.Reference} — {obl.Title}");
            TempData["Success"] = "Obligation updated.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── DELETE ─────────────────────────────────────────────────────────────────
        // Confirmation page — deleting is destructive, so it gets the same explicit
        // acknowledgement step as tickets rather than a browser confirm() popup.
        public async Task<IActionResult> Delete(int id)
        {
            if (!Can(ImsPermission.ManageCompliance)) return Denied();
            var entity = await _db.ComplianceObligations.Include(o => o.Owner).Include(o => o.Department)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null) return NotFound();

            var vm = new ViewModels.DeleteConfirmationVm
            {
                EntityName = "Compliance Obligation",
                Icon = "fa-scale-balanced",
                RecordTitle = entity.Title,
                Reference = entity.Reference,
                Controller = "ComplianceRegister",
                Id = entity.Id
            };
            vm.Add("Type", entity.Type.ToString());
            vm.Add("Status", entity.Status.ToString());
            vm.Add("Owner", entity.Owner?.FullName);
            vm.Add("Department", entity.Department?.Name);
            vm.Add("Next Review", entity.NextReviewDate?.ToString("dd MMM yyyy"));
            vm.Consequences.Add("The obligation, its requirement text and evidence notes will be removed from the register.");
            vm.Consequences.Add("Its assessment history and review dates will no longer appear in compliance dashboards or ISO reports.");
            return View("DeleteConfirm", vm);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!Can(ImsPermission.ManageCompliance)) return Denied();
            var obl = await _db.ComplianceObligations.FindAsync(id);
            if (obl == null) return NotFound();
            _db.ComplianceObligations.Remove(obl);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Deleted", "ComplianceObligation", id, $"{obl.Reference} deleted.");
            TempData["Success"] = "Obligation deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
