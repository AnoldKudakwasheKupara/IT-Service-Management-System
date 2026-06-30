using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    [IT_Service_Management_System.Filters.RoleAuthorize("Admin", "SystemsAdmin")]
    public class PaymentsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PaymentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Read-only list. Generation is an explicit POST action (a GET must not mutate data).
        public async Task<IActionResult> Index()
        {
            var payments = await _context.Payments
                .Include(p => p.PaymentSchedule)
                .OrderByDescending(p => p.DueDate)
                .ToListAsync();

            return View(payments);
        }

        // Generates any due payments from active schedules and advances their next run date.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Generate()
        {
            var created = await GeneratePayments();
            TempData["Success"] = created > 0
                ? $"{created} due payment(s) generated."
                : "No new payments were due.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<int> GeneratePayments()
        {
            var today = DateTime.Today;
            var created = 0;

            var schedules = await _context.PaymentSchedules
                .Where(s => s.IsActive)
                .ToListAsync();

            foreach (var schedule in schedules)
            {
                if (schedule.NextRunDate <= today)
                {
                    // Guard against duplicate generation for the same due date.
                    bool exists = await _context.Payments.AnyAsync(p =>
                        p.PaymentScheduleId == schedule.Id &&
                        p.DueDate == schedule.NextRunDate);

                    if (!exists)
                    {
                        _context.Payments.Add(new Payment
                        {
                            ServiceName = schedule.ServiceName,
                            Amount = schedule.Amount,
                            DueDate = schedule.NextRunDate,
                            Status = "Pending",
                            PaymentScheduleId = schedule.Id
                        });
                        created++;

                        if (schedule.Frequency == PaymentFrequency.Monthly)
                            schedule.NextRunDate = schedule.NextRunDate.AddMonths(1);
                        else if (schedule.Frequency == PaymentFrequency.Annual)
                            schedule.NextRunDate = schedule.NextRunDate.AddYears(1);
                    }
                }
            }

            await _context.SaveChangesAsync();
            return created;
        }
    }
}
