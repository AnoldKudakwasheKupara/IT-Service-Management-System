using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Helpers.Efm;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Filters
{
    /// <summary>
    /// Populates the nav notification bell for every rendered page. Counts the current user's
    /// unread document notifications — their own, plus HR-group ones for staff — into
    /// <c>ViewData["EfmUnread"]</c>, which the layout reads. Runs only for view-returning GETs
    /// so it adds a single lightweight COUNT and never touches file downloads or POSTs.
    /// </summary>
    public class NotificationBadgeFilter : IAsyncActionFilter
    {
        private readonly ApplicationDbContext _db;

        public NotificationBadgeFilter(ApplicationDbContext db) => _db = db;

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var executed = await next();

            if (context.Controller is not Controller controller) return;
            if (!HttpMethods.IsGet(context.HttpContext.Request.Method)) return;
            if (executed.Result is not ViewResult) return;

            var session = context.HttpContext.Session;
            var uid = session.GetInt32("UserId");
            if (uid == null) return;

            var role = session.GetString("UserRole");
            var isStaff = EfmAccess.IsStaff(role);

            try
            {
                var count = await _db.DocumentNotifications.CountAsync(n => !n.IsRead &&
                    (n.RecipientUserId == uid || (isStaff && n.RecipientUserId == null)));
                controller.ViewData["EfmUnread"] = count;
                controller.ViewData["EfmUnreadStaff"] = isStaff;

                if (isStaff)
                {
                    controller.ViewData["EfmPendingApprovals"] =
                        await _db.DocumentApprovals.CountAsync(a => a.Status == Models.Efm.ApprovalStatus.Pending);
                    controller.ViewData["EfmPendingRequests"] =
                        await _db.DocumentRequests.CountAsync(r => r.Status == Models.Efm.DocumentRequestStatus.Pending);
                }
            }
            catch
            {
                // Never let the badge break a page render (e.g. during startup migration).
            }
        }
    }
}
