using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models.Itsm;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static IT_Service_Management_System.Models.Ticket;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>Admin CRUD for SLA policies — configurable response + resolution targets per
    /// priority/category that drive ticket due dates and SLA-breach reporting.</summary>
    [IT_Service_Management_System.Filters.RoleAuthorize("Admin", "SystemsAdmin")]
    public class SlaPoliciesController : Controller
    {
        private readonly ApplicationDbContext _db;
        public SlaPoliciesController(ApplicationDbContext db) => _db = db;

        public async Task<IActionResult> Index()
        {
            ViewBag.Categories = await _db.Tickets.Where(t => t.Category != null && t.Category != "")
                .Select(t => t.Category).Distinct().OrderBy(c => c).ToListAsync();
            var policies = await _db.SlaPolicies.OrderBy(p => p.Priority).ThenBy(p => p.Name).ToListAsync();
            return View(policies);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(int id, string name, TicketPriority? priority, string? category,
            int responseMinutes, int resolutionMinutes, bool businessHoursOnly, bool isActive)
        {
            if (string.IsNullOrWhiteSpace(name))
            { TempData["Error"] = "Policy name is required."; return RedirectToAction(nameof(Index)); }
            if (responseMinutes < 1 || resolutionMinutes < 1)
            { TempData["Error"] = "Response and resolution targets must be positive."; return RedirectToAction(nameof(Index)); }

            category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();

            if (id == 0)
            {
                _db.SlaPolicies.Add(new SlaPolicy
                {
                    Name = name.Trim(), Priority = priority, Category = category,
                    ResponseMinutes = responseMinutes, ResolutionMinutes = resolutionMinutes,
                    BusinessHoursOnly = businessHoursOnly, IsActive = isActive, CreatedAt = DateTime.Now
                });
                TempData["Success"] = $"SLA policy '{name}' created.";
            }
            else
            {
                var p = await _db.SlaPolicies.FindAsync(id);
                if (p == null) return NotFound();
                p.Name = name.Trim(); p.Priority = priority; p.Category = category;
                p.ResponseMinutes = responseMinutes; p.ResolutionMinutes = resolutionMinutes;
                p.BusinessHoursOnly = businessHoursOnly; p.IsActive = isActive;
                TempData["Success"] = $"SLA policy '{name}' updated.";
            }
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int id)
        {
            var p = await _db.SlaPolicies.FindAsync(id);
            if (p == null) return NotFound();
            p.IsActive = !p.IsActive;
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Policy '{p.Name}' {(p.IsActive ? "activated" : "deactivated")}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var p = await _db.SlaPolicies.FindAsync(id);
            if (p == null) return NotFound();
            _db.SlaPolicies.Remove(p);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Policy '{p.Name}' deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
