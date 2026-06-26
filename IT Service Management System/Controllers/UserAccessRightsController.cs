using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    public class UserAccessRightsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UserAccessRightsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var forms = await _context.UserAccessRights
                .OrderByDescending(x => x.Date)
                .ToListAsync();

            return View(forms);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var userAccessRight = await _context.UserAccessRights
                .Include(x => x.Users)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (userAccessRight == null)
                return NotFound();

            return View(userAccessRight);
        }

        // GET: UserAccessRights/Create
        public IActionResult Create()
        {
            var model = new UserAccessRight
            {
                Date = DateTime.Today
            };

            for (int i = 0; i < 10; i++)
            {
                model.Users.Add(new UserAccessRightItem());
            }

            return View(model);
        }

        // POST: UserAccessRights/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserAccessRight model)
        {
            // Remove blank rows
            model.Users = model.Users
                .Where(x => !string.IsNullOrWhiteSpace(x.UserName))
                .ToList();

            if (ModelState.IsValid)
            {
                _context.UserAccessRights.Add(model);

                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        // GET: UserAccessRights/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var model = await _context.UserAccessRights
                .Include(x => x.Users)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (model == null)
                return NotFound();

            return View(model);
        }

        // POST: UserAccessRights/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, UserAccessRight model)
        {
            if (id != model.Id)
                return NotFound();

            model.Users = model.Users
                .Where(x => !string.IsNullOrWhiteSpace(x.UserName))
                .ToList();

            if (!ModelState.IsValid)
                return View(model);

            var existing = await _context.UserAccessRights
                .Include(x => x.Users)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (existing == null)
                return NotFound();

            // Update header
            existing.SolutionSoftware = model.SolutionSoftware;
            existing.ProductionServerId = model.ProductionServerId;
            existing.ClientName = model.ClientName;
            existing.VersionReleaseNo = model.VersionReleaseNo;
            existing.SystemsAdmin = model.SystemsAdmin;
            existing.Date = model.Date;

            existing.AssignedBy = model.AssignedBy;
            existing.AssignedSignature = model.AssignedSignature;
            existing.AssignedDate = model.AssignedDate;

            existing.ReviewedBy = model.ReviewedBy;
            existing.ReviewedSignature = model.ReviewedSignature;
            existing.ReviewedDate = model.ReviewedDate;

            existing.ApprovedBy = model.ApprovedBy;
            existing.ApprovedSignature = model.ApprovedSignature;
            existing.ApprovedDate = model.ApprovedDate;

            // Remove existing user rows
            _context.UserAccessRightItems.RemoveRange(existing.Users);

            // Add the current rows from the form
            existing.Users.Clear();

            foreach (var user in model.Users)
            {
                existing.Users.Add(new UserAccessRightItem
                {
                    UserName = user.UserName,
                    UserManagement = user.UserManagement,
                    Initiate = user.Initiate,
                    Confirmation = user.Confirmation,
                    Approval = user.Approval,
                    AccountManagement = user.AccountManagement,
                    Reports = user.Reports
                });
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // GET: UserAccessRights/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var model = await _context.UserAccessRights
                .FirstOrDefaultAsync(x => x.Id == id);

            if (model == null)
                return NotFound();

            return View(model);
        }


        // POST: UserAccessRights/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var model = await _context.UserAccessRights
                .Include(x => x.Users)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (model != null)
            {
                _context.UserAccessRightItems.RemoveRange(model.Users);

                _context.UserAccessRights.Remove(model);

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private bool UserAccessRightExists(int id)
        {
            return _context.UserAccessRights.Any(e => e.Id == id);
        }
    }
}
