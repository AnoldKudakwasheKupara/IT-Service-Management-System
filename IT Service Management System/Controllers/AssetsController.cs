using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Models;
using IT_Service_Management_System.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    [IT_Service_Management_System.Filters.RoleAuthorize("Admin", "SystemsAdmin")]
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
            // Only an "Issued" asset needs a user; "In Stock" and others don't.
            if (vm.Asset != null && AssetWorkflow.RequiresUser(vm.Asset.ActionType) && vm.Asset.UserId == null)
                ModelState.AddModelError("Asset.UserId", "Please select the user the asset is issued to.");

            if (ModelState.IsValid && vm.Asset != null)
            {
                // Prevent duplicate serial numbers.
                if (_context.Assets.Any(a => a.SerialNumber == vm.Asset.SerialNumber))
                {
                    ModelState.AddModelError("Asset.SerialNumber", "An asset with this serial number already exists.");
                    vm.Users = _context.Users.ToList();
                    return View(vm);
                }

                // The initial action determines status and current holder.
                vm.Asset.EventType = vm.Asset.ActionType;
                vm.Asset.Status = AssetWorkflow.StatusFor(vm.Asset.ActionType);
                vm.Asset.UserId = AssetWorkflow.ResolveHolder(vm.Asset.ActionType, vm.Asset.UserId, vm.Asset.UserId);

                _context.Assets.Add(vm.Asset);
                _context.SaveChanges();

                // Record the opening history entry so the asset has a timeline
                // (use the chosen date with the current time so it has a real timestamp).
                _context.AssetHistories.Add(new AssetHistory
                {
                    AssetId = vm.Asset.Id,
                    Date = vm.Asset.Date.Date.Add(DateTime.Now.TimeOfDay),
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

            if (AssetWorkflow.RequiresUser(vm.Asset.ActionType) && vm.Asset.UserId == null)
                ModelState.AddModelError("Asset.UserId", "Please select the user the asset is issued to.");

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
                asset.ItemName = vm.Asset.ItemName;
                asset.SerialNumber = vm.Asset.SerialNumber;
                asset.Condition = vm.Asset.Condition;
                asset.IssuedBy = vm.Asset.IssuedBy;
                asset.Remarks = vm.Asset.Remarks;

                // Keep action, status and holder consistent with the workflow.
                asset.ActionType = vm.Asset.ActionType;
                asset.EventType = vm.Asset.ActionType;
                asset.Status = AssetWorkflow.StatusFor(vm.Asset.ActionType);
                asset.UserId = AssetWorkflow.ResolveHolder(vm.Asset.ActionType, asset.UserId, vm.Asset.UserId);

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
    }
}