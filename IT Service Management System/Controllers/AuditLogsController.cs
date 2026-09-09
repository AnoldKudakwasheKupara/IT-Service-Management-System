using System.Text;
using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Models;
using IT_Service_Management_System.Services;
using IT_Service_Management_System.Services.Auditing;
using IT_Service_Management_System.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>
    /// Reading end of the audit trail: the filtered list, one entry's field-level diff, the full
    /// history of a single record, a CSV export and the tamper-evidence check.
    ///
    /// Everything here is read-only — entries are written by the interceptor and the audit service,
    /// and removed only by the retention job. Exports and integrity checks are themselves logged,
    /// since bulk access to the trail is exactly the kind of event a trail should record.
    /// </summary>
    [IT_Service_Management_System.Filters.RoleAuthorize(Roles.Admin, Roles.SystemsAdmin)]
    public class AuditLogsController : Controller
    {
        // Enough for a year of a mid-sized deployment without letting one click stream the table.
        private const int ExportRowLimit = 50_000;

        private readonly ApplicationDbContext _context;
        private readonly AuditService _audit;
        private readonly AuditWriter _writer;

        public AuditLogsController(ApplicationDbContext context, AuditService audit, AuditWriter writer)
        {
            _context = context;
            _audit = audit;
            _writer = writer;
        }

        // "action" is an MVC route token, so without [FromQuery] it binds to the route value
        // ("Index") and silently filters the whole trail down to nothing.
        public async Task<IActionResult> Index(
            int page = 1,
            string? q = null,
            string? user = null,
            [FromQuery(Name = "action")] string? actionFilter = null,
            string? entity = null,
            AuditSource? source = null,
            DateTime? from = null,
            DateTime? to = null)
        {
            var filter = new AuditFilter
            {
                Q = q,
                User = user,
                Action = actionFilter,
                Entity = entity,
                Source = source,
                From = from,
                To = to
            };

            var query = Filtered(filter);

            // Counts follow the filter, so narrowing to one user or one week answers
            // "what did they change" directly from the cards.
            var model = new AuditIndexViewModel
            {
                Filter = filter,
                TotalCount = await query.CountAsync(),
                CreatedCount = await query.CountAsync(a => a.Action == "Created"),
                UpdatedCount = await query.CountAsync(a => a.Action == "Updated"),
                DeletedCount = await query.CountAsync(a => a.Action == "Deleted"),
                Actions = await DistinctAsync(a => a.Action),
                Entities = await DistinctAsync(a => a.Entity),
                Users = await DistinctAsync(a => a.UserName)
            };

            var (items, paging) = await query.OrderByDescending(a => a.Timestamp)
                                             .ThenByDescending(a => a.Id)
                                             .PageAsync(page, 25);
            model.Entries = items;

            ViewBag.Paging = paging;
            return View(model);
        }

        /// <summary>One entry in full: its before/after values and the rest of its request.</summary>
        public async Task<IActionResult> Details(int id)
        {
            var entry = await _context.AuditLogs.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
            if (entry == null) return NotFound();

            var related = new List<AuditLog>();
            if (!string.IsNullOrEmpty(entry.CorrelationId))
            {
                related = await _context.AuditLogs.AsNoTracking()
                    .Where(a => a.CorrelationId == entry.CorrelationId && a.Id != entry.Id)
                    .OrderBy(a => a.Id)
                    .Take(50)
                    .ToListAsync();
            }

            // Cheap per-entry check: content hash plus the link to the row before it.
            var previousHash = await _context.AuditLogs.AsNoTracking()
                .Where(a => a.Id < entry.Id)
                .OrderByDescending(a => a.Id)
                .Select(a => a.Hash)
                .FirstOrDefaultAsync();

            return View(new AuditDetailsViewModel
            {
                Entry = entry,
                Changes = entry.ChangeList(),
                RelatedInRequest = related,
                HashValid = !string.IsNullOrEmpty(entry.Hash)
                            && entry.Hash == AuditWriter.ComputeHash(entry)
                            && entry.PreviousHash == previousHash
            });
        }

        /// <summary>Everything that ever happened to one record, oldest first.</summary>
        public async Task<IActionResult> Timeline(string entity, int id)
        {
            if (string.IsNullOrWhiteSpace(entity)) return NotFound();

            var entries = await _context.AuditLogs.AsNoTracking()
                .Where(a => a.Entity == entity && a.EntityId == id)
                .OrderBy(a => a.Timestamp).ThenBy(a => a.Id)
                .Take(500)
                .ToListAsync();

            if (entries.Count == 0) return NotFound();

            // The creating entry names the record best; fall back to the earliest entry.
            var subject = entries.FirstOrDefault(e => e.Action == "Created")?.Details
                          ?? entries[0].Details;

            return View(new AuditTimelineViewModel
            {
                Entity = entity,
                EntityId = id,
                Subject = subject,
                Entries = entries
            });
        }

        /// <summary>
        /// Streams the filtered trail as CSV, field-level changes included. The export is itself
        /// recorded — who pulled the trail, with which filter, and how many rows.
        /// </summary>
        public async Task<IActionResult> Export(
            string? q = null,
            string? user = null,
            [FromQuery(Name = "action")] string? actionFilter = null,
            string? entity = null,
            AuditSource? source = null,
            DateTime? from = null,
            DateTime? to = null)
        {
            var filter = new AuditFilter
            {
                Q = q, User = user, Action = actionFilter, Entity = entity, Source = source, From = from, To = to
            };

            var rows = await Filtered(filter)
                .OrderByDescending(a => a.Timestamp).ThenByDescending(a => a.Id)
                .Take(ExportRowLimit)
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("Id,Timestamp,User,Role,Action,Entity,Record,Details,Changes,Source,IP Address,Location,Device,Request,Hash");

            foreach (var row in rows)
            {
                var changes = string.Join(" | ", row.ChangeList()
                    .Select(c => $"{c.Field}: {c.Old ?? "(none)"} -> {c.New ?? "(none)"}"));

                csv.AppendLine(string.Join(',', new[]
                {
                    Csv(row.Id.ToString()),
                    Csv(row.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")),
                    Csv(row.UserName),
                    Csv(row.UserRole),
                    Csv(row.Action),
                    Csv(row.Entity),
                    Csv(row.EntityKey ?? row.EntityId?.ToString()),
                    Csv(row.Details),
                    Csv(changes),
                    Csv(row.Source.ToString()),
                    Csv(row.IpAddress),
                    Csv(row.Location),
                    Csv(row.Device),
                    Csv(row.HttpMethod == null ? row.RequestPath : $"{row.HttpMethod} {row.RequestPath}"),
                    Csv(row.Hash)
                }));
            }

            await _audit.LogAsync("Exported", nameof(AuditLog), null,
                $"Exported {rows.Count} audit entries ({filter.Describe()})");

            var fileName = $"audit-trail-{DateTime.Now:yyyyMMdd-HHmm}.csv";
            // The BOM keeps Excel from mangling non-ASCII names and locations.
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
            return File(bytes, "text/csv", fileName);
        }

        /// <summary>
        /// Recomputes the hash chain and reports the first break. A clean result is evidence the
        /// stored trail has not been edited or thinned since it was written.
        /// </summary>
        public async Task<IActionResult> Integrity()
        {
            var result = await _writer.VerifyAsync(_context);

            AuditLog? broken = null;
            if (result.FirstBrokenId.HasValue)
                broken = await _context.AuditLogs.AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Id == result.FirstBrokenId.Value);

            await _audit.LogAsync("Integrity Check", nameof(AuditLog), null,
                result.IsIntact
                    ? $"Verified {result.Checked} entries — chain intact"
                    : $"Verified {result.Checked} entries — chain broken at #{result.FirstBrokenId}");

            return View(new AuditIntegrityViewModel { Result = result, BrokenEntry = broken });
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private IQueryable<AuditLog> Filtered(AuditFilter filter)
        {
            IQueryable<AuditLog> query = _context.AuditLogs.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(filter.Q))
            {
                var text = filter.Q.Trim();
                query = query.Where(a =>
                    a.Action.Contains(text) || a.UserName.Contains(text) ||
                    a.Details.Contains(text) || a.Entity.Contains(text) ||
                    a.IpAddress.Contains(text));
            }

            if (!string.IsNullOrWhiteSpace(filter.User))
                query = query.Where(a => a.UserName == filter.User);

            if (!string.IsNullOrWhiteSpace(filter.Action))
                query = query.Where(a => a.Action == filter.Action);

            if (!string.IsNullOrWhiteSpace(filter.Entity))
                query = query.Where(a => a.Entity == filter.Entity);

            if (filter.Source.HasValue)
                query = query.Where(a => a.Source == filter.Source.Value);

            if (filter.From.HasValue)
                query = query.Where(a => a.Timestamp >= filter.From.Value.Date);

            if (filter.To.HasValue)
            {
                // An inclusive end date: "to 3 March" must include everything that day.
                var end = filter.To.Value.Date.AddDays(1);
                query = query.Where(a => a.Timestamp < end);
            }

            return query;
        }

        /// <summary>
        /// Distinct values for a filter dropdown, taken from the recent trail rather than the whole
        /// table so the query stays cheap as the trail grows.
        /// </summary>
        private async Task<List<string>> DistinctAsync(
            System.Linq.Expressions.Expression<Func<AuditLog, string>> selector)
        {
            var since = DateTime.Now.AddDays(-90);
            return await _context.AuditLogs.AsNoTracking()
                .Where(a => a.Timestamp >= since)
                .Select(selector)
                .Distinct()
                .OrderBy(v => v)
                .Take(200)
                .ToListAsync();
        }

        private static string Csv(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            // Neutralise spreadsheet formula injection before the usual CSV quoting.
            if ("=+-@".Contains(value[0])) value = "'" + value;
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
    }
}
