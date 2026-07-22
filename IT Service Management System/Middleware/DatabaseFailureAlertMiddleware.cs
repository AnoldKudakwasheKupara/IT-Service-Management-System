using IT_Service_Management_System.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace IT_Service_Management_System.Middleware
{
    /// <summary>
    /// Catches database-related exceptions bubbling out of the request pipeline and raises a
    /// throttled admin "Database failure" alert, then rethrows so normal error handling proceeds.
    /// </summary>
    public class DatabaseFailureAlertMiddleware
    {
        private const string ThrottleKey = "db-failure-alerted";
        private readonly RequestDelegate _next;
        private readonly ILogger<DatabaseFailureAlertMiddleware> _logger;

        public DatabaseFailureAlertMiddleware(RequestDelegate next, ILogger<DatabaseFailureAlertMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex) when (IsDatabaseError(ex))
            {
                var cache = context.RequestServices.GetRequiredService<IMemoryCache>();
                if (!cache.TryGetValue(ThrottleKey, out _))
                {
                    cache.Set(ThrottleKey, true, TimeSpan.FromMinutes(15));
                    try
                    {
                        var alerts = context.RequestServices.GetRequiredService<AlertService>();
                        await alerts.DatabaseFailureAsync($"{ex.GetType().Name}: {ex.Message}");
                    }
                    catch (Exception alertEx)
                    {
                        _logger.LogError(alertEx, "Failed to dispatch database-failure alert");
                    }
                }

                throw; // let the normal exception handler render the error page
            }
        }

        private static bool IsDatabaseError(Exception ex) =>
            ex is DbUpdateException || ex is SqlException ||
            ex.InnerException is SqlException;
    }
}
