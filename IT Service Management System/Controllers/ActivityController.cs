using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    public class ActivityController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ActivityController(ApplicationDbContext context)
        {
            _context = context;
        }

        private IActionResult CheckAccess()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return RedirectToAction("Login", "Account");

            return null;
        }

        public async Task<IActionResult> Index()
        {
            var access = CheckAccess();
            if (access != null) return access;

            var userId = HttpContext.Session.GetInt32("UserId");

            var today = DateTime.Today;

            var activities = await _context.Activities
                .Include(a => a.Category)
                .Where(a => a.UserId == userId.ToString() && a.StartTime.Date == today)
                .OrderByDescending(a => a.StartTime)
                .ToListAsync();

            return View(activities);
        }

        public async Task<IActionResult> Details(int id)
        {
            var access = CheckAccess();
            if (access != null) return access;

            var userId = HttpContext.Session.GetInt32("UserId");

            var activity = await _context.Activities
                .Include(a => a.Category)
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId.ToString());

            if (activity == null) return NotFound();

            return View(activity);
        }

        public IActionResult Create()
        {
            var access = CheckAccess();
            if (access != null) return access;

            ViewData["CategoryId"] = new SelectList(_context.ActivityCategories, "Id", "Name");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(Activity activity)
        {
            var access = CheckAccess();
            if (access != null) return access;

            if (!ModelState.IsValid)
            {
                ViewData["CategoryId"] = new SelectList(_context.ActivityCategories, "Id", "Name", activity.CategoryId);
                return View(activity);
            }

            var userId = HttpContext.Session.GetInt32("UserId");

            activity.UserId = userId.ToString();
            activity.CreatedAt = DateTime.Now;

            _context.Activities.Add(activity);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Edit(int id)
        {
            var access = CheckAccess();
            if (access != null) return access;

            var userId = HttpContext.Session.GetInt32("UserId");

            var activity = await _context.Activities
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId.ToString());

            if (activity == null) return NotFound();

            ViewData["CategoryId"] = new SelectList(_context.ActivityCategories, "Id", "Name", activity.CategoryId);

            return View(activity);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Activity activity)
        {
            var access = CheckAccess();
            if (access != null) return access;

            if (!ModelState.IsValid)
            {
                ViewData["CategoryId"] = new SelectList(_context.ActivityCategories, "Id", "Name", activity.CategoryId);
                return View(activity);
            }

            var userId = HttpContext.Session.GetInt32("UserId");

            var existing = await _context.Activities
                .FirstOrDefaultAsync(a => a.Id == activity.Id && a.UserId == userId.ToString());

            if (existing == null) return NotFound();

            existing.Title = activity.Title;
            existing.Description = activity.Description;
            existing.StartTime = activity.StartTime;
            existing.EndTime = activity.EndTime;
            existing.CategoryId = activity.CategoryId;

            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Delete(int id)
        {
            var access = CheckAccess();
            if (access != null) return access;

            var userId = HttpContext.Session.GetInt32("UserId");

            var activity = await _context.Activities
                .Include(a => a.Category)
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId.ToString());

            if (activity == null) return NotFound();

            return View(activity);
        }

        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var access = CheckAccess();
            if (access != null) return access;

            var userId = HttpContext.Session.GetInt32("UserId");

            var activity = await _context.Activities
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId.ToString());

            if (activity == null) return NotFound();

            _context.Activities.Remove(activity);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }
        public async Task<IActionResult> WeeklyReport()
        {
            var access = CheckAccess();
            if (access != null) return access;

            var userId = HttpContext.Session.GetInt32("UserId");

            var today = DateTime.Today;
            var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
            var endOfWeek = startOfWeek.AddDays(7);

            var activities = await _context.Activities
                .Include(a => a.Category)
                .Where(a => a.UserId == userId.ToString()
                    && a.StartTime >= startOfWeek
                    && a.StartTime < endOfWeek)
                .OrderBy(a => a.StartTime)
                .ToListAsync();

            var grouped = activities
                .GroupBy(a => a.StartTime.Date)
                .OrderBy(g => g.Key)
                .ToList();

            return View(grouped);
        }
    }
}