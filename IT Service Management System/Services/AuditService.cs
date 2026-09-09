using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using IT_Service_Management_System.Services.Auditing;

namespace IT_Service_Management_System.Services
{
    /// <summary>
    /// Writes the audit entries that describe things which are not row changes — logins, MFA
    /// challenges, exports, approvals, backup runs. Row changes are captured automatically by
    /// <see cref="AuditInterceptor"/>; both halves go through <see cref="AuditWriter"/> so they
    /// share one hash-chained, chronological trail.
    ///
    /// Request-bound data (user, IP, user-agent) is captured synchronously, then the geo lookup and
    /// the database write run on the background queue so they never block the originating request.
    /// </summary>
    public class AuditService
    {
        private readonly AuditContextProvider _context;
        private readonly IBackgroundTaskQueue _queue;
        private readonly ILogger<AuditService> _logger;

        public AuditService(
            AuditContextProvider context,
            IBackgroundTaskQueue queue,
            ILogger<AuditService> logger)
        {
            _context = context;
            _queue = queue;
            _logger = logger;
        }

        public Task LogAsync(string action, string entity, int? entityId = null, string details = "")
            => LogAsync(action, entity, entityId, details, null);

        /// <summary>
        /// Records an event, optionally with the field-level before/after values behind it — used
        /// where a workflow step changes state that no single entity write captures.
        /// </summary>
        public Task LogAsync(
            string action,
            string entity,
            int? entityId,
            string details,
            IReadOnlyList<AuditFieldChange>? changes)
        {
            try
            {
                var request = _context.Capture();
                var entry = new AuditLog
                {
                    // Historically this column held "System" for unauthenticated writes; the
                    // capture keeps that shape so old and new rows read alike.
                    UserId = request.UserId == "Anonymous" ? "System" : request.UserId,
                    // The role used to be appended to the name here. It has its own column now,
                    // and keeping the name plain means one person is one value in the user filter
                    // whether the entry came from a login or from a data change.
                    UserName = request.UserName,
                    UserRole = request.UserRole,
                    Action = action,
                    Entity = entity,
                    EntityId = entityId,
                    EntityKey = entityId?.ToString(),
                    Details = details,
                    Changes = changes is { Count: > 0 }
                        ? System.Text.Json.JsonSerializer.Serialize(changes)
                        : null,
                    Timestamp = DateTime.Now,
                    IpAddress = request.IpAddress,
                    Device = request.Device,
                    CorrelationId = request.CorrelationId,
                    RequestPath = request.RequestPath,
                    HttpMethod = request.HttpMethod,
                    Source = AuditSource.Manual
                };

                _queue.Enqueue(async (sp, ct) =>
                {
                    var db = sp.GetRequiredService<ApplicationDbContext>();
                    var geo = sp.GetRequiredService<GeoLocationService>();
                    var writer = sp.GetRequiredService<AuditWriter>();

                    entry.Location = await geo.ResolveAsync(entry.IpAddress);
                    await writer.AppendAsync(db, new[] { entry }, ct);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue audit log for action {Action} on {Entity}", action, entity);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Records that someone read or exported data. Reads are not captured automatically — the
        /// interceptor only sees writes — so bulk access to personal or security data is logged
        /// here explicitly.
        /// </summary>
        public Task LogAccessAsync(string entity, string details, int? recordCount = null)
            => LogAsync(
                "Accessed",
                entity,
                null,
                recordCount.HasValue ? $"{details} ({recordCount} records)" : details);
    }
}
