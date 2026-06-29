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
                    a.User!.FirstName.Contains(search) ||
                    a.User!.LastName.Contains(search));
            }

            var data = query
                .OrderByDescending(a => a.Date)
                .ToList();

            return View(data);
        }

        public IActionResult Details(int id)
        {
            var asset = _context.Assets
                .Include(a => a.History)
                    .ThenInclude(h => h.User)
                .FirstOrDefault(a => a.Id == id);

            if (asset == null)
                return NotFound();

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
            if (ModelState.IsValid && vm.Asset != null)
            {
                // Prevent duplicate serial numbers.
                if (_context.Assets.Any(a => a.SerialNumber == vm.Asset.SerialNumber))
                {
                    ModelState.AddModelError("Asset.SerialNumber", "An asset with this serial number already exists.");
                    vm.Users = _context.Users.ToList();
                    return View(vm);
                }

                // The initial action determines status; keep EventType in sync.
                vm.Asset.EventType = vm.Asset.ActionType;
                vm.Asset.Status = GetStatusFromEvent(vm.Asset.ActionType);

                _context.Assets.Add(vm.Asset);
                _context.SaveChanges();

                // Record the opening history entry so the asset has a timeline.
                _context.AssetHistories.Add(new AssetHistory
                {
                    AssetId = vm.Asset.Id,
                    Date = vm.Asset.Date,
                    UserId = vm.Asset.UserId,
                    EventType = vm.Asset.ActionType,
                    Condition = vm.Asset.Condition,
                    PerformedBy = vm.Asset.IssuedBy,
                    Remarks = vm.Asset.Remarks
                });
                _context.SaveChanges();

                return RedirectToAction(nameof(Index));
            }

            // reload users if validation fails
            vm.Users = _context.Users.ToList();

            return View(vm);
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var asset = _context.Assets.Find(id);

            if (asset == null)
                return NotFound();

            var vm = new AssetCreateViewModel
            {
                Asset = asset,
                Users = _context.Users.ToList()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, AssetCreateViewModel vm)
        {
            if (vm.Asset == null || id != vm.Asset.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                // Prevent duplicate serial numbers (excluding this asset).
                if (_context.Assets.Any(a => a.SerialNumber == vm.Asset.SerialNumber && a.Id != id))
                {
                    ModelState.AddModelError("Asset.SerialNumber", "Another asset with this serial number already exists.");
                    vm.Users = _context.Users.ToList();
                    return View(vm);
                }

                var asset = _context.Assets.Find(id);

                if (asset == null)
                    return NotFound();

                asset.Date = vm.Asset.Date;
                asset.UserId = vm.Asset.UserId;
                asset.ItemName = vm.Asset.ItemName;
                asset.SerialNumber = vm.Asset.SerialNumber;

                asset.ActionType = vm.Asset.ActionType;

                asset.Condition = vm.Asset.Condition;
                asset.IssuedBy = vm.Asset.IssuedBy;
                asset.Remarks = vm.Asset.Remarks;

                _context.SaveChanges();

                return RedirectToAction(nameof(Index));
            }

            vm.Users = _context.Users.ToList();
            return View(vm);
        }

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

        private string GetStatusFromEvent(string eventType)
        {
            return eventType switch
            {
                "Issued" => "Assigned",
                "Repair" => "Under Repair",
                "Stolen" => "Stolen",
                "Retired" => "Retired",
                "Returned" => "Available",
                _ => "Available"
            };
        }

        private void LogAssetEvent(int assetId, string eventType, int? userId, string performedBy, string remarks, string condition)
        {
            var asset = _context.Assets.Find(assetId);

            if (asset == null) return;

            // 🔥 Update status automatically
            asset.Status = eventType switch
            {
                "Issued" => "Assigned",
                "Returned" => "Available",
                "Repair" => "Under Repair",
                "Stolen" => "Stolen",
                "Retired" => "Retired",
                _ => asset.Status
            };

            // 🔁 Create history automatically
            var history = new AssetHistory
            {
                AssetId = assetId,
                Date = DateTime.Now,
                UserId = userId,
                EventType = eventType,
                Condition = condition,
                PerformedBy = performedBy,
                Remarks = remarks
            };

            _context.AssetHistories.Add(history);
        }
    }
}