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
        [HttpPost]
        public async Task<IActionResult> Create(User user)
        {
            if (!ModelState.IsValid)
                return View(user);

            // 🔍 CHECK EMAIL EXISTS
            var existingUser = await _context.Users
                .AnyAsync(u => u.Email == user.Email);

            if (existingUser)
            {
                ModelState.AddModelError("Email", "This email is already registered.");
                return View(user);
            }

            user.CreatedAt = DateTime.Now;
            user.IsActive = false;
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
                <p>Good Day {user.FirstName},</p>

                <p>Welcome to the <strong>IT Service Management System</strong>.</p>

                <p>Your account has been created successfully. To get started, please set your password by clicking the link below:</p>

                <p>
                    <a href='{link}' style='color:blue; font-weight:bold;'>
                        Set Your Password
                    </a>
                </p>

                <p>This link will expire in 24 hours for security purposes.</p>

                <br/>

                <p>If you did not expect this email, please ignore it.</p>

                <p>Kind Regards,<br/>
                IT Support Team</p>
            ";


            await _emailService.SendEmailAsync(user.Email, "Welcome - Set Your Password", body);

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