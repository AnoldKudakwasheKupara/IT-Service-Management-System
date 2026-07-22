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
    /// Risk Register, Risk Assessment Matrix (heat map) and Opportunities Register
    /// (ISO 9001/27001 cl. 6.1). Likelihood × Impact scoring drives the risk band and the heat map.
    /// </summary>
    [RoleAuthorize("Admin", "SystemsAdmin", "QualityManager", "DocumentController", "DepartmentManager", "Auditor", "ExternalAuditor")]
    public class RiskController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditService _audit;

        public RiskController(ApplicationDbContext db, AuditService audit)
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
            ViewBag.Users = _db.Users.Where(u => u.IsActive).OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToList();
            ViewBag.Departments = _db.Departments.OrderBy(d => d.Name).ToList();
            ViewBag.Assets = _db.Assets.OrderBy(a => a.ItemName).ToList();
        }

        // ── RISKS ─────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Index(RiskCategory? category, RiskStatus? status)
        {
            var query = _db.Risks.Include(r => r.Owner).Include(r => r.Department).Include(r => r.Asset).AsQueryable();
            if (category.HasValue) query = query.Where(r => r.Category == category.Value);
            if (status.HasValue) query = query.Where(r => r.Status == status.Value);

            var list = await query.ToListAsync();
            list = list.OrderByDescending(r => r.Score).ToList();

            ViewBag.Category = category;
            ViewBag.Status = status;
            ViewBag.Total = list.Count;
            ViewBag.Open = list.Count(r => r.Status != RiskStatus.Closed);
            ViewBag.Critical = list.Count(r => r.Band == RiskBand.Critical && r.Status != RiskStatus.Closed);
            ViewBag.ReviewDue = list.Count(r => r.IsReviewDue);
            ViewBag.CanEdit = Can(ImsPermission.ManageRisk);
            return View(list);
        }

        public async Task<IActionResult> Details(int id)
        {
            var risk = await _db.Risks
                .Include(r => r.Owner).Include(r => r.Department).Include(r => r.Asset).Include(r => r.CreatedBy)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (risk == null) return NotFound();
            ViewBag.CanEdit = Can(ImsPermission.ManageRisk);
            return View(risk);
        }

        public IActionResult Create()
        {
            if (!Can(ImsPermission.ManageRisk)) return Denied();
            LoadLookups();
            return View(new Risk());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Risk model)
        {
            if (!Can(ImsPermission.ManageRisk)) return Denied();
            if (!ModelState.IsValid) { LoadLookups(); return View(model); }
            model.CreatedById = Uid;
            model.CreatedAt = DateTime.Now;
            _db.Risks.Add(model);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Created", "Risk", model.Id, $"{model.Reference} — {model.Title}");
            TempData["Success"] = $"{model.Reference} added to the register.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        public async Task<IActionResult> Edit(int id)
        {
            if (!Can(ImsPermission.ManageRisk)) return Denied();
            var risk = await _db.Risks.FindAsync(id);
            if (risk == null) return NotFound();
            LoadLookups();
            return View(risk);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Risk model)
        {
            if (!Can(ImsPermission.ManageRisk)) return Denied();
            var risk = await _db.Risks.FindAsync(id);
            if (risk == null) return NotFound();
            if (!ModelState.IsValid) { LoadLookups(); return View(model); }

            risk.Title = model.Title;
            risk.Category = model.Category;
            risk.Standard = model.Standard;
            risk.Description = model.Description;
            risk.AssetId = model.AssetId;
            risk.Threat = model.Threat;
            risk.Vulnerability = model.Vulnerability;
            risk.Likelihood = model.Likelihood;
            risk.Impact = model.Impact;
            risk.Treatment = model.Treatment;
            risk.TreatmentPlan = model.TreatmentPlan;
            risk.OwnerId = model.OwnerId;
            risk.DepartmentId = model.DepartmentId;
            risk.ResidualLikelihood = model.ResidualLikelihood;
            risk.ResidualImpact = model.ResidualImpact;
            risk.Status = model.Status;
            risk.ReviewDate = model.ReviewDate;
            if (model.Status == RiskStatus.Closed && risk.ClosedAt == null) risk.ClosedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Updated", "Risk", id, $"{risk.Reference} updated.");
            TempData["Success"] = "Risk updated.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!Can(ImsPermission.ManageRisk)) return Denied();
            var risk = await _db.Risks.FindAsync(id);
            if (risk == null) return NotFound();
            _db.Risks.Remove(risk);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Deleted", "Risk", id, $"{risk.Reference} deleted.");
            TempData["Success"] = "Risk deleted.";
            return RedirectToAction(nameof(Index));
        }

        // ── MATRIX (heat map) ─────────────────────────────────────────────────────
        public async Task<IActionResult> Matrix()
        {
            var risks = await _db.Risks.Where(r => r.Status != RiskStatus.Closed).Include(r => r.Owner).ToListAsync();
            return View(risks);
        }

        // ── OPPORTUNITIES ─────────────────────────────────────────────────────────
        public async Task<IActionResult> Opportunities(OpportunityStatus? status)
        {
            var query = _db.Opportunities.Include(o => o.Owner).Include(o => o.Department).AsQueryable();
            if (status.HasValue) query = query.Where(o => o.Status == status.Value);
            var list = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();
            ViewBag.Status = status;
            ViewBag.Total = list.Count;
            ViewBag.Open = list.Count(o => o.Status != OpportunityStatus.Closed && o.Status != OpportunityStatus.Declined);
            ViewBag.Realised = list.Count(o => o.Status == OpportunityStatus.Realised);
            ViewBag.CanEdit = Can(ImsPermission.ManageRisk);
            return View(list);
        }

        public IActionResult CreateOpportunity()
        {
            if (!Can(ImsPermission.ManageRisk)) return Denied();
            LoadLookups();
            return View(new Opportunity());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateOpportunity(Opportunity model)
        {
            if (!Can(ImsPermission.ManageRisk)) return Denied();
            if (!ModelState.IsValid) { LoadLookups(); return View(model); }
            model.CreatedById = Uid;
            model.CreatedAt = DateTime.Now;
            _db.Opportunities.Add(model);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Created", "Opportunity", model.Id, $"{model.Reference} — {model.Title}");
            TempData["Success"] = $"{model.Reference} added.";
            return RedirectToAction(nameof(Opportunities));
        }

        public async Task<IActionResult> EditOpportunity(int id)
        {
            if (!Can(ImsPermission.ManageRisk)) return Denied();
            var opp = await _db.Opportunities.FindAsync(id);
            if (opp == null) return NotFound();
            LoadLookups();
            return View(opp);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditOpportunity(int id, Opportunity model)
        {
            if (!Can(ImsPermission.ManageRisk)) return Denied();
            var opp = await _db.Opportunities.FindAsync(id);
            if (opp == null) return NotFound();
            if (!ModelState.IsValid) { LoadLookups(); return View(model); }
            opp.Title = model.Title;
            opp.Description = model.Description;
            opp.Standard = model.Standard;
            opp.Benefit = model.Benefit;
            opp.Likelihood = model.Likelihood;
            opp.BenefitScore = model.BenefitScore;
            opp.ActionPlan = model.ActionPlan;
            opp.OwnerId = model.OwnerId;
            opp.DepartmentId = model.DepartmentId;
            opp.Status = model.Status;
            opp.TargetDate = model.TargetDate;
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Updated", "Opportunity", id, $"{opp.Reference} updated.");
            TempData["Success"] = "Opportunity updated.";
            return RedirectToAction(nameof(Opportunities));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOpportunity(int id)
        {
            if (!Can(ImsPermission.ManageRisk)) return Denied();
            var opp = await _db.Opportunities.FindAsync(id);
            if (opp == null) return NotFound();
            _db.Opportunities.Remove(opp);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Deleted", "Opportunity", id, $"{opp.Reference} deleted.");
            TempData["Success"] = "Opportunity deleted.";
            return RedirectToAction(nameof(Opportunities));
        }
    }
}
