using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    public class TalentIdentificationController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TalentIdentificationController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: TalentIdentification
        public async Task<IActionResult> Index()
        {
            var records = await _context.TalentIdentifications
                .Where(x => !x.IsDeleted)
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync();

            return View(records);
        }

        // GET: TalentIdentification/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var record = await _context.TalentIdentifications
                .Include(x => x.DirectReports)
                .Include(x => x.DevelopmentActions)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (record == null)
                return NotFound();

            return View(record);
        }

        // GET: TalentIdentification/Create
        public IActionResult Create()
        {
            var model = new TalentIdentification();

            model.DirectReports.Add(new TalentDirectReportAssessment());

            model.DevelopmentActions.Add(new TalentDevelopmentAction());

            return View(model);
        }

        // POST: TalentIdentification/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TalentIdentification model)
        {
            if (ModelState.IsValid)
            {
                model.CreatedDate = DateTime.Now;

                _context.TalentIdentifications.Add(model);

                await _context.SaveChangesAsync();

                TempData["Success"] =
                    "Talent Identification record saved successfully.";

                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        // GET: TalentIdentification/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var record = await _context.TalentIdentifications
                .Include(x => x.DirectReports)
                .Include(x => x.DevelopmentActions)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (record == null)
                return NotFound();

            return View(record);
        }

        // POST: TalentIdentification/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            TalentIdentification model)
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
                        "Talent Identification record updated successfully.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TalentIdentificationExists(model.Id))
                        return NotFound();

                    throw;
                }

                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        // GET: TalentIdentification/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var record = await _context.TalentIdentifications
                .Include(x => x.DirectReports)
                .Include(x => x.DevelopmentActions)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (record == null)
                return NotFound();

            return View(record);
        }

        // POST: TalentIdentification/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var record =
                await _context.TalentIdentifications.FindAsync(id);

            if (record != null)
            {
                record.IsDeleted = true;

                await _context.SaveChangesAsync();
            }

            TempData["Success"] =
                "Talent Identification record deleted successfully.";

            return RedirectToAction(nameof(Index));
        }

        private bool TalentIdentificationExists(int id)
        {
            return _context.TalentIdentifications
                .Any(x => x.Id == id);
        }
    }

}