using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models.Efm;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>
    /// HR-configurable Employee File Management settings — document categories, required-document
    /// rules (which categories are mandatory, per role/department) and retention policies. Lets HR
    /// tune completeness + retention behaviour without code changes.
    /// </summary>
    [IT_Service_Management_System.Filters.RoleAuthorize("Admin", "SystemsAdmin", "HR")]
    public class EfmSettingsController : Controller
    {
        private readonly ApplicationDbContext _db;
        public EfmSettingsController(ApplicationDbContext db) => _db = db;

        private IActionResult Denied() => RedirectToAction("AccessDenied", "Home");

        // ── categories ─────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Categories()
        {
            ViewBag.Folders = await _db.DocumentFolders.Where(f => f.IsActive).OrderBy(f => f.SortOrder).ToListAsync();
            ViewBag.Counts = await _db.EmployeeDocuments.GroupBy(d => d.CategoryId)
                .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
            var cats = await _db.DocumentCategories.Include(c => c.DefaultFolder).OrderBy(c => c.Name).ToListAsync();
            return View(cats);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveCategory(int id, string name, string? description,
            int? defaultFolderId, bool isExpiryTracked, int? defaultRetentionYears, bool isActive)
        {
            if (string.IsNullOrWhiteSpace(name))
            { TempData["Error"] = "Category name is required."; return RedirectToAction(nameof(Categories)); }
            name = name.Trim();

            var dupe = await _db.DocumentCategories.AnyAsync(c => c.Id != id && c.Name == name);
            if (dupe)
            { TempData["Error"] = $"A category named '{name}' already exists."; return RedirectToAction(nameof(Categories)); }

            if (id == 0)
            {
                _db.DocumentCategories.Add(new DocumentCategory
                {
                    Name = name, Description = description, DefaultFolderId = defaultFolderId,
                    IsExpiryTracked = isExpiryTracked, DefaultRetentionYears = defaultRetentionYears,
                    IsActive = isActive, CreatedAt = DateTime.Now
                });
                TempData["Success"] = $"Category '{name}' created.";
            }
            else
            {
                var cat = await _db.DocumentCategories.FindAsync(id);
                if (cat == null) return NotFound();
                cat.Name = name; cat.Description = description; cat.DefaultFolderId = defaultFolderId;
                cat.IsExpiryTracked = isExpiryTracked; cat.DefaultRetentionYears = defaultRetentionYears;
                cat.IsActive = isActive;
                TempData["Success"] = $"Category '{name}' updated.";
            }
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Categories));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleCategory(int id)
        {
            var cat = await _db.DocumentCategories.FindAsync(id);
            if (cat == null) return NotFound();
            cat.IsActive = !cat.IsActive;
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Category '{cat.Name}' {(cat.IsActive ? "activated" : "deactivated")}.";
            return RedirectToAction(nameof(Categories));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var cat = await _db.DocumentCategories.FindAsync(id);
            if (cat == null) return NotFound();

            // Documents (incl. soft-deleted) keep the FK, and it's Restrict — block rather than crash.
            var docCount = await _db.EmployeeDocuments.IgnoreQueryFilters().CountAsync(d => d.CategoryId == id);
            if (docCount > 0)
            {
                TempData["Error"] = $"Cannot delete '{cat.Name}' — {docCount} document(s) still use it. Deactivate it instead, or reassign those documents first.";
                return RedirectToAction(nameof(Categories));
            }
            if (await _db.RetentionPolicies.AnyAsync(p => p.CategoryId == id))
            {
                TempData["Error"] = $"Cannot delete '{cat.Name}' — a retention policy targets it. Update or remove that policy first.";
                return RedirectToAction(nameof(Categories));
            }

            // RequiredDocument rules for this category cascade-delete with it.
            _db.DocumentCategories.Remove(cat);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Category '{cat.Name}' deleted.";
            return RedirectToAction(nameof(Categories));
        }

        // ── required documents ───────────────────────────────────────────────────────────
        public async Task<IActionResult> Required()
        {
            ViewBag.Categories = await _db.DocumentCategories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
            ViewBag.Departments = await _db.Departments.OrderBy(d => d.Name).ToListAsync();
            var rules = await _db.RequiredDocuments
                .Include(r => r.Category)
                .Include(r => r.AppliesToDepartment)
                .OrderBy(r => r.Category!.Name).ToListAsync();
            return View(rules);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveRequired(int id, int categoryId, string? appliesToRole,
            int? appliesToDepartmentId, bool isMandatory, bool isActive)
        {
            if (!await _db.DocumentCategories.AnyAsync(c => c.Id == categoryId))
            { TempData["Error"] = "Choose a valid category."; return RedirectToAction(nameof(Required)); }

            var role = string.IsNullOrWhiteSpace(appliesToRole) ? null : appliesToRole.Trim();

            if (id == 0)
            {
                var dupe = await _db.RequiredDocuments.AnyAsync(r => r.CategoryId == categoryId
                    && r.AppliesToRole == role && r.AppliesToDepartmentId == appliesToDepartmentId);
                if (dupe)
                { TempData["Error"] = "An identical requirement already exists."; return RedirectToAction(nameof(Required)); }

                _db.RequiredDocuments.Add(new RequiredDocument
                {
                    CategoryId = categoryId, AppliesToRole = role, AppliesToDepartmentId = appliesToDepartmentId,
                    IsMandatory = isMandatory, IsActive = isActive
                });
                TempData["Success"] = "Requirement added.";
            }
            else
            {
                var rule = await _db.RequiredDocuments.FindAsync(id);
                if (rule == null) return NotFound();
                rule.CategoryId = categoryId; rule.AppliesToRole = role;
                rule.AppliesToDepartmentId = appliesToDepartmentId;
                rule.IsMandatory = isMandatory; rule.IsActive = isActive;
                TempData["Success"] = "Requirement updated.";
            }
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Required));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRequired(int id)
        {
            var rule = await _db.RequiredDocuments.FindAsync(id);
            if (rule == null) return NotFound();
            _db.RequiredDocuments.Remove(rule);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Requirement removed.";
            return RedirectToAction(nameof(Required));
        }

        // ── retention policies ───────────────────────────────────────────────────────────
        public async Task<IActionResult> Retention()
        {
            ViewBag.Categories = await _db.DocumentCategories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
            ViewBag.Folders = await _db.DocumentFolders.Where(f => f.IsActive).OrderBy(f => f.SortOrder).ToListAsync();
            var policies = await _db.RetentionPolicies
                .Include(p => p.Category).Include(p => p.Folder).OrderBy(p => p.Name).ToListAsync();
            return View(policies);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveRetention(int id, string name, int? categoryId, int? folderId,
            int? retentionYears, RetentionAction action, bool isActive)
        {
            if (string.IsNullOrWhiteSpace(name))
            { TempData["Error"] = "Policy name is required."; return RedirectToAction(nameof(Retention)); }
            name = name.Trim();

            if (id == 0)
            {
                _db.RetentionPolicies.Add(new RetentionPolicy
                {
                    Name = name, CategoryId = categoryId, FolderId = folderId,
                    RetentionYears = retentionYears, Action = action, IsActive = isActive
                });
                TempData["Success"] = $"Retention policy '{name}' created.";
            }
            else
            {
                var p = await _db.RetentionPolicies.FindAsync(id);
                if (p == null) return NotFound();
                p.Name = name; p.CategoryId = categoryId; p.FolderId = folderId;
                p.RetentionYears = retentionYears; p.Action = action; p.IsActive = isActive;
                TempData["Success"] = $"Retention policy '{name}' updated.";
            }
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Retention));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleRetention(int id)
        {
            var p = await _db.RetentionPolicies.FindAsync(id);
            if (p == null) return NotFound();
            p.IsActive = !p.IsActive;
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Policy '{p.Name}' {(p.IsActive ? "activated" : "deactivated")}.";
            return RedirectToAction(nameof(Retention));
        }
    }
}
