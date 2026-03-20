using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;

        public AccountController(ApplicationDbContext context, EmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null || user.PasswordHash != password || !user.IsActive)
            {
                ViewBag.Error = "Invalid credentials or account not activated.";
                return View();
            }

            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("UserName", user.FirstName);
            HttpContext.Session.SetString("UserRole", user.Role.ToString());

            return RedirectToAction("Index", "Home");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        public async Task<IActionResult> SetPassword(string token)
        {
            var user = await _context.Users
                 .Where(u => u.ResetToken == token).FirstOrDefaultAsync();

            if (user == null || user.TokenExpiry < DateTime.Now)
                return BadRequest("Invalid or expired link");

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SetPassword(string token, string password)
        {
            if (string.IsNullOrEmpty(token))
                return Content("ERROR: Token is NULL");

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.ResetToken == token);

            if (user == null)
                return Content("ERROR: User NOT FOUND");

            if (user.TokenExpiry == null || user.TokenExpiry < DateTime.Now)
                return Content("ERROR: Token EXPIRED");

            if (!IsValidPassword(password))
                return Content("ERROR: Weak password");

            user.PasswordHash = password;
            user.IsActive = true;
            user.ResetToken = null;
            user.TokenExpiry = null;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Account activated successfully. Please login.";
            return RedirectToAction("Login", "Account");
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                ViewBag.Message = "If the email exists, a reset link has been sent.";
                return View();
            }

            user.ResetToken = Guid.NewGuid().ToString();
            user.TokenExpiry = DateTime.Now.AddHours(1);

            await _context.SaveChangesAsync();

            var link = Url.Action(
                "SetPassword",
                "Account",
                new { token = user.ResetToken },
                Request.Scheme);

            var body = $@"
                <p>Good Day {user.FirstName},</p>

                <p>You requested to reset your password.</p>

                <p>Click below to set a new password:</p>

                <p>
                    <a href='{link}'>Reset Password</a>
                </p>

                <p>This link expires in 1 hour.</p>
            ";

            await _emailService.SendEmailAsync(user.Email, "Password Reset", body);

            ViewBag.Message = "If the email exists, a reset link has been sent.";
            return View();
        }

        private bool IsValidPassword(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 8)
                return false;

            bool hasUpper = password.Any(char.IsUpper);
            bool hasLower = password.Any(char.IsLower);
            bool hasSpecial = password.Any(ch => !char.IsLetterOrDigit(ch));

            return hasUpper && hasLower && hasSpecial;
        }
    }
}