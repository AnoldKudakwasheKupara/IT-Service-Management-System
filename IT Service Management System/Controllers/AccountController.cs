using IT_Service_Management_System.DbContexts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
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
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.ResetToken == token);

            if (user == null || user.TokenExpiry == null || user.TokenExpiry < DateTime.Now)
                return BadRequest("Invalid or expired link");

            // 🔐 PASSWORD VALIDATION
            if (!IsValidPassword(password))
            {
                ModelState.AddModelError("", "Password must be at least 8 characters and include uppercase, lowercase, and special character.");
                return View();
            }

            user.PasswordHash = password;
            user.IsActive = true;
            user.ResetToken = null;
            user.TokenExpiry = null;

            await _context.SaveChangesAsync();

            return RedirectToAction("Login");
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
