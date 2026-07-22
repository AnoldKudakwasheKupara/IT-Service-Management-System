using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    [IT_Service_Management_System.Filters.RoleAuthorize("Admin", "SystemsAdmin")]
    public class CannedResponsesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CannedResponsesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: CannedResponses
        public async Task<IActionResult> Index()
        {
            var responses = await _context.CannedResponses
                .OrderBy(c => c.Title)
                .ToListAsync();

            return View(responses);
        }

        // GET: CannedResponses/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: CannedResponses/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CannedResponse model)
        {
            if (!ModelState.IsValid)
                return View(model);

            model.CreatedAt = DateTime.Now;

            _context.CannedResponses.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Canned response created.";
            return RedirectToAction(nameof(Index));
        }

        // GET: CannedResponses/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var model = await _context.CannedResponses.FindAsync(id);

            if (model == null)
                return NotFound();

            return View(model);
        }

        // POST: CannedResponses/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CannedResponse model)
        {
            if (id != model.Id)
                return NotFound();

            if (!ModelState.IsValid)
                return View(model);

            var existing = await _context.CannedResponses.FindAsync(id);

            if (existing == null)
                return NotFound();

            existing.Title = model.Title;
            existing.Body = model.Body;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Canned response updated.";
            return RedirectToAction(nameof(Index));
        }

        // GET: CannedResponses/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var model = await _context.CannedResponses.FindAsync(id);

            if (model == null)
                return NotFound();

            return View(model);
        }

        // POST: CannedResponses/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var model = await _context.CannedResponses.FindAsync(id);

            if (model != null)
            {
                _context.CannedResponses.Remove(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Canned response deleted.";
            }
            else
            {
                TempData["Error"] = "Not found.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
