using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    public class TicketsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TicketsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var tickets = await _context.Tickets
                .Include(t => t.CreatedBy)
                .ToListAsync();

            return View(tickets);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(Ticket ticket, List<IFormFile> files)
        {
            if (!ModelState.IsValid)
                return View(ticket);

            ticket.CreatedAt = DateTime.Now;
            ticket.CreatedById = 1;

            _context.Tickets.Add(ticket);
            await _context.SaveChangesAsync();

            if (files != null && files.Count > 0)
            {
                var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");

                if (!Directory.Exists(uploadPath))
                    Directory.CreateDirectory(uploadPath);

                foreach (var file in files)
                {
                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                    var filePath = Path.Combine(uploadPath, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    var attachment = new TicketAttachment
                    {
                        FileName = file.FileName,
                        FilePath = "/uploads/" + fileName,
                        ContentType = file.ContentType,
                        TicketId = ticket.Id,
                        UploadedAt = DateTime.Now
                    };

                    _context.TicketAttachments.Add(attachment);
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Details(int id)
        {
            var ticket = await _context.Tickets
                .Include(t => t.CreatedBy)
                .Include(t => t.Attachments)
                .Include(t => t.Messages)
                    .ThenInclude(m => m.Sender)
                .Include(t => t.Messages)
                    .ThenInclude(m => m.Attachments)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null) return NotFound();

            return View(ticket);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var ticket = await _context.Tickets.FindAsync(id);
            if (ticket == null) return NotFound();

            return View(ticket);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Ticket ticket)
        {
            if (!ModelState.IsValid) return View(ticket);

            _context.Tickets.Update(ticket);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Delete(int id)
        {
            var ticket = await _context.Tickets.FindAsync(id);
            if (ticket == null) return NotFound();

            return View(ticket);
        }

        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var ticket = await _context.Tickets.FindAsync(id);

            var attachments = _context.TicketAttachments.Where(a => a.TicketId == id);
            var messages = _context.TicketMessages.Where(m => m.TicketId == id);

            _context.TicketAttachments.RemoveRange(attachments);
            _context.TicketMessages.RemoveRange(messages);
            _context.Tickets.Remove(ticket);

            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }
        [HttpPost]
        public async Task<IActionResult> AddReply(int ticketId, string message, List<IFormFile> files)
        {
            var ticketMessage = new TicketMessage
            {
                TicketId = ticketId,
                SenderId = _context.Users.First().Id,
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
    }
}