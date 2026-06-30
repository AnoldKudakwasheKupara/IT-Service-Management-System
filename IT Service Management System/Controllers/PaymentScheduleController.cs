using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[IT_Service_Management_System.Filters.RoleAuthorize("Admin", "SystemsAdmin")]
public class PaymentScheduleController : Controller
{
    private readonly ApplicationDbContext _context;

    public PaymentScheduleController(ApplicationDbContext context)
    {
        _context = context;
    }

    // INDEX
    public async Task<IActionResult> Index()
    {
        return View(await _context.PaymentSchedules.ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var schedule = await _context.PaymentSchedules
            .FirstOrDefaultAsync(m => m.Id == id);

        if (schedule == null)
            return NotFound();

        return View(schedule);
    }

    // CREATE (GET)
    public IActionResult Create()
    {
        return View();
    }

    // CREATE (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PaymentSchedule model)
    {
        if (!ModelState.IsValid)
            return View(model);

        // 🔥 SIMPLE LOGIC
        model.NextRunDate = CalculateNextRun(model);
        model.IsActive = true;

        _context.Add(model);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Payment schedule created.";
        return RedirectToAction(nameof(Index));
    }

    // EDIT (GET)
    public async Task<IActionResult> Edit(int id)
    {
        var schedule = await _context.PaymentSchedules.FindAsync(id);
        if (schedule == null)
            return NotFound();

        return View(schedule);
    }

    // EDIT (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PaymentSchedule model)
    {
        if (id != model.Id)
            return NotFound();

        if (!ModelState.IsValid)
            return View(model);

        var existing = await _context.PaymentSchedules.FindAsync(model.Id);
        if (existing == null)
            return NotFound();

        existing.ServiceName = model.ServiceName;
        existing.Amount = model.Amount;
        existing.PaymentDate = model.PaymentDate;
        existing.Frequency = model.Frequency;
        existing.Departments = model.Departments;

        // 🔥 Recalculate Next Run Date
        existing.NextRunDate = CalculateNextRun(model);
        // IsActive preserved from existing entity (not editable via the form)

        await _context.SaveChangesAsync();

        TempData["Success"] = "Payment schedule updated.";
        return RedirectToAction(nameof(Index));
    }


    // DELETE
    public async Task<IActionResult> Delete(int id)
    {
        var schedule = await _context.PaymentSchedules.FindAsync(id);
        if (schedule == null) return NotFound();

        return View(schedule);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var schedule = await _context.PaymentSchedules.FindAsync(id);

        if (schedule != null)
        {
            _context.PaymentSchedules.Remove(schedule);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Payment schedule deleted.";
        }
        else
        {
            TempData["Error"] = "Payment schedule not found.";
        }

        return RedirectToAction(nameof(Index));
    }

    private DateTime CalculateNextRun(PaymentSchedule s)
    {
        var today = DateTime.Today;

        if (s.Frequency == PaymentFrequency.Monthly)
        {
            var next = s.PaymentDate;

            while (next < today)
            {
                next = next.AddMonths(1);
            }

            return next;
        }

        if (s.Frequency == PaymentFrequency.Annual)
        {
            var next = s.PaymentDate;

            while (next < today)
            {
                next = next.AddYears(1);
            }

            return next;
        }

        return today;
    }
}