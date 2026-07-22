using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models.Itsm;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>CMDB — manage Configuration Items (servers, apps, services, databases…) that
    /// incidents, problems and changes reference for impact analysis.</summary>
    [IT_Service_Management_System.Filters.RoleAuthorize("Admin", "SystemsAdmin")]
    public class CmdbController : Controller
    {
        private readonly ApplicationDbContext _db;
        public CmdbController(ApplicationDbContext db) => _db = db;

        public async Task<IActionResult> Index(string? q, CiType? type, CiStatus? status, CiCriticality? criticality)
        {
            IQueryable<ConfigurationItem> query = _db.ConfigurationItems.Include(c => c.Owner);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var t = q.Trim();
                query = query.Where(c => c.Name.Contains(t) || (c.Vendor != null && c.Vendor.Contains(t))
                    || (c.IpOrHostname != null && c.IpOrHostname.Contains(t)) || (c.Location != null && c.Location.Contains(t)));
            }
            if (type.HasValue) query = query.Where(c => c.Type == type.Value);
            if (status.HasValue) query = query.Where(c => c.Status == status.Value);
            if (criticality.HasValue) query = query.Where(c => c.Criticality == criticality.Value);

            var all = await _db.ConfigurationItems.AsNoTracking()
                .Select(c => new { c.Status, c.Criticality }).ToListAsync();
            ViewBag.Total = all.Count;
            ViewBag.Active = all.Count(c => c.Status == CiStatus.Active);
            ViewBag.Critical = all.Count(c => c.Criticality == CiCriticality.Critical);
            ViewBag.Maintenance = all.Count(c => c.Status == CiStatus.UnderMaintenance);

            ViewBag.Owners = await _db.Users.Where(u => u.IsActive).OrderBy(u => u.FirstName)
                .Select(u => new { u.Id, Name = u.FirstName + " " + u.LastName }).ToListAsync();
            ViewBag.Assets = await _db.Assets.OrderBy(a => a.ItemName)
                .Select(a => new { a.Id, Name = a.ItemName + " (" + (a.AssetTag ?? a.SerialNumber) + ")" }).ToListAsync();
            ViewBag.Q = q; ViewBag.Type = type; ViewBag.Status = status; ViewBag.Criticality = criticality;

            return View(await query.OrderBy(c => c.Name).ToListAsync());
        }

        public async Task<IActionResult> Details(int id)
        {
            var ci = await _db.ConfigurationItems.Include(c => c.Owner).Include(c => c.Asset)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (ci == null) return NotFound();

            ViewBag.Incidents = await _db.Tickets.Where(t => t.ConfigurationItemId == id)
                .OrderByDescending(t => t.CreatedAt).Take(20).ToListAsync();
            ViewBag.Problems = await _db.Problems.Where(p => p.ConfigurationItemId == id)
                .OrderByDescending(p => p.CreatedAt).Take(20).ToListAsync();
            ViewBag.Changes = await _db.ChangeRequests.Where(c => c.ConfigurationItemId == id)
                .OrderByDescending(c => c.CreatedAt).Take(20).ToListAsync();
            return View(ci);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(int id, string name, CiType type, CiStatus status,
            CiCriticality criticality, CiEnvironment environment, string? description, string? location,
            string? vendor, string? version, string? ipOrHostname, int? ownerId, int? assetId)
        {
            if (string.IsNullOrWhiteSpace(name))
            { TempData["Error"] = "CI name is required."; return RedirectToAction(nameof(Index)); }

            if (id == 0)
            {
                _db.ConfigurationItems.Add(new ConfigurationItem
                {
                    Name = name.Trim(), Type = type, Status = status, Criticality = criticality,
                    Environment = environment, Description = description, Location = location, Vendor = vendor,
                    Version = version, IpOrHostname = ipOrHostname, OwnerId = ownerId, AssetId = assetId,
                    CreatedAt = DateTime.Now
                });
                TempData["Success"] = $"Configuration item '{name}' created.";
            }
            else
            {
                var ci = await _db.ConfigurationItems.FindAsync(id);
                if (ci == null) return NotFound();
                ci.Name = name.Trim(); ci.Type = type; ci.Status = status; ci.Criticality = criticality;
                ci.Environment = environment; ci.Description = description; ci.Location = location;
                ci.Vendor = vendor; ci.Version = version; ci.IpOrHostname = ipOrHostname;
                ci.OwnerId = ownerId; ci.AssetId = assetId;
                TempData["Success"] = $"Configuration item '{name}' updated.";
            }
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var ci = await _db.ConfigurationItems.FindAsync(id);
            if (ci == null) return NotFound();
            // Linked incidents/problems/changes are detached (FK set null), not deleted.
            _db.ConfigurationItems.Remove(ci);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Configuration item '{ci.Name}' deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
