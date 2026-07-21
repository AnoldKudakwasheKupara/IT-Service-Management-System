using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models.Itsm;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    [IT_Service_Management_System.Filters.RoleAuthorize("Admin", "SystemsAdmin")]
    public class SlaCalendarsController : Controller
    {
        private readonly ApplicationDbContext _db;
        public SlaCalendarsController(ApplicationDbContext db) => _db = db;

        public async Task<IActionResult> Index() => View(await _db.SlaCalendars
            .Include(c => c.Holidays).Include(c => c.Policies)
            .OrderByDescending(c => c.IsDefault).ThenBy(c => c.Name).ToListAsync());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(int id, string name, TimeSpan workDayStart, TimeSpan workDayEnd,
            int[]? workingDays, bool isDefault)
        {
            if (string.IsNullOrWhiteSpace(name) || workDayEnd <= workDayStart)
            {
                TempData["Error"] = "Provide a name and a valid working-hours window.";
                return RedirectToAction(nameof(Index));
            }
            var mask = (workingDays ?? Array.Empty<int>()).Aggregate(0, (current, day) => current | (1 << day));
            if (mask == 0)
            {
                TempData["Error"] = "Select at least one working day.";
                return RedirectToAction(nameof(Index));
            }

            var calendar = id == 0 ? new SlaCalendar { CreatedAt = DateTime.Now } : await _db.SlaCalendars.FindAsync(id);
            if (calendar == null) return NotFound();
            if (isDefault)
                await _db.SlaCalendars.Where(c => c.IsDefault && c.Id != id)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsDefault, false));
            calendar.Name = name.Trim();
            calendar.WorkDayStart = workDayStart;
            calendar.WorkDayEnd = workDayEnd;
            calendar.WorkingDaysMask = mask;
            calendar.IsDefault = isDefault;
            if (id == 0) _db.SlaCalendars.Add(calendar);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"SLA calendar '{calendar.Name}' saved.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddHoliday(int calendarId, string name, DateOnly date)
        {
            if (!await _db.SlaCalendars.AnyAsync(c => c.Id == calendarId)) return NotFound();
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Holiday name is required.";
                return RedirectToAction(nameof(Index));
            }
            var existing = await _db.SlaHolidays.FirstOrDefaultAsync(h => h.SlaCalendarId == calendarId && h.Date == date);
            if (existing == null)
                _db.SlaHolidays.Add(new SlaHoliday { SlaCalendarId = calendarId, Name = name.Trim(), Date = date });
            else
                existing.Name = name.Trim();
            await _db.SaveChangesAsync();
            TempData["Success"] = "Holiday saved.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteHoliday(int id)
        {
            var holiday = await _db.SlaHolidays.FindAsync(id);
            if (holiday == null) return NotFound();
            _db.SlaHolidays.Remove(holiday);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Holiday removed.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var calendar = await _db.SlaCalendars.Include(c => c.Policies).FirstOrDefaultAsync(c => c.Id == id);
            if (calendar == null) return NotFound();
            if (calendar.Policies.Count > 0)
            {
                TempData["Error"] = "This calendar is used by an SLA policy and cannot be deleted.";
                return RedirectToAction(nameof(Index));
            }
            _db.SlaCalendars.Remove(calendar);
            await _db.SaveChangesAsync();
            TempData["Success"] = "SLA calendar deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
