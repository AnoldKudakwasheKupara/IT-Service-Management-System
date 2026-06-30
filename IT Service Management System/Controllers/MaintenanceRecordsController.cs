using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    [IT_Service_Management_System.Filters.RoleAuthorize("Admin", "SystemsAdmin")]
    public class MaintenanceRecordsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MaintenanceRecordsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: MaintenanceRecords
        public async Task<IActionResult> Index()
        {
            return View(await _context.MaintenanceRecords
                .OrderByDescending(m => m.MaintenanceDate)
                .ToListAsync());
        }

        // GET: MaintenanceRecords/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var maintenance = await _context.MaintenanceRecords
                .FirstOrDefaultAsync(m => m.Id == id);

            if (maintenance == null)
                return NotFound();

            return View(maintenance);
        }

        // GET: MaintenanceRecords/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: MaintenanceRecords/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MaintenanceRecord maintenanceRecord)
        {
            if (ModelState.IsValid)
            {
                maintenanceRecord.CreatedAt = DateTime.Now;

                _context.Add(maintenanceRecord);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Maintenance record created.";
                return RedirectToAction(nameof(Index));
            }

            return View(maintenanceRecord);
        }

        // GET: MaintenanceRecords/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var maintenanceRecord = await _context.MaintenanceRecords.FindAsync(id);

            if (maintenanceRecord == null)
                return NotFound();

            return View(maintenanceRecord);
        }

        // POST: MaintenanceRecords/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, MaintenanceRecord maintenanceRecord)
        {
            if (id != maintenanceRecord.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                var existing = await _context.MaintenanceRecords.FindAsync(maintenanceRecord.Id);
                if (existing == null)
                    return NotFound();

                existing.AssetName = maintenanceRecord.AssetName;
                existing.EmployeeName = maintenanceRecord.EmployeeName;
                existing.MaintenanceDate = maintenanceRecord.MaintenanceDate;
                existing.MaintenanceType = maintenanceRecord.MaintenanceType;
                existing.ProblemDescription = maintenanceRecord.ProblemDescription;
                existing.WorkDone = maintenanceRecord.WorkDone;
                existing.PartsReplaced = maintenanceRecord.PartsReplaced;
                existing.SoftwareInstalled = maintenanceRecord.SoftwareInstalled;
                existing.Comments = maintenanceRecord.Comments;
                existing.NextMaintenanceDate = maintenanceRecord.NextMaintenanceDate;
                // CreatedAt preserved from existing entity

                await _context.SaveChangesAsync();

                TempData["Success"] = "Maintenance record updated.";
                return RedirectToAction(nameof(Index));
            }

            return View(maintenanceRecord);
        }

        // GET: MaintenanceRecords/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var maintenanceRecord = await _context.MaintenanceRecords
                .FirstOrDefaultAsync(m => m.Id == id);

            if (maintenanceRecord == null)
                return NotFound();

            return View(maintenanceRecord);
        }

        // POST: MaintenanceRecords/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var maintenanceRecord = await _context.MaintenanceRecords.FindAsync(id);

            if (maintenanceRecord != null)
            {
                _context.MaintenanceRecords.Remove(maintenanceRecord);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Maintenance record deleted.";
            }
            else
            {
                TempData["Error"] = "Maintenance record not found.";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool MaintenanceRecordExists(int id)
        {
            return _context.MaintenanceRecords.Any(e => e.Id == id);
        }
    }
}