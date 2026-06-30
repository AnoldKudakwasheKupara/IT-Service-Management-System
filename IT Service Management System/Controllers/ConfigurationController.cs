using IT_Service_Management_System.Models;
using IT_Service_Management_System.Services;
using Microsoft.AspNetCore.Mvc;

namespace IT_Service_Management_System.Controllers
{
    [IT_Service_Management_System.Filters.RoleAuthorize("Admin", "SystemsAdmin")]
    public class ConfigurationController : Controller
    {
        private readonly ConfigurationService _configService;
        private readonly AuditService _auditService;
        private readonly IConfiguration _appConfig;

        public ConfigurationController(
            ConfigurationService configService,
            AuditService auditService,
            IConfiguration appConfig)
        {
            _configService = configService;
            _auditService = auditService;
            _appConfig = appConfig;
        }

        public IActionResult Index()
        {
            // Surface whether the SMTP password secret is present (without revealing it).
            ViewBag.SmtpPasswordConfigured =
                !string.IsNullOrWhiteSpace(_appConfig["EmailSettings:SenderPassword"]);
            return View(_configService.Get());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(AppConfiguration model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.SmtpPasswordConfigured =
                    !string.IsNullOrWhiteSpace(_appConfig["EmailSettings:SenderPassword"]);
                return View(model);
            }

            var updatedBy = HttpContext.Session.GetString("UserName") ?? "Unknown";
            await _configService.SaveAsync(model, updatedBy);

            await _auditService.LogAsync("Configuration Updated", "AppConfiguration", model.Id,
                "Security configuration updated");

            TempData["Success"] = "Configuration saved. Note: session-timeout changes apply after the next app restart.";
            return RedirectToAction(nameof(Index));
        }
    }
}
