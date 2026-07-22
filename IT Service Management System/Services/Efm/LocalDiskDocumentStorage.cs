using System.Security.Cryptography;
using IT_Service_Management_System.Models.Efm;

namespace IT_Service_Management_System.Services.Efm
{
    /// <summary>
    /// Stores document bytes on the local disk (or a mounted network share) OUTSIDE wwwroot, so
    /// files are never publicly reachable. Keys are sharded two levels deep (ab/cd/…) to keep any
    /// single directory small even with millions of files.
    /// </summary>
    public class LocalDiskDocumentStorage : IDocumentStorage
    {
        private readonly string _root;
        private readonly ILogger<LocalDiskDocumentStorage> _logger;

        public StorageProviderType ProviderType => StorageProviderType.LocalDisk;

        public LocalDiskDocumentStorage(IWebHostEnvironment env, IConfiguration config,
            ILogger<LocalDiskDocumentStorage> logger)
        {
            _root = config["EFM:LocalRoot"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(_root))
                _root = Path.Combine(env.ContentRootPath, "employee-documents");
            Directory.CreateDirectory(_root);
            _logger = logger;
        }

        public async Task<StoredFileResult> SaveAsync(Stream content, string originalFileName, string contentType,
            CancellationToken ct = default)
        {
            var ext = Path.GetExtension(originalFileName);
            if (ext.Length > 20) ext = "";               // guard against absurd extensions
            var id = Guid.NewGuid().ToString("N");
            // Shard: ab/cd/<guid><ext>
            var key = $"{id[..2]}/{id.Substring(2, 2)}/{id}{ext}";
            var fullPath = Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            long size;
            string hash;
            using (var sha = SHA256.Create())
            using (var fileStream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       bufferSize: 81920, useAsync: true))
            using (var crypto = new CryptoStream(fileStream, sha, CryptoStreamMode.Write))
            {
                await content.CopyToAsync(crypto, ct);
                await crypto.FlushFinalBlockAsync(ct);
                size = fileStream.Length;
                hash = Convert.ToHexString(sha.Hash!).ToLowerInvariant();
            }

            _logger.LogInformation("Stored document {Key} ({Size} bytes) on local disk", key, size);
            return new StoredFileResult(key, size,
                hash, string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        }

        public Task<Stream> OpenReadAsync(string storedKey, CancellationToken ct = default)
        {
            var fullPath = Resolve(storedKey);
            Stream s = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 81920, useAsync: true);
            return Task.FromResult(s);
        }

        public Task<bool> ExistsAsync(string storedKey, CancellationToken ct = default)
            => Task.FromResult(File.Exists(Resolve(storedKey)));

        public Task<bool> DeleteAsync(string storedKey, CancellationToken ct = default)
        {
            try
            {
                var fullPath = Resolve(storedKey);
                if (File.Exists(fullPath)) File.Delete(fullPath);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete stored document {Key}", storedKey);
                return Task.FromResult(false);
            }
        }

        // Resolves a stored key to an absolute path, guarding against path traversal.
        private string Resolve(string storedKey)
        {
            var full = Path.GetFullPath(Path.Combine(_root, storedKey.Replace('/', Path.DirectorySeparatorChar)));
            if (!full.StartsWith(Path.GetFullPath(_root), StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Invalid storage key.");
            return full;
        }
    }
}
