using IT_Service_Management_System.Services.Security;

namespace IT_Service_Management_System.Services.Pm
{
    /// <summary>Where an accepted upload ended up, and what it was called before storage renamed it.</summary>
    public record StoredFile(string OriginalName, string RelativePath, string? ContentType, long Size);

    /// <summary>
    /// Handles every file upload in the project module: validates the extension and size, scans the
    /// bytes for malware, then stores under a generated name so a hostile filename can never escape
    /// the upload directory or be replayed as a script.
    /// </summary>
    public class PmFileService
    {
        private readonly IWebHostEnvironment _env;
        private readonly IMalwareScanner _scanner;
        private readonly ILogger<PmFileService> _log;

        /// <summary>Upload ceiling per file (25 MB) — matches the request size limit on the actions.</summary>
        public const long MaxBytes = 25 * 1024 * 1024;

        /// <summary>
        /// Extensions accepted anywhere in the module. Deliberately an allowlist: anything
        /// executable, scriptable or archive-based is rejected outright.
        /// </summary>
        private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".csv", ".rtf", ".odt",
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".svg",
            ".mp4", ".mov", ".webm", ".mp3", ".wav",
            ".dwg", ".dxf", ".msg", ".eml"
        };

        public PmFileService(IWebHostEnvironment env, IMalwareScanner scanner, ILogger<PmFileService> log)
        {
            _env = env; _scanner = scanner; _log = log;
        }

        /// <summary>Human-readable reason the last call rejected a file, for surfacing to the user.</summary>
        public string? LastError { get; private set; }

        /// <summary>
        /// Validate, scan and store an upload under <c>wwwroot/uploads/pm/{area}/{ownerId}</c>.
        /// Returns null (with <see cref="LastError"/> set) when the file is missing or rejected.
        /// </summary>
        public async Task<StoredFile?> SaveAsync(IFormFile? file, string area, int ownerId, CancellationToken ct = default)
        {
            LastError = null;

            if (file == null || file.Length == 0)
            {
                LastError = "No file was selected.";
                return null;
            }

            if (file.Length > MaxBytes)
            {
                LastError = $"The file is larger than the {MaxBytes / 1024 / 1024} MB limit.";
                return null;
            }

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrEmpty(extension) || !Allowed.Contains(extension))
            {
                LastError = $"“{extension}” files are not accepted. Allowed types: documents, images, video, audio and CAD drawings.";
                return null;
            }

            // Read once, scan, then write — never trust bytes that have not been checked.
            byte[] content;
            await using (var stream = file.OpenReadStream())
            await using (var buffer = new MemoryStream())
            {
                await stream.CopyToAsync(buffer, ct);
                content = buffer.ToArray();
            }

            var scan = await _scanner.ScanAsync(content, file.FileName, ct);
            if (!scan.IsClean)
            {
                _log.LogWarning("Rejected project upload {FileName}: {Threat}", file.FileName, scan.Threat);
                LastError = $"The file was rejected by the malware scanner ({scan.Threat}).";
                return null;
            }

            // Sanitise the area segment too — it is code-supplied today, but this keeps it safe if
            // that ever changes.
            var safeArea = string.Concat(area.Where(char.IsLetterOrDigit));
            if (safeArea.Length == 0) safeArea = "misc";

            var folder = Path.Combine(_env.WebRootPath, "uploads", "pm", safeArea, ownerId.ToString());
            Directory.CreateDirectory(folder);

            // The stored name is generated, so the original filename never reaches the filesystem.
            var storedName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            var fullPath = Path.Combine(folder, storedName);
            await File.WriteAllBytesAsync(fullPath, content, ct);

            var relative = $"/uploads/pm/{safeArea}/{ownerId}/{storedName}";
            return new StoredFile(SafeDisplayName(file.FileName), relative, file.ContentType, content.Length);
        }

        /// <summary>Remove a stored file. Silent when it is already gone — deletion is idempotent.</summary>
        public void Delete(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return;
            try
            {
                // Only ever delete inside the module's own upload tree.
                if (!relativePath.StartsWith("/uploads/pm/", StringComparison.OrdinalIgnoreCase)) return;

                var full = Path.Combine(_env.WebRootPath, relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                var root = Path.Combine(_env.WebRootPath, "uploads", "pm");
                if (!Path.GetFullPath(full).StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase)) return;

                if (File.Exists(full)) File.Delete(full);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Could not delete project file {Path}", relativePath);
            }
        }

        /// <summary>Strip any path information and control characters from a user-supplied filename.</summary>
        private static string SafeDisplayName(string fileName)
        {
            var name = Path.GetFileName(fileName);
            var cleaned = new string(name.Where(c => !char.IsControl(c) && !Path.GetInvalidFileNameChars().Contains(c)).ToArray());
            if (string.IsNullOrWhiteSpace(cleaned)) cleaned = "upload";
            return cleaned.Length > 250 ? cleaned[..250] : cleaned;
        }
    }
}
