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
            if (Roles.IsHr(role)) return HrDashboard();
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
            ViewBag.PendingClearances = _context.ExitClearances.Count(c => c.Status == ClearanceStatus.InProgress);
            ViewBag.ExpiringCertificates = _context.SSLCertificates.Count(c => c.ExpiryDate <= soon);
            ViewBag.TotalAssets = _context.Assets.Count();
            ViewBag.PendingInterviews = _context.ExitInterviews.Count();

            return View("Index");
        }

        private IActionResult HrDashboard()
        {
            ViewBag.TotalClearances = _context.ExitClearances.Count();
            ViewBag.ClearancesInProgress = _context.ExitClearances.Count(c => c.Status == ClearanceStatus.InProgress);
            ViewBag.ClearancesCompleted = _context.ExitClearances.Count(c => c.Status == ClearanceStatus.Completed);
            ViewBag.ExitInterviews = _context.ExitInterviews.Count();
            ViewBag.EngagementInterviews = _context.EngagementStayInterviews.Count();
            ViewBag.TalentRecords = _context.TalentIdentifications.Count();
            ViewBag.RecentClearances = _context.ExitClearances
                .Include(c => c.Employee)
                .OrderByDescending(c => c.CreatedDate)
                .Take(6).ToList();

            return View("HrDashboard");
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

            var clearance = _context.ExitClearances
                .Where(c => c.EmployeeId == userId)
                .OrderByDescending(c => c.CreatedDate)
                .FirstOrDefault();
            ViewBag.MyClearanceStatus = clearance?.Status.ToString();
            ViewBag.MyClearanceStage = clearance?.CurrentStage.ToString();

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
