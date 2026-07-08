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
    /// Supplier evaluation &amp; control (ISO 9001:2015 cl. 8.4 — control of externally provided processes,
    /// products and services). Maintains the approved-supplier register and the periodic performance
    /// evaluations scored 0–100 across quality, delivery, pricing, support and compliance.
    /// </summary>
    [IT_Service_Management_System.Filters.RoleAuthorize("Admin", "SystemsAdmin", "QualityManager", "DocumentController", "DepartmentManager", "Auditor", "ExternalAuditor")]
    public class SuppliersController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditService _audit;

        public SuppliersController(ApplicationDbContext db, AuditService audit)
        {
            _db = db;
            _audit = audit;
        }

        private int? Uid => HttpContext.Session.GetInt32("UserId");
        private string? Role => HttpContext.Session.GetString("UserRole");
        private bool Can(ImsPermission p) => ImsAccess.Can(Role, p);
        private IActionResult Denied() => RedirectToAction("AccessDenied", "Home");

        // ── LIST ─────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Index(SupplierStatus? status)
        {
            var query = _db.Suppliers
                .Include(s => s.Evaluations)
                .AsQueryable();

            if (status.HasValue) query = query.Where(s => s.Status == status.Value);

            var list = await query.OrderBy(s => s.Name).ToListAsync();

            var scored = list
                .Select(s => s.Evaluations.OrderByDescending(e => e.EvaluationDate).ThenByDescending(e => e.Id).FirstOrDefault())
                .Where(e => e != null)
                .Select(e => e!.OverallScore)
                .ToList();

            ViewBag.Status = status;
            ViewBag.Total = list.Count;
            ViewBag.Approved = list.Count(s => s.Status == SupplierStatus.Approved);
            ViewBag.Expiring = list.Count(s => s.ContractEnd.HasValue
                && s.ContractEnd.Value.Date >= DateTime.Now.Date
                && s.ContractEnd.Value.Date <= DateTime.Now.Date.AddDays(30));
            ViewBag.AvgScore = scored.Any() ? (int)Math.Round(scored.Average()) : 0;
            ViewBag.CanManage = Can(ImsPermission.ManageSuppliers);

            return View(list);
        }

        // ── DETAILS ────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Details(int id)
        {
            var supplier = await _db.Suppliers
                .Include(s => s.CreatedBy)
                .Include(s => s.Evaluations).ThenInclude(e => e.EvaluatedBy)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (supplier == null) return NotFound();

            var latest = supplier.Evaluations
                .OrderByDescending(e => e.EvaluationDate).ThenByDescending(e => e.Id)
                .FirstOrDefault();

            ViewBag.Latest = latest;
            ViewBag.CanManage = Can(ImsPermission.ManageSuppliers);

            return View(supplier);
        }

        // ── CREATE ───────────────────────────────────────────────────────────────
        public IActionResult Create()
        {
            if (!Can(ImsPermission.ManageSuppliers)) return Denied();
            return View(new Supplier());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Supplier model)
        {
            if (!Can(ImsPermission.ManageSuppliers)) return Denied();

            if (!ModelState.IsValid) return View(model);

            model.CreatedById = Uid;
            model.CreatedAt = DateTime.Now;
            if (model.Status == SupplierStatus.Approved && model.ApprovedDate == null)
                model.ApprovedDate = DateTime.Now;

            _db.Suppliers.Add(model);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Created", "Supplier", model.Id, $"{model.Reference} — {model.Name}");
            TempData["Success"] = $"Supplier {model.Reference} created.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        // ── EDIT ───────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Edit(int id)
        {
            if (!Can(ImsPermission.ManageSuppliers)) return Denied();
            var supplier = await _db.Suppliers.FindAsync(id);
            if (supplier == null) return NotFound();
            return View(supplier);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Supplier model)
        {
            if (!Can(ImsPermission.ManageSuppliers)) return Denied();
            var supplier = await _db.Suppliers.FindAsync(id);
            if (supplier == null) return NotFound();

            if (!ModelState.IsValid) return View(model);

            supplier.Name = model.Name;
            supplier.Category = model.Category;
            supplier.Status = model.Status;
            supplier.ContactName = model.ContactName;
            supplier.Email = model.Email;
            supplier.Phone = model.Phone;
            supplier.Address = model.Address;
            supplier.ProductsServices = model.ProductsServices;
            supplier.ApprovedDate = model.ApprovedDate;
            supplier.ContractStart = model.ContractStart;
            supplier.ContractEnd = model.ContractEnd;
            supplier.CertificateName = model.CertificateName;
            supplier.CertificateExpiry = model.CertificateExpiry;
            supplier.Notes = model.Notes;
            if (supplier.Status == SupplierStatus.Approved && supplier.ApprovedDate == null)
                supplier.ApprovedDate = DateTime.Now;

            await _db.SaveChangesAsync();

            await _audit.LogAsync("Updated", "Supplier", supplier.Id, $"{supplier.Reference} — {supplier.Name}");
            TempData["Success"] = "Supplier updated.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── DELETE (hard) ──────────────────────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!Can(ImsPermission.ManageSuppliers)) return Denied();
            var supplier = await _db.Suppliers.Include(s => s.Evaluations).FirstOrDefaultAsync(s => s.Id == id);
            if (supplier == null) return NotFound();

            var reference = supplier.Reference;
            var name = supplier.Name;
            _db.SupplierEvaluations.RemoveRange(supplier.Evaluations);
            _db.Suppliers.Remove(supplier);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Deleted", "Supplier", id, $"{reference} — {name}");
            TempData["Success"] = "Supplier deleted.";
            return RedirectToAction(nameof(Index));
        }

        // ── ADD EVALUATION ───────────────────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddEvaluation(int id, SupplierEvaluation eval)
        {
            if (!Can(ImsPermission.ManageSuppliers)) return Denied();
            var supplier = await _db.Suppliers.FindAsync(id);
            if (supplier == null) return NotFound();

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please provide valid scores (0–100) for every criterion.";
                return RedirectToAction(nameof(Details), new { id });
            }

            eval.SupplierId = id;
            eval.EvaluatedById = Uid;
            eval.CreatedAt = DateTime.Now;
            if (eval.EvaluationDate == default) eval.EvaluationDate = DateTime.Now;

            _db.SupplierEvaluations.Add(eval);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Evaluated", "Supplier", id, $"{supplier.Reference} — {eval.Period} evaluation, overall {eval.OverallScore} ({eval.Rating})");
            TempData["Success"] = $"Evaluation recorded — overall score {eval.OverallScore} ({eval.Rating}).";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
