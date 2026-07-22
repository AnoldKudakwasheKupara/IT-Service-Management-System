using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    [IT_Service_Management_System.Filters.RoleAuthorize("Admin", "SystemsAdmin", "HR")]
    public class EngagementStayInterviewController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EngagementStayInterviewController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: EngagementStayInterview
        public async Task<IActionResult> Index()
        {
            return View(await _context.EngagementStayInterviews.ToListAsync());
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
                _context.EngagementStayInterviews.Remove(interview);

                await _context.SaveChangesAsync();
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