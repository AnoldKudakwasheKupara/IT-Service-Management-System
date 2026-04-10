using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    public class PaymentScheduleController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PaymentScheduleController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ✅ INDEX
        public async Task<IActionResult> Index()
        {
            var schedules = await _context.PaymentSchedules.ToListAsync();
            return View(schedules);
        }

        // ✅ CREATE (GET)
        public IActionResult Create()
        {
            return View();
        }

        // ✅ CREATE (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PaymentSchedule model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // 🔥 VALIDATION
            if (model.Frequency == PaymentFrequency.Monthly && model.DayOfMonth == null)
            {
                ModelState.AddModelError("", "Day of Month is required for Monthly payments.");
                return View(model);
            }

            if (model.Frequency == PaymentFrequency.Annual && model.FixedDate == null)
            {
                ModelState.AddModelError("", "Fixed Date is required for Annual payments.");
                return View(model);
            }

            // ✅ Calculate Next Run
            model.NextRunDate = CalculateNextRun(model);
            model.IsActive = true;

            _context.Add(model);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // ✅ DETAILS
        [HttpPost]
                public async Task<IActionResult> Details(int id)
        {
            var schedule = await _context.PaymentSchedules
                .FirstOrDefaultAsync(m => m.Id == id);
            if (schedule == null) return NotFound();
            return View(schedule);
        }

        // ✅ EDIT (GET)
        public async Task<IActionResult> Edit(int id)
        {
            var schedule = await _context.PaymentSchedules.FindAsync(id);
            if (schedule == null) return NotFound();

            return View(schedule);
        }

        // ✅ EDIT (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PaymentSchedule model)
        {
            if (id != model.Id) return NotFound();

            if (!ModelState.IsValid)
                return View(model);

            // 🔥 VALIDATION
            if (model.Frequency == PaymentFrequency.Monthly && model.DayOfMonth == null)
            {
                ModelState.AddModelError("", "Day of Month is required for Monthly payments.");
                return View(model);
            }

            if (model.Frequency == PaymentFrequency.Annual && model.FixedDate == null)
            {
                ModelState.AddModelError("", "Fixed Date is required for Annual payments.");
                return View(model);
            }

            try
            {
                model.NextRunDate = CalculateNextRun(model);
                _context.Update(model);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.PaymentSchedules.Any(e => e.Id == model.Id))
                    return NotFound();
                else
                    throw;
            }

            return RedirectToAction(nameof(Index));
        }

        // ✅ DELETE (GET)
        public async Task<IActionResult> Delete(int id)
        {
            var schedule = await _context.PaymentSchedules
                .FirstOrDefaultAsync(m => m.Id == id);

            if (schedule == null) return NotFound();

            return View(schedule);
        }

        // ✅ DELETE (POST)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var schedule = await _context.PaymentSchedules.FindAsync(id);
            if (schedule != null)
            {
                _context.PaymentSchedules.Remove(schedule);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // 🔁 CORE LOGIC: Calculate Next Run Date
        private DateTime CalculateNextRun(PaymentSchedule s)
        {
            var today = DateTime.Today;

            if (s.Frequency == PaymentFrequency.Monthly)
            {
                var day = s.DayOfMonth ?? 1;

                var next = new DateTime(today.Year, today.Month, day);

                if (next < today)
                    next = next.AddMonths(1);

                return next;
            }

            if (s.Frequency == PaymentFrequency.Annual && s.FixedDate.HasValue)
            {
                var date = s.FixedDate.Value;

                var next = new DateTime(today.Year, date.Month, date.Day);

                if (next < today)
                    next = next.AddYears(1);

                return next;
            }

            return today;
        }
    }
}