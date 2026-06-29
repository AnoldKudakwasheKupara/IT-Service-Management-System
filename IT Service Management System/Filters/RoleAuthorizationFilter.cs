using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace IT_Service_Management_System.Filters
{
    /// <summary>
    /// Global filter that enforces <see cref="RoleAuthorizeAttribute"/> based on the
    /// session role. Runs after <see cref="SessionAuthorizationFilter"/> (login check).
    /// Action-level role restrictions take precedence over controller-level ones, and
    /// <see cref="AllowAnyRoleAttribute"/> exempts an action entirely.
    /// </summary>
    public class RoleAuthorizationFilter : IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var endpoint = context.HttpContext.GetEndpoint();
            if (endpoint == null) return;

            // Anonymous endpoints are never role-gated.
            if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() != null) return;

            var metadata = context.ActionDescriptor.EndpointMetadata;

            // An explicit per-action escape hatch wins over a controller-level restriction.
            if (metadata.OfType<AllowAnyRoleAttribute>().Any()) return;

            // The most specific (last) RoleAuthorize attribute applies.
            var rule = metadata.OfType<RoleAuthorizeAttribute>().LastOrDefault();
            if (rule == null) return; // no restriction

            var role = context.HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrEmpty(role))
            {
                context.Result = new RedirectToActionResult("Login", "Account", null);
                return;
            }

            if (!rule.Roles.Contains(role))
                context.Result = new RedirectToActionResult("AccessDenied", "Home", null);
        }
    }
}
