using System.Security.Cryptography;
using System.Text;
using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Services.Auditing
{
    /// <summary>
    /// The single point where audit entries reach the database.
    ///
    /// Every entry is hash-chained to its predecessor: Hash = SHA-256(content ‖ PreviousHash).
    /// Editing or deleting any row therefore breaks every hash after it, which
    /// <see cref="VerifyAsync"/> detects and localises. Writes are serialised through a semaphore
    /// so concurrent requests cannot interleave and fork the chain.
    ///
    /// Registered as a singleton: the lock has to be process-wide. A DbContext is passed in per
    /// call rather than held, since the writer outlives any scope.
    /// </summary>
    public class AuditWriter
    {
        private readonly SemaphoreSlim _chainLock = new(1, 1);
        private readonly ILogger<AuditWriter> _logger;

        public AuditWriter(ILogger<AuditWriter> logger) => _logger = logger;

        /// <summary>
        /// Appends entries in order, chaining each to the last row already stored. Failures are
        /// logged and swallowed — an audit write must never take down the operation it describes.
        /// </summary>
        public async Task AppendAsync(ApplicationDbContext db, IReadOnlyList<AuditLog> entries, CancellationToken ct = default)
        {
            if (entries.Count == 0) return;

            await _chainLock.WaitAsync(ct);
            try
            {
                // Re-read the tail inside the lock rather than caching it, so a write from another
                // process (or a failed batch) cannot leave us chaining onto a stale hash.
                var previousHash = await db.AuditLogs
                    .AsNoTracking()
                    .OrderByDescending(a => a.Id)
                    .Select(a => a.Hash)
                    .FirstOrDefaultAsync(ct);

                foreach (var entry in entries)
                {
                    entry.PreviousHash = previousHash;
                    entry.Hash = ComputeHash(entry);
                    previousHash = entry.Hash;
                    db.AuditLogs.Add(entry);
                }

                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write {Count} audit entries", entries.Count);
            }
            finally
            {
                _chainLock.Release();
            }
        }

        /// <summary>
        /// Recomputes the chain over the stored rows and reports the first break, if any.
        /// Reads in id order in batches so a long trail does not have to fit in memory.
        /// </summary>
        public async Task<AuditIntegrityResult> VerifyAsync(ApplicationDbContext db, int batchSize = 500, CancellationToken ct = default)
        {
            var result = new AuditIntegrityResult();
            string? expectedPrevious = null;
            int lastId = 0;

            while (true)
            {
                var batch = await db.AuditLogs
                    .AsNoTracking()
                    .Where(a => a.Id > lastId)
                    .OrderBy(a => a.Id)
                    .Take(batchSize)
                    .ToListAsync(ct);

                if (batch.Count == 0) break;

                foreach (var entry in batch)
                {
                    result.Checked++;
                    lastId = entry.Id;

                    // Rows written before hashing was introduced carry no hash; they are counted
                    // separately rather than reported as tampering.
                    if (string.IsNullOrEmpty(entry.Hash))
                    {
                        result.Unhashed++;
                        continue;
                    }

                    if (entry.PreviousHash != expectedPrevious && result.FirstBrokenId == null)
                    {
                        result.FirstBrokenId = entry.Id;
                        result.Problem = "An entry is missing or was removed — the chain link does not match.";
                    }
                    else if (ComputeHash(entry) != entry.Hash && result.FirstBrokenId == null)
                    {
                        result.FirstBrokenId = entry.Id;
                        result.Problem = "An entry's contents no longer match its recorded hash.";
                    }

                    expectedPrevious = entry.Hash;
                }
            }

            result.LastId = lastId;
            return result;
        }

        /// <summary>
        /// Canonical serialisation of the fields an entry asserts. Any change to a captured value
        /// changes the hash; presentational fields are excluded so they cannot silently matter.
        /// </summary>
        public static string ComputeHash(AuditLog e)
        {
            var payload = string.Join('\u001F',
                // A fixed, offset-free format. DateTime.Now is Kind=Local, and "O" would append a
                // UTC offset — but the same value read back from datetime2 is Kind=Unspecified and
                // renders without one, so "O" would make every entry fail its own verification.
                e.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffff",
                    System.Globalization.CultureInfo.InvariantCulture),
                e.UserId, e.UserName, e.UserRole ?? string.Empty,
                e.Action, e.Entity,
                e.EntityId?.ToString() ?? string.Empty,
                e.EntityKey ?? string.Empty,
                e.Details, e.Changes ?? string.Empty,
                e.IpAddress, e.Device ?? string.Empty,
                e.CorrelationId ?? string.Empty,
                e.RequestPath ?? string.Empty,
                e.HttpMethod ?? string.Empty,
                ((int)e.Source).ToString(),
                e.PreviousHash ?? string.Empty);

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        }
    }

    /// <summary>Outcome of a chain verification pass.</summary>
    public class AuditIntegrityResult
    {
        public int Checked { get; set; }
        public int Unhashed { get; set; }
        public int LastId { get; set; }
        public int? FirstBrokenId { get; set; }
        public string? Problem { get; set; }
        public bool IsIntact => FirstBrokenId == null;
        public DateTime VerifiedAt { get; } = DateTime.Now;
    }
}
