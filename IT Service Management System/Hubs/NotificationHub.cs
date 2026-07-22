using Microsoft.AspNetCore.SignalR;

namespace IT_Service_Management_System.Hubs
{
    /// <summary>
    /// Pushes live notifications (ticket events, document approvals, expiry alerts) to the browser.
    /// Each connection joins a per-user group and, for staff, a shared "staff" group, so the server
    /// can target an individual or the whole HR/IT group.
    /// </summary>
    public class NotificationHub : Hub
    {
        public static string UserGroup(int userId) => $"user-{userId}";
        public const string StaffGroup = "staff";

        public override async Task OnConnectedAsync()
        {
            var http = Context.GetHttpContext();
            var uid = http?.Session.GetInt32("UserId");
            var role = http?.Session.GetString("UserRole");

            if (uid != null)
                await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(uid.Value));

            if (role is "Admin" or "SystemsAdmin" or "HR" or "Auditor")
                await Groups.AddToGroupAsync(Context.ConnectionId, StaffGroup);

            await base.OnConnectedAsync();
        }
    }
}
