using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using IT_Service_Management_System.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace IT_Service_Management_System.Controllers
{
    public class AssetHistoriesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AssetHistoriesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ➕ CREATE (GET)
        public IActionResult Create(int assetId)
        {
            var vm = new AssetHistoryViewModel
            {
                AssetId = assetId,
                Users = _context.Users.ToList(),
                Date = DateTime.Now
            };

            return View(vm);
        }

        // ➕ CREATE (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(AssetHistoryViewModel vm)
        {
            // An "Issued" event must specify which user receives the asset.
            if (vm.EventType == "Issued" && vm.UserId == null)
                ModelState.AddModelError(nameof(vm.UserId), "Please select the user the asset is issued to.");

            if (ModelState.IsValid)
            {
                var asset = _context.Assets.Find(vm.AssetId);

                if (asset == null)
                {
                    ModelState.AddModelError("", "The asset could not be found.");
                    vm.Users = _context.Users.ToList();
                    return View(vm);
                }

                _context.AssetHistories.Add(new AssetHistory
                {
                    AssetId = vm.AssetId,
                    Date = vm.Date,
                    UserId = vm.UserId,
                    EventType = vm.EventType,
                    Condition = vm.Condition,
                    PerformedBy = vm.PerformedBy,
                    Remarks = vm.Remarks
                });

                // 🔥 Keep the asset's status in sync with the event.
                asset.Status = vm.EventType switch
                {
                    "Issued" => "Assigned",
                    "Returned" => "Available",
                    "Repair" => "Under Repair",
                    "Stolen" => "Stolen",
                    "Retired" => "Retired",
                    _ => asset.Status
                };

                // 🔁 Keep the asset's current holder in sync with the event.
                if (vm.EventType == "Issued")
                    asset.UserId = vm.UserId;
                else if (vm.EventType is "Returned" or "Retired" or "Stolen")
                    asset.UserId = null;

                asset.EventType = vm.EventType;
                asset.Condition = vm.Condition;

                _context.SaveChanges();

                return RedirectToAction("Details", "Assets", new { id = vm.AssetId });
            }

            vm.Users = _context.Users.ToList();
            return View(vm);
        }
    }
}