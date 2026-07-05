using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models.Itsm;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>ITIL Problem management — track root causes behind recurring incidents, record
    /// workarounds/known errors, link incidents, and raise changes to fix them permanently.</summary>
    [IT_Service_Management_System.Filters.RoleAuthorize("Admin", "SystemsAdmin")]
    public class ProblemsController : Controller
    {
        private readonly ApplicationDbContext _db;
        public ProblemsController(ApplicationDbContext db) => _db = db;

        private int? Uid => HttpContext.Session.GetInt32("UserId");

        private async Task PopulateListsAsync()
        {
            ViewBag.Cis = await _db.ConfigurationItems.OrderBy(c => c.Name)
                .Select(c => new { c.Id, c.Name }).ToListAsync();
            ViewBag.Agents = await _db.Users.Where(u => u.IsActive).OrderBy(u => u.FirstName)
                .Select(u => new { u.Id, Name = u.FirstName + " " + u.LastName }).ToListAsync();
        }

        public async Task<IActionResult> Index(ProblemStatus? status, string? q)
        {
            IQueryable<Problem> query = _db.Problems.Include(p => p.ConfigurationItem).Include(p => p.AssignedTo);
            if (status.HasValue) query = query.Where(p => p.Status == status.Value);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var t = q.Trim();
                query = query.Where(p => p.Title.Contains(t) || p.Description.Contains(t));
            }

            var all = await _db.Problems.AsNoTracking().Select(p => new { p.Status }).ToListAsync();
            ViewBag.Total = all.Count;
            ViewBag.Open = all.Count(p => p.Status != ProblemStatus.Resolved && p.Status != ProblemStatus.Closed);
            ViewBag.KnownErrors = all.Count(p => p.Status == ProblemStatus.KnownError);
            ViewBag.Resolved = all.Count(p => p.Status == ProblemStatus.Resolved || p.Status == ProblemStatus.Closed);
            ViewBag.Status = status; ViewBag.Q = q;

            // Linked-incident counts for the list.
            ViewBag.IncidentCounts = await _db.Tickets.Where(t => t.ProblemId != null)
                .GroupBy(t => t.ProblemId!.Value).Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            return View(await query.OrderByDescending(p => p.CreatedAt).ToListAsync());
        }

        public async Task<IActionResult> Create()
        {
            await PopulateListsAsync();
            return View("Form", new Problem());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Problem input)
        {
            if (!ModelState.IsValid) { await PopulateListsAsync(); return View("Form", input); }
            input.CreatedById = Uid ?? 0;
            input.CreatedAt = DateTime.Now;
            input.Status = ProblemStatus.New;
            _db.Problems.Add(input);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Problem {input.ProblemRef} created.";
            return RedirectToAction(nameof(Details), new { id = input.Id });
        }

        public async Task<IActionResult> Edit(int id)
        {
            var p = await _db.Problems.FindAsync(id);
            if (p == null) return NotFound();
            await PopulateListsAsync();
            return View("Form", p);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Problem input)
        {
            var p = await _db.Problems.FindAsync(input.Id);
            if (p == null) return NotFound();
            if (!ModelState.IsValid) { await PopulateListsAsync(); return View("Form", input); }

            p.Title = input.Title; p.Description = input.Description; p.Priority = input.Priority;
            p.RootCause = input.RootCause; p.Workaround = input.Workaround;
            p.ConfigurationItemId = input.ConfigurationItemId; p.AssignedToId = input.AssignedToId;
            p.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Problem {p.ProblemRef} updated.";
            return RedirectToAction(nameof(Details), new { id = p.Id });
        }

        public async Task<IActionResult> Details(int id)
        {
            var p = await _db.Problems.Include(x => x.ConfigurationItem).Include(x => x.AssignedTo)
                .Include(x => x.CreatedBy).FirstOrDefaultAsync(x => x.Id == id);
            if (p == null) return NotFound();

            ViewBag.Incidents = await _db.Tickets.Include(t => t.CreatedBy)
                .Where(t => t.ProblemId == id).OrderByDescending(t => t.CreatedAt).ToListAsync();
            ViewBag.Changes = await _db.ChangeRequests.Where(c => c.ProblemId == id)
                .OrderByDescending(c => c.CreatedAt).ToListAsync();
            // Open, unlinked incidents that could be attached to this problem.
            ViewBag.LinkableIncidents = await _db.Tickets
                .Where(t => t.ProblemId == null && t.Status != Models.Ticket.TicketStatus.Closed)
                .OrderByDescending(t => t.CreatedAt).Take(50)
                .Select(t => new { t.Id, Label = "TKT-" + t.Id.ToString("D5") + " · " + t.Title }).ToListAsync();
            return View(p);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatus(int id, ProblemStatus status)
        {
            var p = await _db.Problems.FindAsync(id);
            if (p == null) return NotFound();
            p.Status = status;
            p.UpdatedAt = DateTime.Now;
            if (status == ProblemStatus.Resolved) p.ResolvedAt ??= DateTime.Now;
            if (status == ProblemStatus.Closed) p.ClosedAt ??= DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Problem status set to {status}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LinkIncident(int id, int ticketId)
        {
            var ticket = await _db.Tickets.FindAsync(ticketId);
            if (ticket == null) { TempData["Error"] = "Ticket not found."; return RedirectToAction(nameof(Details), new { id }); }
            ticket.ProblemId = id;
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Incident TKT-{ticketId:D5} linked.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnlinkIncident(int id, int ticketId)
        {
            var ticket = await _db.Tickets.FindAsync(ticketId);
            if (ticket != null && ticket.ProblemId == id) { ticket.ProblemId = null; await _db.SaveChangesAsync(); }
            TempData["Success"] = "Incident unlinked.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var p = await _db.Problems.FindAsync(id);
            if (p == null) return NotFound();
            _db.Problems.Remove(p);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Problem {p.ProblemRef} deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
