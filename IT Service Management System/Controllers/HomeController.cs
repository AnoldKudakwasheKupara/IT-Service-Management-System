using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace IT_Service_Management_System.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            // Get ticket statistics
            ViewBag.TotalTickets = _context.Tickets.Count();
            ViewBag.OpenTickets = _context.Tickets.Count(t => t.Status.ToString() == "Open");
            ViewBag.InProgressTickets = _context.Tickets.Count(t => t.Status.ToString() == "InProgress");
            ViewBag.ResolvedTickets = _context.Tickets.Count(t => t.Status.ToString() == "Resolved");
            ViewBag.ClosedTickets = _context.Tickets.Count(t => t.Status.ToString() == "Closed");
            ViewBag.HighPriorityTickets = _context.Tickets.Count(t => t.Priority.ToString() == "High");
            ViewBag.CriticalPriorityTickets = _context.Tickets.Count(t => t.Priority.ToString() == "Critical");

            return View();
        }
    }
}