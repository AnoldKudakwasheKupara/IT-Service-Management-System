using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Models.Itsm;
using IT_Service_Management_System.Services;
using IT_Service_Management_System.Services.Itsm;
using IT_Service_Management_System.Services.Realtime;
using IT_Service_Management_System.ViewModels.Itsm;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static IT_Service_Management_System.Models.Ticket;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>Employee self-service catalogue plus staff approval and fulfilment queue.</summary>
    public class ServiceCatalogController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditService _audit;
        private readonly IRealtimeNotifier _realtime;

        public ServiceCatalogController(ApplicationDbContext db, AuditService audit, IRealtimeNotifier realtime)
        {
            _db = db;
            _audit = audit;
            _realtime = realtime;
        }

        private int Uid => HttpContext.Session.GetInt32("UserId") ?? 0;
        private string? Role => HttpContext.Session.GetString("UserRole");
        private bool CanManage => Roles.IsFullAccess(Role);

        public async Task<IActionResult> Index(string? q, string? category)
        {
            var query = _db.ServiceCatalogItems.AsNoTracking().Include(i => i.Owner).AsQueryable();
            if (!CanManage) query = query.Where(i => i.IsPublished);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(i => i.Name.Contains(term) || i.Summary.Contains(term) ||
                    (i.Description != null && i.Description.Contains(term)));
            }
            if (!string.IsNullOrWhiteSpace(category)) query = query.Where(i => i.Category == category);

            var categoriesQuery = _db.ServiceCatalogItems.AsNoTracking().AsQueryable();
            if (!CanManage) categoriesQuery = categoriesQuery.Where(i => i.IsPublished);

            return View(new ServiceCatalogIndexVm
            {
                Items = await query.OrderBy(i => i.Category).ThenBy(i => i.Name).ToListAsync(),
                Categories = await categoriesQuery.Select(i => i.Category).Distinct().OrderBy(c => c).ToListAsync(),
                Query = q,
                Category = category,
                CanManage = CanManage
            });
        }

        public async Task<IActionResult> Details(int id)
        {
            var item = await _db.ServiceCatalogItems.AsNoTracking().Include(i => i.Owner)
                .FirstOrDefaultAsync(i => i.Id == id && (i.IsPublished || CanManage));
            return item == null ? NotFound() : View(item);
        }

        public async Task<IActionResult> Submit(int id)
        {
            var item = await _db.ServiceCatalogItems.AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == id && i.IsPublished);
            if (item == null) return NotFound();
            return View(new SubmitServiceRequestVm { ServiceCatalogItemId = item.Id, Item = item, Subject = item.Name });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(SubmitServiceRequestVm model)
        {
            var item = await _db.ServiceCatalogItems.FirstOrDefaultAsync(i => i.Id == model.ServiceCatalogItemId && i.IsPublished);
            if (item == null) return NotFound();
            model.Item = item;
            if (!ModelState.IsValid) return View(model);

            var now = DateTime.Now;
            var request = new ServiceRequest
            {
                ServiceCatalogItemId = item.Id,
                RequestedById = Uid,
                AssignedToId = item.OwnerId,
                Subject = model.Subject.Trim(),
                Details = model.Details.Trim(),
                BusinessJustification = model.BusinessJustification?.Trim(),
                Priority = item.DefaultPriority,
                Status = item.RequiresApproval ? ServiceRequestStatus.AwaitingApproval : ServiceRequestStatus.Approved,
                ApprovedAt = item.RequiresApproval ? null : now,
                CreatedAt = now,
                DueAt = now.AddMinutes(item.FulfillmentTargetMinutes)
            };
            _db.ServiceRequests.Add(request);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Created", "ServiceRequest", request.Id, $"{request.Reference}: {item.Name}");
            if (request.AssignedToId.HasValue)
                await _realtime.NotifyUserAsync(request.AssignedToId.Value,
                    new RealtimeNotice($"New service request {request.Reference}", request.Subject,
                        Url.Action(nameof(RequestDetails), new { id = request.Id }), "info"));

            TempData["Success"] = $"Request {request.Reference} submitted successfully.";
            return RedirectToAction(nameof(RequestDetails), new { id = request.Id });
        }

        public async Task<IActionResult> MyRequests()
        {
            var requests = await _db.ServiceRequests.AsNoTracking()
                .Include(r => r.ServiceCatalogItem).Include(r => r.AssignedTo)
                .Where(r => r.RequestedById == Uid)
                .OrderByDescending(r => r.CreatedAt).ToListAsync();
            return View(requests);
        }

        public async Task<IActionResult> Queue(ServiceRequestStatus? status)
        {
            if (!CanManage) return Forbid();
            var query = _db.ServiceRequests.AsNoTracking().Include(r => r.ServiceCatalogItem)
                .Include(r => r.RequestedBy).Include(r => r.AssignedTo).AsQueryable();
            if (status.HasValue) query = query.Where(r => r.Status == status);
            ViewBag.Status = status;
            return View(await query.OrderBy(r => r.DueAt).ThenByDescending(r => r.CreatedAt).ToListAsync());
        }

        public async Task<IActionResult> RequestDetails(int id)
        {
            var request = await _db.ServiceRequests.Include(r => r.ServiceCatalogItem)
                .Include(r => r.RequestedBy).Include(r => r.AssignedTo).Include(r => r.ApprovedBy)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (request == null) return NotFound();
            if (!CanManage && request.RequestedById != Uid) return Forbid();
            if (CanManage)
                ViewBag.Agents = await _db.Users.Where(u => u.IsActive &&
                        (u.Role == UserRole.Admin || u.Role == UserRole.SystemsAdmin))
                    .OrderBy(u => u.FirstName).ToListAsync();
            return View(request);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Decide(int id, bool approve, string? notes)
        {
            if (!CanManage) return Forbid();
            var request = await _db.ServiceRequests.FindAsync(id);
            if (request == null) return NotFound();
            var next = approve ? ServiceRequestStatus.Approved : ServiceRequestStatus.Rejected;
            if (!ServiceRequestWorkflow.CanTransition(request.Status, next))
            {
                TempData["Error"] = "This request is no longer awaiting approval.";
                return RedirectToAction(nameof(RequestDetails), new { id });
            }

            request.Status = next;
            request.ApprovedById = Uid;
            request.ApprovedAt = DateTime.Now;
            request.ApprovalNotes = notes?.Trim();
            request.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            await _audit.LogAsync(approve ? "Approved" : "Rejected", "ServiceRequest", id, notes ?? string.Empty);
            await NotifyRequesterAsync(request, approve ? "approved" : "rejected", approve ? "success" : "error");
            TempData["Success"] = approve ? "Request approved." : "Request rejected.";
            return RedirectToAction(nameof(RequestDetails), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRequest(int id, ServiceRequestStatus status, int? assignedToId,
            string? fulfillmentNotes, string? holdReason)
        {
            if (!CanManage) return Forbid();
            var request = await _db.ServiceRequests.FindAsync(id);
            if (request == null) return NotFound();
            if (!ServiceRequestWorkflow.CanTransition(request.Status, status))
            {
                TempData["Error"] = $"Cannot move a request from {request.Status} to {status}.";
                return RedirectToAction(nameof(RequestDetails), new { id });
            }

            request.Status = status;
            request.AssignedToId = assignedToId;
            request.FulfillmentNotes = fulfillmentNotes?.Trim();
            request.HoldReason = status == ServiceRequestStatus.OnHold ? holdReason?.Trim() : null;
            request.UpdatedAt = DateTime.Now;
            if (status == ServiceRequestStatus.Fulfilled) request.FulfilledAt = DateTime.Now;
            await _db.SaveChangesAsync();
            await _audit.LogAsync("StatusChanged", "ServiceRequest", id, $"Status: {status}");
            await NotifyRequesterAsync(request, $"updated to {status}", status == ServiceRequestStatus.Fulfilled ? "success" : "info");
            TempData["Success"] = $"Request updated to {status}.";
            return RedirectToAction(nameof(RequestDetails), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var request = await _db.ServiceRequests.FindAsync(id);
            if (request == null) return NotFound();
            if (!CanManage && request.RequestedById != Uid) return Forbid();
            if (!ServiceRequestWorkflow.CanTransition(request.Status, ServiceRequestStatus.Cancelled))
            {
                TempData["Error"] = "This request can no longer be cancelled.";
                return RedirectToAction(nameof(RequestDetails), new { id });
            }
            request.Status = ServiceRequestStatus.Cancelled;
            request.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Cancelled", "ServiceRequest", id);
            TempData["Success"] = "Request cancelled.";
            return RedirectToAction(nameof(RequestDetails), new { id });
        }

        public async Task<IActionResult> Manage()
        {
            if (!CanManage) return Forbid();
            ViewBag.Owners = await _db.Users.Where(u => u.IsActive &&
                    (u.Role == UserRole.Admin || u.Role == UserRole.SystemsAdmin))
                .OrderBy(u => u.FirstName).ToListAsync();
            return View(await _db.ServiceCatalogItems.Include(i => i.Owner).OrderBy(i => i.Category).ThenBy(i => i.Name).ToListAsync());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveItem(int id, string name, string summary, string? description,
            string category, string? icon, TicketPriority defaultPriority, int fulfillmentTargetMinutes,
            bool requiresApproval, bool isPublished, int? ownerId)
        {
            if (!CanManage) return Forbid();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(summary) || string.IsNullOrWhiteSpace(category)
                || fulfillmentTargetMinutes < 1)
            {
                TempData["Error"] = "Name, summary, category and a positive target are required.";
                return RedirectToAction(nameof(Manage));
            }

            var item = id == 0 ? new ServiceCatalogItem { CreatedAt = DateTime.Now } :
                await _db.ServiceCatalogItems.FindAsync(id);
            if (item == null) return NotFound();
            item.Name = name.Trim();
            item.Summary = summary.Trim();
            item.Description = description?.Trim();
            item.Category = category.Trim();
            item.Icon = string.IsNullOrWhiteSpace(icon) ? "fa-concierge-bell" : icon.Trim();
            item.DefaultPriority = defaultPriority;
            item.FulfillmentTargetMinutes = fulfillmentTargetMinutes;
            item.RequiresApproval = requiresApproval;
            item.IsPublished = isPublished;
            item.OwnerId = ownerId;
            item.UpdatedAt = DateTime.Now;
            if (id == 0) _db.ServiceCatalogItems.Add(item);
            await _db.SaveChangesAsync();
            await _audit.LogAsync(id == 0 ? "Created" : "Updated", "ServiceCatalogItem", item.Id, item.Name);
            TempData["Success"] = $"Catalogue item '{item.Name}' saved.";
            return RedirectToAction(nameof(Manage));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleItem(int id)
        {
            if (!CanManage) return Forbid();
            var item = await _db.ServiceCatalogItems.FindAsync(id);
            if (item == null) return NotFound();
            item.IsPublished = !item.IsPublished;
            item.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["Success"] = $"'{item.Name}' is now {(item.IsPublished ? "published" : "hidden")}.";
            return RedirectToAction(nameof(Manage));
        }

        private Task NotifyRequesterAsync(ServiceRequest request, string change, string level) =>
            _realtime.NotifyUserAsync(request.RequestedById,
                new RealtimeNotice($"Service request {request.Reference}", $"Your request was {change}.",
                    Url.Action(nameof(RequestDetails), new { id = request.Id }), level));
    }
}
