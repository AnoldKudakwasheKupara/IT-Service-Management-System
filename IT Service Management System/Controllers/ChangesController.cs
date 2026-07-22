using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models.Itsm;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>ITIL Change management — controlled changes to CIs/services with risk, an approval
    /// gate, scheduled windows, implementation/backout plans, and success tracking.</summary>
    [IT_Service_Management_System.Filters.RoleAuthorize("Admin", "SystemsAdmin")]
    public class ChangesController : Controller
    {
        private readonly ApplicationDbContext _db;
        public ChangesController(ApplicationDbContext db) => _db = db;

        private int? Uid => HttpContext.Session.GetInt32("UserId");
        private string? UserName => HttpContext.Session.GetString("UserName");

        private async Task PopulateListsAsync()
        {
            ViewBag.Cis = await _db.ConfigurationItems.OrderBy(c => c.Name)
                .Select(c => new { c.Id, c.Name }).ToListAsync();
            ViewBag.Agents = await _db.Users.Where(u => u.IsActive).OrderBy(u => u.FirstName)
                .Select(u => new { u.Id, Name = u.FirstName + " " + u.LastName }).ToListAsync();
            ViewBag.Problems = await _db.Problems.OrderByDescending(p => p.CreatedAt)
                .Select(p => new { p.Id, Label = "PRB-" + p.Id.ToString("D5") + " · " + p.Title }).ToListAsync();
        }

        public async Task<IActionResult> Index(ChangeStatus? status, ChangeType? type, string? q)
        {
            IQueryable<ChangeRequest> query = _db.ChangeRequests
                .Include(c => c.ConfigurationItem).Include(c => c.AssignedTo);
            if (status.HasValue) query = query.Where(c => c.Status == status.Value);
            if (type.HasValue) query = query.Where(c => c.Type == type.Value);
            if (!string.IsNullOrWhiteSpace(q))
            { var t = q.Trim(); query = query.Where(c => c.Title.Contains(t) || c.Description.Contains(t)); }

            var all = await _db.ChangeRequests.AsNoTracking()
                .Select(c => new { c.Status, c.ImplementedSuccessfully }).ToListAsync();
            ViewBag.Total = all.Count;
            ViewBag.AwaitingApproval = all.Count(c => c.Status == ChangeStatus.SubmittedForApproval);
            ViewBag.Scheduled = all.Count(c => c.Status == ChangeStatus.Scheduled || c.Status == ChangeStatus.Approved);
            var done = all.Count(c => c.ImplementedSuccessfully != null);
            ViewBag.SuccessRate = done == 0 ? 0 : (int)Math.Round(100.0 * all.Count(c => c.ImplementedSuccessfully == true) / done);
            ViewBag.Status = status; ViewBag.Type = type; ViewBag.Q = q;

            return View(await query.OrderByDescending(c => c.CreatedAt).ToListAsync());
        }

        public async Task<IActionResult> Create()
        {
            await PopulateListsAsync();
            return View("Form", new ChangeRequest());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ChangeRequest input)
        {
            if (!ModelState.IsValid) { await PopulateListsAsync(); return View("Form", input); }
            input.CreatedById = Uid ?? 0;
            input.CreatedAt = DateTime.Now;
            input.Status = ChangeStatus.Draft;
            _db.ChangeRequests.Add(input);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Change {input.ChangeRef} created (draft).";
            return RedirectToAction(nameof(Details), new { id = input.Id });
        }

        public async Task<IActionResult> Edit(int id)
        {
            var c = await _db.ChangeRequests.FindAsync(id);
            if (c == null) return NotFound();
            await PopulateListsAsync();
            return View("Form", c);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ChangeRequest input)
        {
            var c = await _db.ChangeRequests.FindAsync(input.Id);
            if (c == null) return NotFound();
            if (!ModelState.IsValid) { await PopulateListsAsync(); return View("Form", input); }

            c.Title = input.Title; c.Description = input.Description; c.Type = input.Type;
            c.Risk = input.Risk; c.Impact = input.Impact;
            c.ImplementationPlan = input.ImplementationPlan; c.BackoutPlan = input.BackoutPlan; c.TestPlan = input.TestPlan;
            c.ScheduledStart = input.ScheduledStart; c.ScheduledEnd = input.ScheduledEnd;
            c.ConfigurationItemId = input.ConfigurationItemId; c.ProblemId = input.ProblemId;
            c.AssignedToId = input.AssignedToId; c.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Change {c.ChangeRef} updated.";
            return RedirectToAction(nameof(Details), new { id = c.Id });
        }

        public async Task<IActionResult> Details(int id)
        {
            var c = await _db.ChangeRequests
                .Include(x => x.ConfigurationItem).Include(x => x.AssignedTo)
                .Include(x => x.ApprovedBy).Include(x => x.CreatedBy).Include(x => x.Problem)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return NotFound();
            return View(c);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(int id)
        {
            var c = await _db.ChangeRequests.FindAsync(id);
            if (c == null) return NotFound();
            if (c.Status == ChangeStatus.Draft)
            {
                c.Status = ChangeStatus.SubmittedForApproval;
                c.UpdatedAt = DateTime.Now;
                await _db.SaveChangesAsync();
                TempData["Success"] = "Change submitted for approval.";
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, bool approve, string? notes)
        {
            var c = await _db.ChangeRequests.FindAsync(id);
            if (c == null) return NotFound();
            if (c.Status != ChangeStatus.SubmittedForApproval)
            { TempData["Error"] = "This change is not awaiting approval."; return RedirectToAction(nameof(Details), new { id }); }

            c.Status = approve ? ChangeStatus.Approved : ChangeStatus.Rejected;
            c.ApprovedById = Uid; c.ApprovedAt = DateTime.Now; c.ApprovalNotes = notes; c.UpdatedAt = DateTime.Now;
            if (!approve) c.ClosedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["Success"] = approve ? "Change approved." : "Change rejected.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetStatus(int id, ChangeStatus status)
        {
            var c = await _db.ChangeRequests.FindAsync(id);
            if (c == null) return NotFound();
            c.Status = status;
            c.UpdatedAt = DateTime.Now;
            if (status is ChangeStatus.Closed or ChangeStatus.Cancelled) c.ClosedAt ??= DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Change status set to {status}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Close(int id, bool successful, string? notes)
        {
            var c = await _db.ChangeRequests.FindAsync(id);
            if (c == null) return NotFound();
            c.ImplementedSuccessfully = successful;
            c.Status = successful ? ChangeStatus.Closed : ChangeStatus.Failed;
            c.ClosedAt = DateTime.Now; c.UpdatedAt = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(notes)) c.ApprovalNotes = (c.ApprovalNotes + "\nClosure: " + notes).Trim();
            await _db.SaveChangesAsync();
            TempData["Success"] = successful ? "Change closed as successful." : "Change closed as failed.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var c = await _db.ChangeRequests.FindAsync(id);
            if (c == null) return NotFound();
            _db.ChangeRequests.Remove(c);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Change {c.ChangeRef} deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
