using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Models;
using IT_Service_Management_System.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    [IT_Service_Management_System.Filters.RoleAuthorize("Admin", "SystemsAdmin", "HR")]
    public class EngagementStayInterviewController : Controller
    {
        private readonly ApplicationDbContext _context;

        private readonly AuditService _audit;

        private const int PageSize = 25;

        public EngagementStayInterviewController(ApplicationDbContext context, AuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        // GET: EngagementStayInterview
        public async Task<IActionResult> Index(string? q, EngagementStatus? status,
            string? department, DateTime? from, DateTime? to, int page = 1)
        {
            IQueryable<EngagementStayInterview> query = _context.EngagementStayInterviews.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(x => x.NameAndSurname.Contains(term)
                    || x.JobTitle.Contains(term)
                    || x.ManagerName.Contains(term));
            }

            if (status.HasValue) query = query.Where(x => x.OverallStatus == status.Value);
            if (!string.IsNullOrWhiteSpace(department)) query = query.Where(x => x.Department == department);
            if (from.HasValue) query = query.Where(x => x.DiscussionDate >= from.Value);
            if (to.HasValue) query = query.Where(x => x.DiscussionDate <= to.Value);

            var (items, paging) = await query
                .OrderByDescending(x => x.DiscussionDate ?? x.CreatedDate)
                .PageAsync(page, PageSize);

            ViewBag.Paging = paging;
            ViewBag.Q = q; ViewBag.Status = status; ViewBag.Department = department;
            ViewBag.From = from; ViewBag.To = to;

            ViewBag.Departments = await _context.EngagementStayInterviews.AsNoTracking()
                .Where(x => x.Department != "")
                .Select(x => x.Department)
                .Distinct().OrderBy(d => d).ToListAsync();

            return View(items);
        }

        // GET: EngagementStayInterview/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var interview = await _context.EngagementStayInterviews
                .FirstOrDefaultAsync(x => x.Id == id);

            if (interview == null)
                return NotFound();

            return View(interview);
        }

        // GET: EngagementStayInterview/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: EngagementStayInterview/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EngagementStayInterview model)
        {
            if (ModelState.IsValid)
            {
                model.CreatedDate = DateTime.Now;

                _context.Add(model);
                await _context.SaveChangesAsync();

                await _audit.LogAsync("Created", nameof(EngagementStayInterview), model.Id,
                    $"Stay interview recorded for {model.NameAndSurname}");

                TempData["Success"] =
                    "Engagement Stay Interview saved successfully.";

                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        // GET: EngagementStayInterview/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var interview =
                await _context.EngagementStayInterviews.FindAsync(id);

            if (interview == null)
                return NotFound();

            return View(interview);
        }

        // POST: EngagementStayInterview/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            EngagementStayInterview model)
        {
            if (id != model.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    model.ModifiedDate = DateTime.Now;

                    _context.Update(model);

                    await _context.SaveChangesAsync();

                    await _audit.LogAsync("Updated", nameof(EngagementStayInterview), model.Id,
                        $"Stay interview amended for {model.NameAndSurname}");

                    TempData["Success"] =
                        "Engagement Stay Interview updated successfully.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EngagementStayInterviewExists(model.Id))
                        return NotFound();

                    throw;
                }

                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        // GET: EngagementStayInterview/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var interview =
                await _context.EngagementStayInterviews
                    .FirstOrDefaultAsync(x => x.Id == id);

            if (interview == null)
                return NotFound();

            return View(interview);
        }

        // POST: EngagementStayInterview/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var interview =
                await _context.EngagementStayInterviews.FindAsync(id);

            if (interview != null)
            {
                // Soft delete — the IsDeleted column already existed but was never used, so these
                // records were being destroyed outright. A stay interview is the organisation's
                // record of what an employee said while they were still there; it is worth keeping.
                interview.IsDeleted = true;
                interview.ModifiedDate = DateTime.Now;

                await _context.SaveChangesAsync();

                await _audit.LogAsync("Deleted", nameof(EngagementStayInterview), interview.Id,
                    $"Stay interview for {interview.NameAndSurname} withdrawn from view (retained)");
            }

            TempData["Success"] =
                "Engagement Stay Interview deleted successfully.";

            return RedirectToAction(nameof(Index));
        }

        private bool EngagementStayInterviewExists(int id)
        {
            return _context.EngagementStayInterviews
                .Any(x => x.Id == id);
        }
    }

}