using IT_Service_Management_System.Services;
using Microsoft.AspNetCore.Mvc;

namespace IT_Service_Management_System.Controllers
{
    [IT_Service_Management_System.Filters.RoleAuthorize("Admin", "SystemsAdmin")]
    public class BackupController : Controller
    {
        private readonly BackupService _backup;
        private readonly ConfigurationService _config;

        public BackupController(BackupService backup, ConfigurationService config)
        {
            _backup = backup;
            _config = config;
        }

        public IActionResult Index()
        {
            ViewBag.Config = _config.Get();
            ViewBag.Backups = _backup.ListBackups();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BackupNow()
        {
            var (ok, message) = await _backup.BackupNowAsync();
            if (ok) TempData["Success"] = message; else TempData["Error"] = message;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verify(string fileName)
        {
            var (ok, message) = await _backup.VerifyAsync(fileName);
            if (ok) TempData["Success"] = message; else TempData["Error"] = message;
            return RedirectToAction(nameof(Index));
        }
    }
}
