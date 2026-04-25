using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace IT_Service_Management_System.Controllers
{
    public class ActivityCategoriesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ActivityCategoriesController(ApplicationDbContext context)
        {
            _context = context;
        }

        private IActionResult CheckAccess()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return RedirectToAction("Login", "Account");

            if (HttpContext.Session.GetString("UserRole") != "Admin")
                return Forbid();

            return null;
        }

        public async Task<IActionResult> Index()
        {
            var access = CheckAccess();
            if (access != null) return access;

            var categories = await _context.ActivityCategories
                .Include(c => c.Activities)
                .ToListAsync();

            return View(categories);
        }

        public async Task<IActionResult> Details(int id)
        {
            var access = CheckAccess();
            if (access != null) return access;

            var category = await _context.ActivityCategories
                .Include(c => c.Activities) 
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null) return NotFound();

            return View(category);
        }

        public IActionResult Create()
        {
            var access = CheckAccess();
            if (access != null) return access;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(ActivityCategory category)
        {
            var access = CheckAccess();
            if (access != null) return access;

            if (!ModelState.IsValid)
                return View(category);

            var exists = await _context.ActivityCategories
                .AnyAsync(c => c.Name == category.Name);

            if (exists)
            {
                ModelState.AddModelError("Name", "Category already exists.");
                return View(category);
            }

            _context.ActivityCategories.Add(category);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Edit(int id)
        {
            var access = CheckAccess();
            if (access != null) return access;

            var category = await _context.ActivityCategories.FindAsync(id);

            if (category == null) return NotFound();

            return View(category);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(ActivityCategory category)
        {
            var access = CheckAccess();
            if (access != null) return access;

            if (!ModelState.IsValid)
                return View(category);

            var exists = await _context.ActivityCategories
                .AnyAsync(c => c.Name == category.Name && c.Id != category.Id);

            if (exists)
            {
                ModelState.AddModelError("Name", "Category already exists.");
                return View(category);
            }

            _context.ActivityCategories.Update(category);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Delete(int id)
        {
            var access = CheckAccess();
            if (access != null) return access;

            var category = await _context.ActivityCategories.FindAsync(id);

            if (category == null) return NotFound();

            return View(category);
        }

        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var access = CheckAccess();
            if (access != null) return access;

            var category = await _context.ActivityCategories
                .Include(c => c.Activities)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null) return NotFound();

            if (category.Activities.Any())
            {
                ModelState.AddModelError("", "Cannot delete category with existing activities.");
                return View(category);
            }

            _context.ActivityCategories.Remove(category);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }
    }
}