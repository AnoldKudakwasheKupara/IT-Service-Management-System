using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    public class ExitInterviewController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ExitInterviewController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: ExitInterview
        public async Task<IActionResult> Index()
        {
            return View(await _context.ExitInterviews.ToListAsync());
        }

        // GET: ExitInterview/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var interview = await _context.ExitInterviews
                .FirstOrDefaultAsync(m => m.Id == id);

            if (interview == null)
                return NotFound();

            return View(interview);
        }

        // GET: ExitInterview/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: ExitInterview/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ExitInterview model)
        {
            if (ModelState.IsValid)
            {
                model.CreatedDate = DateTime.Now;

                _context.Add(model);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Exit Interview saved successfully.";

                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        // GET: ExitInterview/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var interview = await _context.ExitInterviews.FindAsync(id);

            if (interview == null)
                return NotFound();

            return View(interview);
        }

        // POST: ExitInterview/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ExitInterview model)
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

                    TempData["Success"] = "Exit Interview updated successfully.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ExitInterviewExists(model.Id))
                        return NotFound();

                    throw;
                }

                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        // GET: ExitInterview/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var interview = await _context.ExitInterviews
                .FirstOrDefaultAsync(m => m.Id == id);

            if (interview == null)
                return NotFound();

            return View(interview);
        }

        // POST: ExitInterview/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var interview = await _context.ExitInterviews.FindAsync(id);

            if (interview != null)
            {
                _context.ExitInterviews.Remove(interview);
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "Exit Interview deleted successfully.";

            return RedirectToAction(nameof(Index));
        }

        private bool ExitInterviewExists(int id)
        {
            return _context.ExitInterviews.Any(e => e.Id == id);
        }
    }
}
