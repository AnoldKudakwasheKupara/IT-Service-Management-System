using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using IT_Service_Management_System.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;

        public UsersController(ApplicationDbContext context, EmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _context.Users.ToListAsync();
            return View(users);
        }

        public async Task<IActionResult> Details(int id)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null) return NotFound();

            return View(user);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(User user)
        {
            if (!ModelState.IsValid)
                return View(user);

            user.CreatedAt = DateTime.Now;
            user.IsActive = false;

            // 🔐 Generate token
            user.ResetToken = Guid.NewGuid().ToString();
            user.TokenExpiry = DateTime.Now.AddHours(24);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var link = Url.Action(
                "SetPassword",
                "Account",
                new { token = user.ResetToken },
                Request.Scheme);

                        var body = $@"
                <h3>Welcome to IT Service Management System</h3>
                <p>Please click the link below to set your password:</p>
                <a href='{link}'>Set Password</a>
            ";

            await _emailService.SendEmailAsync(user.Email, "Set Your Password", body);


            return RedirectToAction("Index");
        }

        // 🔹 SHOW EDIT FORM
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null) return NotFound();

            return View(user);
        }

        // 🔹 UPDATE USER
        [HttpPost]
        public async Task<IActionResult> Edit(User user)
        {
            if (!ModelState.IsValid)
                return View(user);

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        // 🔹 SHOW DELETE CONFIRMATION
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null) return NotFound();

            return View(user);
        }

        // 🔹 DELETE USER
        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null) return NotFound();

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        private async Task SendEmail(string toEmail, string link)
        {
            // TEMP: just log to console
            Console.WriteLine($"Send this link to user: {link}");
        }
    }
}