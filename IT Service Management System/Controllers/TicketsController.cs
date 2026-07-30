using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Models;
using IT_Service_Management_System.Services.Itsm;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static IT_Service_Management_System.Models.Ticket;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>
    /// Helpdesk ticket surface. Reads/query + view preparation live here; every state change and
    /// its side-effects (audit, email, realtime, SLA math, concurrency handling) are delegated to
    /// <see cref="TicketService"/>, and attachment vetting/storage to <see cref="TicketAttachmentService"/>.
    /// Authentication is guaranteed by the global SessionAuthorizationFilter, so actions only assert
    /// the extra role/ownership rules they need.
    /// </summary>
    public class TicketsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly TicketService _tickets;
        private readonly TicketAttachmentService _attachments;

        public TicketsController(ApplicationDbContext context, TicketService tickets,
            TicketAttachmentService attachments)
        {
            _context = context;
            _tickets = tickets;
            _attachments = attachments;
        }

        // ── helpers ──────────────────────────────────────────────────────────────────
        private int Uid => HttpContext.Session.GetInt32("UserId") ?? 0;
        private string? Role => HttpContext.Session.GetString("UserRole");
        private bool IsStaff => TicketService.IsStaff(Role);

        // Session-based auth has no ASP.NET auth scheme, so Forbid() would throw. Redirect instead.
        private IActionResult Denied() => RedirectToAction("AccessDenied", "Home");

        // Maps a service outcome to a redirect back to the ticket, with the right flash message.
        private IActionResult Redirect(TicketOp op, int id, string successKey = "Success") => op.Status switch
        {
            TicketOpStatus.NotFound => NotFound(),
            TicketOpStatus.Ok => Flash(successKey, op.Message, id),
            _ => Flash("Error", op.Message, id) // Concurrency + Invalid
        };

        private IActionResult Flash(string key, string? message, int id)
        {
            if (message != null) TempData[key] = message;
            return RedirectToAction("Details", new { id });
        }

        // ── list ─────────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Index(int page = 1, string? q = null, string? status = null, string? priority = null, string? category = null)
        {
            // Role-scoped base set drives the (unfiltered) summary counts so the stat cards
            // stay stable regardless of the current search/filter or page.
            var baseQuery = _context.Tickets.AsQueryable();
            if (!IsStaff)
                baseQuery = baseQuery.Where(t => t.CreatedById == Uid);

            ViewBag.TotalTickets = await baseQuery.CountAsync();
            ViewBag.OpenTickets = await baseQuery.CountAsync(t => t.Status == TicketStatus.Open);
            ViewBag.InProgressTickets = await baseQuery.CountAsync(t => t.Status == TicketStatus.InProgress);
            ViewBag.ClosedTickets = await baseQuery.CountAsync(t => t.Status == TicketStatus.Closed);

            IQueryable<Ticket> query = baseQuery
                .Include(t => t.CreatedBy)
                .Include(t => t.AssignedTo);

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(t => t.Title.Contains(q) || t.Description.Contains(q));

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TicketStatus>(status, out var st))
                query = query.Where(t => t.Status == st);

            if (!string.IsNullOrWhiteSpace(priority) && Enum.TryParse<TicketPriority>(priority, out var pr))
                query = query.Where(t => t.Priority == pr);

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(t => t.Category == category);

            var ordered = query.OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt);

            // Distinct categories (queues) for the filter dropdown.
            ViewBag.Categories = await baseQuery
                .Where(t => t.Category != null && t.Category != "")
                .Select(t => t.Category).Distinct().OrderBy(c => c).ToListAsync();

            var (tickets, paging) = await ordered.PageAsync(page);
            ViewBag.Paging = paging;
            ViewBag.Search = q;
            ViewBag.Status = status;
            ViewBag.Priority = priority;
            ViewBag.Category = category;

            return View(tickets);
        }

        // ── create ───────────────────────────────────────────────────────────────────
        public IActionResult Create() => View();

        [HttpPost]
        public async Task<IActionResult> Create(Ticket ticket, List<IFormFile> files)
        {
            if (!ModelState.IsValid) return View(ticket);

            var created = await _tickets.CreateAsync(ticket, Uid);
            var result = await _attachments.SaveAsync(files, ticketId: created.Id);

            var message = $"Ticket {created.Reference} created. Our team has been notified.";
            if (result.AnyRejected)
                message += " Some files were not attached: " + string.Join("; ", result.Skipped);
            TempData["Success"] = message;
            return RedirectToAction("Details", new { id = created.Id });
        }

        // ── details ──────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Details(int id)
        {
            var ticket = await _context.Tickets
                .Include(t => t.CreatedBy)
                .Include(t => t.AssignedTo)
                .Include(t => t.SlaPolicy).ThenInclude(p => p!.Calendar)
                .Include(t => t.SlaEvents)
                .Include(t => t.Attachments)
                .Include(t => t.Messages).ThenInclude(m => m.Sender)
                .Include(t => t.Messages).ThenInclude(m => m.Attachments)
                .AsSplitQuery()   // multiple collection includes — avoid a cartesian row explosion
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null) return NotFound();
            bool staff = IsStaff;
            if (!staff && ticket.CreatedById != Uid) return Denied();

            // Requesters never see internal staff notes.
            if (!staff)
                ticket.Messages = ticket.Messages.Where(m => !m.IsInternal).ToList();

            ViewBag.IsStaff = staff;
            ViewBag.Agents = staff ? await _tickets.AgentsAsync() : new List<User>();
            ViewBag.CannedResponses = staff
                ? await _context.CannedResponses.AsNoTracking().OrderBy(c => c.Title).ToListAsync()
                : new List<CannedResponse>();
            return View(ticket);
        }

        // ── edit (staff) ─────────────────────────────────────────────────────────────
        public async Task<IActionResult> Edit(int id)
        {
            if (!IsStaff) return Denied();

            var ticket = await _context.Tickets
                .Include(t => t.CreatedBy).Include(t => t.AssignedTo)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (ticket == null) return NotFound();

            ViewBag.Agents = await _tickets.AgentsAsync();
            return View(ticket);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Ticket updatedTicket)
        {
            if (!IsStaff) return Denied();
            if (!ModelState.IsValid)
            {
                ViewBag.Agents = await _tickets.AgentsAsync();
                return View(updatedTicket);
            }

            var op = await _tickets.EditAsync(updatedTicket);
            if (op.Status == TicketOpStatus.NotFound) return NotFound();
            // Concurrency and a rejected status/assignee both send the user back to the form to correct it.
            if (op.Status != TicketOpStatus.Ok)
            {
                TempData["Error"] = op.Message;
                return RedirectToAction("Edit", new { id = updatedTicket.Id });
            }
            return RedirectToAction("Details", new { id = updatedTicket.Id });
        }

        // ── assign (staff) ───────────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(int id, int? assignedToId)
        {
            if (!IsStaff) return Denied();
            return Redirect(await _tickets.AssignAsync(id, assignedToId), id);
        }

        // ── status quick-change (staff) ──────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatus(int id, TicketStatus status)
        {
            if (!IsStaff) return Denied();
            return Redirect(await _tickets.ChangeStatusAsync(id, status), id);
        }

        // ── reopen (owner or staff) ──────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reopen(int id)
        {
            var op = await _tickets.ReopenAsync(id, Uid, Role);
            if (op.Status == TicketOpStatus.Invalid) return Denied();
            return Redirect(op, id);
        }

        // ── put on hold (staff) — pauses the SLA clock ───────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Hold(int id, string? reason)
        {
            if (!IsStaff) return Denied();
            return Redirect(await _tickets.HoldAsync(id, reason, Uid), id);
        }

        // ── resume from hold (staff) — resumes the SLA clock ─────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resume(int id)
        {
            if (!IsStaff) return Denied();
            return Redirect(await _tickets.ResumeAsync(id, Uid), id);
        }

        // ── escalate (staff) — raise priority + alert the team ───────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Escalate(int id, string? reason)
        {
            if (!IsStaff) return Denied();
            return Redirect(await _tickets.EscalateAsync(id, reason, Uid), id, successKey: "Warning");
        }

        // ── reply ────────────────────────────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> AddReply(int ticketId, string message, List<IFormFile> files, bool isInternal = false)
        {
            var reply = await _tickets.AddReplyAsync(ticketId, Uid, Role, message, isInternal);
            if (!reply.Success)
                return StatusCode(reply.StatusCode, new { success = false, message = reply.Error });

            var attach = await _attachments.SaveAsync(files, ticketMessageId: reply.MessageId);
            return Json(new
            {
                success = true,
                senderName = reply.SenderName,
                time = reply.Time,
                isStaff = reply.IsStaffReply,
                isInternal = reply.IsInternal,
                attachmentsRejected = attach.AnyRejected ? attach.Skipped : null
            });
        }

        // ── CSAT: requester rates a resolved/closed ticket ─────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RateSatisfaction(int id, int rating, string? comment)
        {
            var op = await _tickets.RateSatisfactionAsync(id, Uid, rating, comment);
            return Redirect(op, id);
        }

        // ── close (staff) ────────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Close(int id)
        {
            if (!IsStaff) return Denied();
            var ticket = await _context.Tickets.Include(t => t.CreatedBy).FirstOrDefaultAsync(t => t.Id == id);
            if (ticket == null) return NotFound();
            return View(ticket);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Close(int id, string closingNotes)
        {
            if (!IsStaff) return Denied();
            return Redirect(await _tickets.CloseAsync(id, closingNotes, Uid), id);
        }

        // ── delete (staff) ───────────────────────────────────────────────────────────
        public async Task<IActionResult> Delete(int id)
        {
            if (!IsStaff) return Denied();
            var ticket = await _context.Tickets.Include(t => t.CreatedBy)
                .Include(t => t.Messages).Include(t => t.Attachments)
                .AsSplitQuery()   // two collection includes — avoid a cartesian row explosion
                .FirstOrDefaultAsync(t => t.Id == id);
            if (ticket == null) return NotFound();
            return View(ticket);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!IsStaff) return Denied();
            var op = await _tickets.SoftDeleteAsync(id);
            if (op.Status == TicketOpStatus.NotFound) return NotFound();
            if (op.Message != null) TempData[op.Status == TicketOpStatus.Ok ? "Success" : "Error"] = op.Message;
            return RedirectToAction("Index");
        }
    }
}
