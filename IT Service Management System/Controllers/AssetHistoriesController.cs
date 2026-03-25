using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
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
            if (ModelState.IsValid)
            {
                var history = new AssetHistory
                {
                    AssetId = vm.AssetId,
                    Date = vm.Date,
                    UserId = vm.UserId,
                    EventType = vm.EventType,
                    Condition = vm.Condition,
                    PerformedBy = vm.PerformedBy,
                    Remarks = vm.Remarks
                };

                _context.AssetHistories.Add(history);

                // 🔥 Update Asset Status Automatically
                var asset = _context.Assets.Find(vm.AssetId);

                if (vm.EventType == "Issued")
                    asset.Status = "Assigned";
                else if (vm.EventType == "Repair")
                    asset.Status = "Under Repair";
                else if (vm.EventType == "Stolen")
                    asset.Status = "Stolen";
                else if (vm.EventType == "Retired")
                    asset.Status = "Retired";
                else if (vm.EventType == "Returned")
                    asset.Status = "Available";

                _context.SaveChanges();

                return RedirectToAction("Details", "Assets", new { id = vm.AssetId });
            }

            vm.Users = _context.Users.ToList();
            return View(vm);
        }
    }
}