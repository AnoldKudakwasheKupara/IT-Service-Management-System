using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static IT_Service_Management_System.Models.Ticket;

namespace IT_Service_Management_System.Controllers
{
    public class TicketsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TicketsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 🔹 LIST TICKETS
        public async Task<IActionResult> Index()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var role = HttpContext.Session.GetString("UserRole");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            List<Ticket> tickets;


            if (role == "Admin")
            {
                tickets = await _context.Tickets
                    .Include(t => t.CreatedBy)
                    .ToListAsync();
            }
            else
            {
                tickets = await _context.Tickets
                    .Where(t => t.CreatedById == userId)
                    .Include(t => t.CreatedBy)
                    .ToListAsync();
            }

            return View(tickets);
        }

        // 🔹 CREATE
        public IActionResult Create()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return RedirectToAction("Login", "Account");

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(Ticket ticket, List<IFormFile> files)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
                return View(ticket);

            ticket.CreatedAt = DateTime.Now;
            ticket.Status = Ticket.TicketStatus.Open;
            ticket.CreatedById = userId.Value;

            _context.Tickets.Add(ticket);
            await _context.SaveChangesAsync();

            // Attachments
            if (files != null && files.Count > 0)
            {
                var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");

                if (!Directory.Exists(uploadPath))
                    Directory.CreateDirectory(uploadPath);

                foreach (var file in files)
                {
                    var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
                    var filePath = Path.Combine(uploadPath, fileName);

                    using var stream = new FileStream(filePath, FileMode.Create);
                    await file.CopyToAsync(stream);

                    _context.TicketAttachments.Add(new TicketAttachment
                    {
                        FileName = file.FileName,
                        FilePath = "/uploads/" + fileName,
                        TicketId = ticket.Id
                    });
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }

        // 🔹 DETAILS
        public async Task<IActionResult> Details(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var role = HttpContext.Session.GetString("UserRole");

            if (userId == null)
                return RedirectToAction("Login", "Account");

            var ticket = await _context.Tickets
                .Include(t => t.Messages)
                    .ThenInclude(m => m.Sender)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null)
                return NotFound();

            if (role != "Admin" && ticket.CreatedById != userId)
                return Forbid();

            return View(ticket);
        }

        // 🔹 EDIT (ADMIN ONLY)
        public async Task<IActionResult> Edit(int id)
        {
            if (HttpContext.Session.GetString("UserRole") != "Admin")
                return Forbid();

            var ticket = await _context.Tickets.FindAsync(id);
            if (ticket == null) return NotFound();

            return View(ticket);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Ticket ticket)
        {
            if (HttpContext.Session.GetString("UserRole") != "Admin")
                return Forbid();

            if (!ModelState.IsValid) return View(ticket);

            _context.Tickets.Update(ticket);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        // 🔹 DELETE (ADMIN ONLY)
        public async Task<IActionResult> Delete(int id)
        {
            if (HttpContext.Session.GetString("UserRole") != "Admin")
                return Forbid();

            var ticket = await _context.Tickets.FindAsync(id);
            if (ticket == null) return NotFound();

            return View(ticket);
        }

        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (HttpContext.Session.GetString("UserRole") != "Admin")
                return Forbid();

            var ticket = await _context.Tickets.FindAsync(id);
            if (ticket == null) return NotFound();

            var attachments = _context.TicketAttachments.Where(a => a.TicketId == id);
            var messages = _context.TicketMessages.Where(m => m.TicketId == id);

            _context.TicketAttachments.RemoveRange(attachments);
            _context.TicketMessages.RemoveRange(messages);
            _context.Tickets.Remove(ticket);

            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        // 🔹 REPLY
        [HttpPost]
        public async Task<IActionResult> AddReply(int ticketId, string message, List<IFormFile> files)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
                return Unauthorized();

            var ticketMessage = new TicketMessage
            {
                TicketId = ticketId,
                SenderId = userId.Value,
                Message = message,
                SentAt = DateTime.Now
            };

            _context.TicketMessages.Add(ticketMessage);
            await _context.SaveChangesAsync();

            if (files != null && files.Count > 0)
            {
                var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");

                if (!Directory.Exists(uploadPath))
                    Directory.CreateDirectory(uploadPath);

                foreach (var file in files)
                {
                    var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
                    var filePath = Path.Combine(uploadPath, fileName);

                    using var stream = new FileStream(filePath, FileMode.Create);
                    await file.CopyToAsync(stream);

                    _context.TicketAttachments.Add(new TicketAttachment
                    {
                        FileName = file.FileName,
                        FilePath = "/uploads/" + fileName,
                        TicketMessageId = ticketMessage.Id
                    });
                }

                await _context.SaveChangesAsync();
            }

            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> GetStats()
        {
            var stats = new
            {
                total = await _context.Tickets.CountAsync(),
                open = await _context.Tickets.CountAsync(t => t.Status == TicketStatus.Open),
                inProgress = await _context.Tickets.CountAsync(t => t.Status == TicketStatus.InProgress),
                resolved = await _context.Tickets.CountAsync(t => t.Status == TicketStatus.Resolved)
            };
            return Json(stats);
        }

        [HttpGet]
        public async Task<IActionResult> GetRecent(int count = 5)
        {
            var tickets = await _context.Tickets
                .Include(t => t.CreatedBy)
                .OrderByDescending(t => t.CreatedAt)
                .Take(count)
                .Select(t => new
                {
                    t.Id,
                    t.Title,
                    Priority = t.Priority.ToString(),
                    Status = t.Status.ToString(),
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync();
            return Json(tickets);
        }

        [HttpGet("api/tickets/stats")]
        public async Task<IActionResult> GetTicketStats()
        {
            var stats = new
            {
                total = await _context.Tickets.CountAsync(),
                open = await _context.Tickets.CountAsync(t => t.Status == TicketStatus.Open),
                inProgress = await _context.Tickets.CountAsync(t => t.Status == TicketStatus.InProgress),
                resolved = await _context.Tickets.CountAsync(t => t.Status == TicketStatus.Resolved)
            };
            return Ok(stats);
        }

        [HttpGet("api/tickets/recent")]
        public async Task<IActionResult> GetRecentTickets(int count = 5)
        {
            var tickets = await _context.Tickets
                .Include(t => t.CreatedBy)
                .OrderByDescending(t => t.CreatedAt)
                .Take(count)
                .Select(t => new
                {
                    t.Id,
                    t.Title,
                    Priority = t.Priority.ToString(),
                    Status = t.Status.ToString(),
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync();
            return Ok(tickets);
        }


        [HttpPost]
        public async Task<IActionResult> CloseTicket(int id)
        {
            if (HttpContext.Session.GetString("UserRole") != "Admin")
                return Forbid();

            var ticket = await _context.Tickets.FindAsync(id);

            if (ticket == null) return NotFound();

            ticket.Status = Ticket.TicketStatus.Closed;

            _context.Tickets.Update(ticket);
            await _context.SaveChangesAsync();

            return RedirectToAction("Details", new { id });
        }
    }
}