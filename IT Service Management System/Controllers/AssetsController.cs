using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using IT_Service_Management_System.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    public class AssetsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AssetsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 🔍 INDEX + SEARCH + SORT
        public IActionResult Index(string search)
        {
            var query = _context.Assets
                .Include(a => a.User)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(a =>
                    a.ItemName.Contains(search) ||
                    a.SerialNumber.Contains(search) ||
                    a.User.FirstName.Contains(search) ||
                    a.User.LastName.Contains(search));
            }

            var data = query
                .OrderByDescending(a => a.Date)
                .ToList();

            return View(data);
        }

        // 👁️ DETAILS
        public IActionResult Details(int? id)
        {
            if (id == null) return NotFound();

            var asset = _context.Assets
                .Include(a => a.User)
                .FirstOrDefault(a => a.Id == id);

            if (asset == null) return NotFound();

            return View(asset);
        }

        [HttpGet]
        public IActionResult Create()
        {
            var vm = new AssetCreateViewModel
            {
                Asset = new Asset(),
                Users = _context.Users.ToList()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(AssetCreateViewModel vm)
        {
            if (ModelState.IsValid)
            {
                _context.Add(vm.Asset);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }

            // reload users if validation fails
            vm.Users = _context.Users.ToList();

            return View(vm);
        }

        // ✏️ EDIT (GET)
        public IActionResult Edit(int? id)
        {
            if (id == null) return NotFound();

            var asset = _context.Assets.Find(id);
            if (asset == null) return NotFound();

            LoadUsers();
            return View(asset);
        }

        // ✏️ EDIT (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, Asset asset)
        {
            if (id != asset.Id) return NotFound();

            if (ModelState.IsValid)
            {
                _context.Update(asset);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }

            LoadUsers();
            return View(asset);
        }

        // ❌ DELETE (GET)
        public IActionResult Delete(int? id)
        {
            if (id == null) return NotFound();

            var asset = _context.Assets
                .Include(a => a.User)
                .FirstOrDefault(a => a.Id == id);

            if (asset == null) return NotFound();

            return View(asset);
        }

        // ❌ DELETE (POST)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var asset = _context.Assets.Find(id);

            if (asset != null)
            {
                _context.Assets.Remove(asset);
                _context.SaveChanges();
            }

            return RedirectToAction(nameof(Index));
        }

        // 🔧 HELPER: Load Users Dropdown
        private void LoadUsers()
        {
            ViewBag.Users = new SelectList(
                _context.Users
                    .Where(u => u.IsActive)
                    .ToList(),
                "Id",
                "FullName"
            );
        }
    }
}