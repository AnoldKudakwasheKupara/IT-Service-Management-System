using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    public class PaymentsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PaymentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            await GeneratePayments();

            var payments = await _context.Payments
                .Include(p => p.PaymentSchedule)
                .ToListAsync();

            return View(payments);
        }

        public async Task GeneratePayments()
        {
            var today = DateTime.Today;

            var schedules = await _context.PaymentSchedules
                .Where(s => s.IsActive)
                .ToListAsync();

            foreach (var schedule in schedules)
            {
                // If it's time to generate a payment
                if (schedule.NextRunDate <= today)
                {
                    // جلوگیری duplicate
                    bool exists = await _context.Payments.AnyAsync(p =>
                        p.PaymentScheduleId == schedule.Id &&
                        p.DueDate == schedule.NextRunDate);

                    if (!exists)
                    {
                        var payment = new Payment
                        {
                            ServiceName = schedule.ServiceName,
                            Amount = schedule.Amount,
                            DueDate = schedule.NextRunDate,
                            Status = "Pending",
                            PaymentScheduleId = schedule.Id
                        };

                        _context.Payments.Add(payment);

                        // 🔁 Move to next cycle
                        if (schedule.Frequency == PaymentFrequency.Monthly)
                            schedule.NextRunDate = schedule.NextRunDate.AddMonths(1);

                        else if (schedule.Frequency == PaymentFrequency.Annual)
                            schedule.NextRunDate = schedule.NextRunDate.AddYears(1);
                    }
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}
