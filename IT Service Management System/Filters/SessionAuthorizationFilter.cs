using IT_Service_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace IT_Service_Management_System.Filters
{
    /// <summary>
    /// Global filter that requires a valid, non-revoked session for every MVC action
    /// unless the action or controller is decorated with [AllowAnonymous].
    /// Revocation covers logout, "log out everywhere", and invalidate-after-password-change.
    /// </summary>
    public class SessionAuthorizationFilter : IAsyncAuthorizationFilter
    {
        private readonly SessionService _sessions;

        public SessionAuthorizationFilter(SessionService sessions)
        {
            _sessions = sessions;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var endpoint = context.HttpContext.GetEndpoint();
            if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() != null)
                return;

            var userId = context.HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                context.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }

            // Session exists in cookie — confirm it's still valid in the database.
            if (!await _sessions.ValidateCurrentAsync())
            {
                context.HttpContext.Session.Clear();
                context.Result = new RedirectToActionResult("Login", "Account",
                    new { expired = true });
            }
        }
    }
}
