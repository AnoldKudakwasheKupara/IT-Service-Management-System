using System.Collections;
using System.Text.Json;
using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IT_Service_Management_System.Services.Auditing
{
    /// <summary>
    /// Turns every tracked insert, update and delete into an audit entry, so the trail covers the
    /// whole application rather than only the places someone remembered to call the audit service.
    ///
    /// Timing matters in two directions: before-values and modified flags only exist *before*
    /// SaveChanges, while generated primary keys only exist *after* it. So changes are collected in
    /// <see cref="SavingChangesAsync"/>, keys are resolved in <see cref="SavedChangesAsync"/>, and
    /// only then is the write handed to the background queue — the audited request never waits for
    /// the geo lookup or the audit insert.
    /// </summary>
    public class AuditInterceptor : SaveChangesInterceptor
    {
        private readonly AuditContextProvider _context;
        private readonly IBackgroundTaskQueue _queue;
        private readonly AuditOptions _options;
        private readonly ILogger<AuditInterceptor> _logger;

        // Pending entries for the save currently in flight. The interceptor is scoped to the same
        // lifetime as its DbContext, and EF does not run two saves on one context concurrently.
        private readonly List<PendingAudit> _pending = new();

        public AuditInterceptor(
            AuditContextProvider context,
            IBackgroundTaskQueue queue,
            AuditOptions options,
            ILogger<AuditInterceptor> logger)
        {
            _context = context;
            _queue = queue;
            _options = options;
            _logger = logger;
        }

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData, InterceptionResult<int> result)
        {
            Collect(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Collect(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            Dispatch();
            return base.SavedChanges(eventData, result);
        }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            Dispatch();
            return base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        public override void SaveChangesFailed(DbContextErrorEventData eventData)
        {
            // The change never landed, so there is nothing to record.
            _pending.Clear();
            base.SaveChangesFailed(eventData);
        }

        public override Task SaveChangesFailedAsync(
            DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            _pending.Clear();
            return base.SaveChangesFailedAsync(eventData, cancellationToken);
        }

        // ── Collection ───────────────────────────────────────────────────────────

        private void Collect(DbContext? db)
        {
            _pending.Clear();
            if (db == null || !_options.CaptureEntityChanges) return;

            try
            {
                foreach (var entry in db.ChangeTracker.Entries())
                {
                    if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                        continue;

                    var entityName = entry.Metadata.ClrType.Name;
                    if (_options.IsExcluded(entityName)) continue;

                    var pending = Describe(entry, entityName);
                    if (pending != null) _pending.Add(pending);
                }
            }
            catch (Exception ex)
            {
                // Never let auditing break the save it is observing.
                _logger.LogError(ex, "Failed to collect audit changes; the save itself is unaffected");
                _pending.Clear();
            }
        }

        private PendingAudit? Describe(EntityEntry entry, string entityName)
        {
            var changes = new List<AuditFieldChange>();
            var action = entry.State switch
            {
                EntityState.Added => "Created",
                EntityState.Deleted => "Deleted",
                _ => "Updated"
            };

            foreach (var property in entry.Properties)
            {
                var name = property.Metadata.Name;
                if (_options.IsIgnored(name)) continue;

                if (entry.State == EntityState.Added)
                {
                    var value = Render(property.CurrentValue, name);
                    // Unset columns on a new row are noise; only record what was actually given.
                    if (value != null)
                        changes.Add(new AuditFieldChange { Field = name, Old = null, New = value });
                }
                else if (entry.State == EntityState.Deleted)
                {
                    var value = Render(property.OriginalValue, name);
                    if (value != null)
                        changes.Add(new AuditFieldChange { Field = name, Old = value, New = null });
                }
                else
                {
                    if (!property.IsModified) continue;
                    var oldValue = Render(property.OriginalValue, name);
                    var newValue = Render(property.CurrentValue, name);
                    // EF marks a property modified on assignment even when the value is unchanged;
                    // comparing keeps those out of the trail.
                    if (oldValue == newValue) continue;
                    changes.Add(new AuditFieldChange { Field = name, Old = oldValue, New = newValue });
                }
            }

            // An update that changed nothing observable is not an event.
            if (entry.State == EntityState.Modified && changes.Count == 0) return null;

            if (action == "Updated" && _options.TreatSoftDeleteAsDelete && IsSoftDelete(entry, changes))
                action = "Deleted";

            return new PendingAudit(entry, entityName, action, changes, DescribeSubject(entry));
        }

        /// <summary>An update that flips IsDeleted on is a deletion as far as the reader is concerned.</summary>
        private static bool IsSoftDelete(EntityEntry entry, List<AuditFieldChange> changes)
        {
            if (entry.Entity is not ISoftDelete) return false;
            return changes.Any(c => c.Field == nameof(ISoftDelete.IsDeleted) &&
                                    string.Equals(c.New, "True", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// A human label for the row, so the trail reads "Ticket INC-0042" rather than "Ticket 17".
        /// Uses the first recognisable naming property the entity happens to have.
        /// </summary>
        private static string? DescribeSubject(EntityEntry entry)
        {
            string[] candidates = { "Reference", "Name", "Title", "Subject", "Email", "Code", "FullName" };
            foreach (var candidate in candidates)
            {
                var property = entry.Properties.FirstOrDefault(p =>
                    string.Equals(p.Metadata.Name, candidate, StringComparison.Ordinal));
                if (property == null) continue;

                var value = entry.State == EntityState.Deleted
                    ? property.OriginalValue
                    : property.CurrentValue;

                if (value is string text && !string.IsNullOrWhiteSpace(text))
                    return text.Length > 100 ? text[..100] : text;
            }
            return null;
        }

        private string? Render(object? value, string propertyName)
        {
            if (value == null) return null;
            if (_options.IsRedacted(propertyName)) return AuditFieldChange.Redacted;

            var text = value switch
            {
                bool b => b ? "True" : "False",
                DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss"),
                DateTimeOffset dto => dto.ToString("yyyy-MM-dd HH:mm:ss zzz"),
                decimal or double or float => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
                byte[] bytes => $"<binary, {bytes.Length} bytes>",
                Enum e => e.ToString(),
                string s => s,
                IEnumerable list => string.Join(", ", list.Cast<object?>().Take(20)),
                _ => value.ToString()
            };

            if (string.IsNullOrEmpty(text)) return text;
            return text.Length > _options.MaxValueLength
                ? text[.._options.MaxValueLength] + "…"
                : text;
        }

        // ── Dispatch ─────────────────────────────────────────────────────────────

        private void Dispatch()
        {
            if (_pending.Count == 0) return;

            List<AuditLog> entries;
            AuditRequestInfo request;
            try
            {
                // Keys generated by the database only exist now that the save has completed.
                request = _context.Capture();
                var timestamp = DateTime.Now;
                entries = _pending.Select(p => p.ToAuditLog(request, timestamp)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to build audit entries after save");
                return;
            }
            finally
            {
                _pending.Clear();
            }

            _queue.Enqueue(async (sp, ct) =>
            {
                var db = sp.GetRequiredService<ApplicationDbContext>();
                var geo = sp.GetRequiredService<GeoLocationService>();
                var writer = sp.GetRequiredService<AuditWriter>();

                // One lookup for the whole batch — every entry shares the originating request.
                var location = await geo.ResolveAsync(request.IpAddress);
                foreach (var entry in entries) entry.Location = location;

                await writer.AppendAsync(db, entries, ct);
            });
        }

        /// <summary>A change captured before the save, awaiting its generated key and request context.</summary>
        private sealed record PendingAudit(
            EntityEntry Entry,
            string EntityName,
            string Action,
            List<AuditFieldChange> Changes,
            string? Subject)
        {
            public AuditLog ToAuditLog(AuditRequestInfo request, DateTime timestamp)
            {
                var (entityId, entityKey) = ReadKey();

                return new AuditLog
                {
                    UserId = request.UserId,
                    UserName = request.UserName,
                    UserRole = request.UserRole,
                    Action = Action,
                    Entity = EntityName,
                    EntityId = entityId,
                    EntityKey = entityKey,
                    Details = Subject == null
                        ? $"{Action} {EntityName}"
                        : $"{Action} {EntityName}: {Subject}",
                    Changes = Changes.Count == 0 ? null : JsonSerializer.Serialize(Changes),
                    Timestamp = timestamp,
                    IpAddress = request.IpAddress,
                    Device = request.Device,
                    CorrelationId = request.CorrelationId,
                    RequestPath = request.RequestPath,
                    HttpMethod = request.HttpMethod,
                    Source = AuditSource.Automatic
                };
            }

            private (int? Id, string? Key) ReadKey()
            {
                var keyProperties = Entry.Metadata.FindPrimaryKey()?.Properties;
                if (keyProperties == null || keyProperties.Count == 0) return (null, null);

                var values = keyProperties
                    .Select(p => Entry.Property(p.Name).CurrentValue)
                    .ToList();

                var key = string.Join(", ", values.Select(v => v?.ToString() ?? "null"));
                var id = values.Count == 1 && values[0] is int single ? single : (int?)null;
                return (id, key);
            }
        }
    }
}
