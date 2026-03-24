using IT_Service_Management_System.DbContexts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    public class AuditLogsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AuditLogsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index(int page = 1, int pageSize = 10)
        {
            // Get all logs ordered by most recent first
            var logs = _context.AuditLogs
                .OrderByDescending(l => l.Timestamp)
                .AsQueryable();

            // Calculate total count for pagination
            var totalLogs = logs.Count();
            var totalPages = (int)Math.Ceiling(totalLogs / (double)pageSize);

            // Ensure page is within valid range
            if (page < 1) page = 1;
            if (page > totalPages && totalPages > 0) page = totalPages;

            // Apply pagination
            var paginatedLogs = logs
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Calculate statistics from all logs
            ViewBag.TotalCount = totalLogs;
            ViewBag.CreatedCount = _context.AuditLogs.Count(l => l.Action == "Created");
            ViewBag.UpdatedCount = _context.AuditLogs.Count(l => l.Action == "Updated");
            ViewBag.DeletedCount = _context.AuditLogs.Count(l => l.Action == "Deleted");

            // Pagination data
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;
            ViewBag.HasPreviousPage = page > 1;
            ViewBag.HasNextPage = page < totalPages;

            return View(paginatedLogs);
        }
    }
}