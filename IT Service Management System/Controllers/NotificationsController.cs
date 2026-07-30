using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Helpers.Efm;
using IT_Service_Management_System.Helpers.Ims;
using IT_Service_Management_System.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>
    /// The unified notification centre. Each module keeps its own notification table, but the bell
    /// needs a single place to land: this controller merges them into one inbox scoped to the
    /// signed-in user, so the badge count and the list the user opens always agree.
    /// </summary>
    /// <remarks>
    /// Deliberately carries no [RoleAuthorize]: every signed-in user has notifications. The global
    /// SessionAuthorizationFilter still requires a valid session, and each notification is scoped
    /// to the caller below.
    /// </remarks>
    public class NotificationsController : Controller
    {
        private readonly ApplicationDbContext _db;

        public NotificationsController(ApplicationDbContext db) => _db = db;

        private int? Uid => HttpContext.Session.GetInt32("UserId");
        private string? Role => HttpContext.Session.GetString("UserRole");

        public async Task<IActionResult> Index(bool unreadOnly = false)
        {
            var uid = Uid;
            if (uid == null) return RedirectToAction("Login", "Account");

            var items = await GatherAsync(uid.Value, Role, unreadOnly);
            return View(new NotificationInboxVm { Items = items, UnreadOnly = unreadOnly });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(NotificationSource source, int id, bool unreadOnly = false)
        {
            var uid = Uid;
            if (uid == null) return RedirectToAction("Login", "Account");

            if (source == NotificationSource.Documents)
            {
                var n = await _db.DocumentNotifications.FirstOrDefaultAsync(x => x.Id == id);
                if (n == null) return NotFound();
                if (!CanSeeDocumentNotification(n, uid.Value)) return RedirectToAction("AccessDenied", "Home");
                if (!n.IsRead) { n.IsRead = true; n.ReadAt = DateTime.Now; }
            }
            else
            {
                var n = await _db.IsoNotifications.FirstOrDefaultAsync(x => x.Id == id);
                if (n == null) return NotFound();
                if (!CanSeeIsoNotification(n, uid.Value)) return RedirectToAction("AccessDenied", "Home");
                if (!n.IsRead) n.IsRead = true;
            }

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { unreadOnly });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead()
        {
            var uid = Uid;
            if (uid == null) return RedirectToAction("Login", "Account");

            var isEfmStaff = EfmAccess.IsStaff(Role);
            var isImsManager = ImsAccess.IsImsManager(Role);

            var docs = await _db.DocumentNotifications
                .Where(n => !n.IsRead && (n.RecipientUserId == uid || (isEfmStaff && n.RecipientUserId == null)))
                .ToListAsync();
            foreach (var n in docs) { n.IsRead = true; n.ReadAt = DateTime.Now; }

            var isos = await _db.IsoNotifications
                .Where(n => !n.IsRead && (n.RecipientUserId == uid || (isImsManager && n.RecipientUserId == null)))
                .ToListAsync();
            foreach (var n in isos) n.IsRead = true;

            await _db.SaveChangesAsync();
            TempData["Success"] = $"{docs.Count + isos.Count} notification(s) marked as read.";
            return RedirectToAction(nameof(Index));
        }

        // ── scoping ──────────────────────────────────────────────────────────────
        // A null recipient is a broadcast to that module's staff group, so only those
        // roles may see (or dismiss) it.
        private bool CanSeeDocumentNotification(Models.Efm.DocumentNotification n, int uid) =>
            n.RecipientUserId == uid || (n.RecipientUserId == null && EfmAccess.IsStaff(Role));

        private bool CanSeeIsoNotification(Models.Ims.IsoNotification n, int uid) =>
            n.RecipientUserId == uid || (n.RecipientUserId == null && ImsAccess.IsImsManager(Role));

        private async Task<List<NotificationItem>> GatherAsync(int uid, string? role, bool unreadOnly)
        {
            var isEfmStaff = EfmAccess.IsStaff(role);
            var isImsManager = ImsAccess.IsImsManager(role);

            var docQuery = _db.DocumentNotifications.AsNoTracking()
                .Where(n => n.RecipientUserId == uid || (isEfmStaff && n.RecipientUserId == null));
            if (unreadOnly) docQuery = docQuery.Where(n => !n.IsRead);

            var docs = await docQuery
                .OrderByDescending(n => n.CreatedAt).Take(100)
                .Select(n => new
                {
                    n.Id, n.Title, n.Message, n.CreatedAt, n.IsRead,
                    Kind = n.Type, n.EmployeeDocumentId, n.EmployeeId
                })
                .ToListAsync();

            var isoQuery = _db.IsoNotifications.AsNoTracking()
                .Where(n => n.RecipientUserId == uid || (isImsManager && n.RecipientUserId == null));
            if (unreadOnly) isoQuery = isoQuery.Where(n => !n.IsRead);

            var isos = await isoQuery
                .OrderByDescending(n => n.CreatedAt).Take(100)
                .Select(n => new { n.Id, n.Title, n.Message, n.CreatedAt, n.IsRead, Kind = n.Type, n.Url })
                .ToListAsync();

            var items = new List<NotificationItem>(docs.Count + isos.Count);

            items.AddRange(docs.Select(n => new NotificationItem
            {
                Source = NotificationSource.Documents,
                Id = n.Id,
                Kind = Humanise(n.Kind.ToString()),
                Title = n.Title,
                Message = n.Message,
                CreatedAt = n.CreatedAt,
                IsRead = n.IsRead,
                Icon = "fa-folder-open",
                // DocumentNotification stores relations rather than a link, so derive one.
                Url = n.EmployeeDocumentId != null ? $"/EmployeeDocuments/Details/{n.EmployeeDocumentId}"
                    : n.EmployeeId != null ? $"/EmployeeDocuments/File/{n.EmployeeId}"
                    : null
            }));

            items.AddRange(isos.Select(n => new NotificationItem
            {
                Source = NotificationSource.Iso,
                Id = n.Id,
                Kind = Humanise(n.Kind.ToString()),
                Title = n.Title,
                Message = n.Message,
                CreatedAt = n.CreatedAt,
                IsRead = n.IsRead,
                Icon = "fa-certificate",
                Url = n.Url
            }));

            return items
                .OrderBy(i => i.IsRead)                 // unread first
                .ThenByDescending(i => i.CreatedAt)
                .ToList();
        }

        /// <summary>"AcknowledgementRequired" -> "Acknowledgement Required".</summary>
        private static string Humanise(string pascal) =>
            System.Text.RegularExpressions.Regex.Replace(pascal, "(?<!^)([A-Z])", " $1");
    }
}
