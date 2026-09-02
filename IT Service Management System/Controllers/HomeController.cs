using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Role-aware dashboard dispatcher.
        public IActionResult Index()
        {
            var role = HttpContext.Session.GetString("UserRole");
            var userId = HttpContext.Session.GetInt32("UserId") ?? 0;

            if (Roles.IsFullAccess(role)) return AdminDashboard();
            return MyDashboard(userId);
        }

        private IActionResult AdminDashboard()
        {
            var soon = DateTime.Now.AddDays(30);

            ViewBag.TotalTickets = _context.Tickets.Count();
            ViewBag.OpenTickets = _context.Tickets.Count(t => t.Status == Ticket.TicketStatus.Open);
            ViewBag.InProgressTickets = _context.Tickets.Count(t => t.Status == Ticket.TicketStatus.InProgress);
            ViewBag.ResolvedTickets = _context.Tickets.Count(t => t.Status == Ticket.TicketStatus.Resolved);
            ViewBag.ClosedTickets = _context.Tickets.Count(t => t.Status == Ticket.TicketStatus.Closed);
            ViewBag.HighPriorityTickets = _context.Tickets.Count(t => t.Priority == Ticket.TicketPriority.High);
            ViewBag.CriticalPriorityTickets = 0;

            ViewBag.TotalUsers = _context.Users.Count();
            ViewBag.TotalDepartments = _context.Departments.Count();
            ViewBag.ExpiringCertificates = _context.SSLCertificates.Count(c => c.ExpiryDate <= soon);
            ViewBag.TotalAssets = _context.Assets.Count();

            return View("Index");
        }

        private IActionResult MyDashboard(int userId)
        {
            var myTickets = _context.Tickets.Where(t => t.CreatedById == userId).ToList();
            ViewBag.MyTotalTickets = myTickets.Count;
            ViewBag.MyOpenTickets = myTickets.Count(t => t.Status == Ticket.TicketStatus.Open);
            ViewBag.MyResolvedTickets = myTickets.Count(t => t.Status == Ticket.TicketStatus.Resolved || t.Status == Ticket.TicketStatus.Closed);
            ViewBag.MyRecentTickets = myTickets.OrderByDescending(t => t.CreatedAt).Take(5).ToList();

            var uid = userId.ToString();
            var myActivities = _context.Activities.Where(a => a.UserId == uid).ToList();
            ViewBag.MyActivityCount = myActivities.Count;
            ViewBag.MyActivityHours = Math.Round(myActivities.Where(a => a.Duration.HasValue).Sum(a => a.Duration!.Value.TotalHours), 1);

            return View("MyDashboard");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        // Shown when a user hits a module their role can't access.
        public IActionResult AccessDenied()
        {
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}
