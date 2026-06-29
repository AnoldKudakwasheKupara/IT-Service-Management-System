using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Models;
using IT_Service_Management_System.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
            var asset = _context.Assets
                .Include(a => a.User)
                .FirstOrDefault(a => a.Id == assetId);

            if (asset == null)
                return NotFound();

            var vm = new AssetHistoryViewModel
            {
                AssetId = assetId,
                Users = _context.Users.ToList(),
                Date = DateTime.Now,
                Condition = asset.Condition
            };

            LoadContext(asset);
            return View(vm);
        }

        // ➕ CREATE (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(AssetHistoryViewModel vm)
        {
            var asset = _context.Assets
                .Include(a => a.User)
                .FirstOrDefault(a => a.Id == vm.AssetId);

            if (asset == null)
                return NotFound();

            // Only an "Issued" event needs a target user.
            if (AssetWorkflow.RequiresUser(vm.EventType) && vm.UserId == null)
                ModelState.AddModelError(nameof(vm.UserId), "Please select the user the asset is issued to.");

            if (ModelState.IsValid)
            {
                // Attribute the event to the affected user: the recipient when issuing,
                // otherwise the asset's current holder (the person it is returned from /
                // repaired for). This keeps the event on that user's history even though
                // the asset's own holder is cleared below.
                int? eventUserId = vm.EventType == AssetWorkflow.Issued ? vm.UserId : asset.UserId;

                _context.AssetHistories.Add(new AssetHistory
                {
                    AssetId = vm.AssetId,
                    Date = vm.Date,
                    UserId = eventUserId,
                    EventType = vm.EventType,
                    Condition = vm.Condition,
                    PerformedBy = vm.PerformedBy,
                    Remarks = vm.Remarks
                });

                // Keep the asset's status and current holder in sync with the event.
                asset.Status = AssetWorkflow.StatusFor(vm.EventType);
                asset.UserId = AssetWorkflow.ResolveHolder(vm.EventType, asset.UserId, vm.UserId);
                asset.EventType = vm.EventType;
                asset.ActionType = vm.EventType;
                asset.Condition = vm.Condition;

                _context.SaveChanges();

                return RedirectToAction("Details", "Assets", new { id = vm.AssetId });
            }

            vm.Users = _context.Users.ToList();
            LoadContext(asset);
            return View(vm);
        }

        // Surface the asset being acted on to the view header.
        private void LoadContext(Asset asset)
        {
            ViewBag.AssetName = asset.ItemName;
            ViewBag.AssetSerial = asset.SerialNumber;
            ViewBag.AssetStatus = asset.Status ?? AssetWorkflow.InStock;
            ViewBag.CurrentHolder = asset.User != null ? $"{asset.User.FirstName} {asset.User.LastName}" : null;
        }
    }
}
