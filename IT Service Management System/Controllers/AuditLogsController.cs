using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    [IT_Service_Management_System.Filters.RoleAuthorize("Admin", "SystemsAdmin")]
    public class AuditLogsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AuditLogsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int page = 1, string? q = null)
        {
            // Statistics over the full (unfiltered) set so the stat cards stay
            // stable regardless of the current search or page.
            ViewBag.TotalCount = await _context.AuditLogs.CountAsync();
            ViewBag.CreatedCount = await _context.AuditLogs.CountAsync(l => l.Action == "Created");
            ViewBag.UpdatedCount = await _context.AuditLogs.CountAsync(l => l.Action == "Updated");
            ViewBag.DeletedCount = await _context.AuditLogs.CountAsync(l => l.Action == "Deleted");

            // Declared as IQueryable<AuditLog> before .Where so the search filter composes cleanly.
            IQueryable<AuditLog> query = _context.AuditLogs;

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(a =>
                    a.Action.Contains(q) || a.UserName.Contains(q) ||
                    a.Details.Contains(q) || a.Entity.Contains(q));

            query = query.OrderByDescending(a => a.Timestamp);

            var (items, paging) = await query.PageAsync(page);
            ViewBag.Paging = paging;
            ViewBag.Search = q;

            return View(items);
        }
    }
}