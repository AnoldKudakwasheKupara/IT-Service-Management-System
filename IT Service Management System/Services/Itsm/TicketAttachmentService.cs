using System.Security.Cryptography;
using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using IT_Service_Management_System.Services.Security;

namespace IT_Service_Management_System.Services.Itsm
{
    /// <summary>
    /// Validates, scans and stores ticket attachments. Replaces the previous inline save that wrote
    /// any uploaded file straight to <c>wwwroot/uploads</c> unchecked. Every file is now:
    ///   • size-capped and checked against a safe extension allow-list (no .html/.svg/.js/executables,
    ///     which — being served from the web root — would otherwise be a stored-XSS vector);
    ///   • scanned by the configured <see cref="IMalwareScanner"/>; and
    ///   • content-addressed (stored under its SHA-256) so identical uploads are de-duplicated on disk.
    /// Files remain under wwwroot/uploads (linked directly by the ticket views); the allow-list plus the
    /// global <c>X-Content-Type-Options: nosniff</c> header are what make direct serving safe.
    /// </summary>
    public class TicketAttachmentService
    {
        private readonly ApplicationDbContext _db;
        private readonly IMalwareScanner _scanner;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<TicketAttachmentService> _logger;

        public TicketAttachmentService(ApplicationDbContext db, IMalwareScanner scanner,
            IWebHostEnvironment env, ILogger<TicketAttachmentService> logger)
        {
            _db = db;
            _scanner = scanner;
            _env = env;
            _logger = logger;
        }

        // Documents + images a requester or agent legitimately attaches to a ticket. Deliberately
        // excludes anything the browser would execute in our origin (.html, .htm, .svg, .xml, .js).
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
            ".txt", ".csv", ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp",
            ".zip", ".msg", ".eml", ".log"
        };

        private const long MaxFileBytes = 25 * 1024 * 1024; // 25 MB per file

        /// <summary>Outcome of a save: how many files were stored and any human-readable rejection reasons.</summary>
        public record SaveResult(int Saved, IReadOnlyList<string> Skipped)
        {
            public bool AnyRejected => Skipped.Count > 0;
            public static readonly SaveResult Empty = new(0, Array.Empty<string>());
        }

        /// <summary>
        /// Vets each file, stores the clean ones and adds their <see cref="TicketAttachment"/> rows to the
        /// context (a single SaveChanges at the end). Exactly one of <paramref name="ticketId"/> /
        /// <paramref name="ticketMessageId"/> should be supplied. Never throws on a bad file — it is skipped
        /// and reported so the caller can surface the reason.
        /// </summary>
        public async Task<SaveResult> SaveAsync(IEnumerable<IFormFile>? files,
            int? ticketId = null, int? ticketMessageId = null, CancellationToken ct = default)
        {
            var list = (files ?? Enumerable.Empty<IFormFile>()).Where(f => f != null && f.Length > 0).ToList();
            if (list.Count == 0) return SaveResult.Empty;

            var uploadPath = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(uploadPath);

            var skipped = new List<string>();
            int saved = 0;

            foreach (var file in list)
            {
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

                if (file.Length > MaxFileBytes)
                {
                    skipped.Add($"'{file.FileName}' exceeds the 25 MB limit.");
                    continue;
                }
                if (!AllowedExtensions.Contains(ext))
                {
                    skipped.Add($"'{file.FileName}': file type {ext} is not allowed.");
                    continue;
                }

                byte[] content;
                await using (var input = file.OpenReadStream())
                using (var buffer = new MemoryStream())
                {
                    await input.CopyToAsync(buffer, ct);
                    content = buffer.ToArray();
                }

                var scan = await _scanner.ScanAsync(content, file.FileName, ct);
                if (!scan.IsClean)
                {
                    _logger.LogWarning("Rejected ticket attachment {File}: {Threat}", file.FileName, scan.Threat);
                    skipped.Add($"'{file.FileName}' was rejected: malware detected ({scan.Threat}).");
                    continue;
                }

                // Content-addressed storage: identical bytes reuse the same file on disk.
                var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
                var storedName = hash + ext;
                var storedPath = Path.Combine(uploadPath, storedName);
                if (!File.Exists(storedPath))
                    await File.WriteAllBytesAsync(storedPath, content, ct);

                _db.TicketAttachments.Add(new TicketAttachment
                {
                    FileName = file.FileName,
                    FilePath = "/uploads/" + storedName,
                    ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                        ? "application/octet-stream" : file.ContentType,
                    UploadedAt = DateTime.Now,
                    TicketId = ticketId,
                    TicketMessageId = ticketMessageId
                });
                saved++;
            }

            if (saved > 0) await _db.SaveChangesAsync(ct);
            return new SaveResult(saved, skipped);
        }
    }
}
