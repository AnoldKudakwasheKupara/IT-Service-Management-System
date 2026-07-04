using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models.Efm;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Services.Efm
{
    /// <summary>Metadata supplied alongside an uploaded file.</summary>
    public class DocumentUploadInput
    {
        public int EmployeeId { get; set; }
        public int FolderId { get; set; }
        public int CategoryId { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? DocumentNumber { get; set; }
        public DateTime? IssueDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public ConfidentialityLevel Confidentiality { get; set; } = ConfidentialityLevel.Confidential;
        public string? TagsCsv { get; set; }
        public string? Keywords { get; set; }
    }

    /// <summary>
    /// Core business logic for employee documents: create documents + versions through the
    /// pluggable storage layer, stream files back, and write the document audit trail.
    /// </summary>
    public class DocumentService
    {
        private readonly ApplicationDbContext _db;
        private readonly IDocumentStorage _storage;
        private readonly IHttpContextAccessor _http;
        private readonly ILogger<DocumentService> _logger;

        public DocumentService(ApplicationDbContext db, IDocumentStorage storage,
            IHttpContextAccessor http, ILogger<DocumentService> logger)
        {
            _db = db;
            _storage = storage;
            _http = http;
            _logger = logger;
        }

        /// <summary>Creates a new document and its first version from an uploaded file.</summary>
        public async Task<EmployeeDocument> CreateFromUploadAsync(
            DocumentUploadInput input, IFormFile file, int? userId, string? userName, CancellationToken ct = default)
        {
            StoredFileResult stored;
            await using (var read = file.OpenReadStream())
                stored = await _storage.SaveAsync(read, file.FileName, file.ContentType, ct);

            var title = string.IsNullOrWhiteSpace(input.Title)
                ? Path.GetFileNameWithoutExtension(file.FileName)
                : input.Title.Trim();

            var doc = new EmployeeDocument
            {
                EmployeeId = input.EmployeeId,
                FolderId = input.FolderId,
                CategoryId = input.CategoryId,
                Title = title.Length > 200 ? title[..200] : title,
                Description = input.Description,
                DocumentNumber = input.DocumentNumber,
                IssueDate = input.IssueDate,
                ExpiryDate = input.ExpiryDate,
                ConfidentialityLevel = input.Confidentiality,
                Keywords = input.Keywords,
                Status = DocumentStatus.Active,
                CreatedAt = DateTime.Now,
                CreatedById = userId,
                CreatedByName = userName
            };
            _db.EmployeeDocuments.Add(doc);
            await _db.SaveChangesAsync(ct);

            var version = new DocumentVersion
            {
                EmployeeDocumentId = doc.Id,
                VersionNumber = 1,
                FileName = Path.GetFileName(file.FileName),
                StoredKey = stored.StoredKey,
                StorageProvider = _storage.ProviderType,
                ContentType = stored.ContentType,
                FileSizeBytes = stored.SizeBytes,
                Sha256 = stored.Sha256,
                UploadedAt = DateTime.Now,
                UploadedById = userId,
                UploadedByName = userName,
                IsCurrent = true
            };
            _db.DocumentVersions.Add(version);
            await _db.SaveChangesAsync(ct);

            doc.CurrentVersionId = version.Id;
            await ApplyTagsAsync(doc, input.TagsCsv, ct);
            await _db.SaveChangesAsync(ct);

            await LogAsync(DocumentAuditAction.Uploaded, doc.Id, doc.EmployeeId,
                $"Uploaded '{doc.Title}' ({version.FileName}, {stored.SizeBytes} bytes)");

            return doc;
        }

        /// <summary>Loads a document's current version and opens a readable stream for it.</summary>
        public async Task<(DocumentVersion Version, Stream Stream)?> OpenCurrentAsync(int documentId, CancellationToken ct = default)
        {
            var doc = await _db.EmployeeDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == documentId, ct);
            if (doc?.CurrentVersionId == null) return null;

            var version = await _db.DocumentVersions.AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == doc.CurrentVersionId, ct);
            if (version == null) return null;

            if (!await _storage.ExistsAsync(version.StoredKey, ct)) return null;
            var stream = await _storage.OpenReadAsync(version.StoredKey, ct);
            return (version, stream);
        }

        /// <summary>Opens a readable stream for a specific version (used by version history/download).</summary>
        public async Task<(DocumentVersion Version, Stream Stream)?> OpenVersionAsync(int versionId, CancellationToken ct = default)
        {
            var version = await _db.DocumentVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == versionId, ct);
            if (version == null || !await _storage.ExistsAsync(version.StoredKey, ct)) return null;
            var stream = await _storage.OpenReadAsync(version.StoredKey, ct);
            return (version, stream);
        }

        /// <summary>Writes a document audit-trail entry, capturing IP + user agent.</summary>
        public async Task LogAsync(DocumentAuditAction action, int? documentId, int? employeeId, string? details, CancellationToken ct = default)
        {
            var http = _http.HttpContext;
            var ip = http?.Connection.RemoteIpAddress?.ToString();
            if (ip == "::1") ip = "127.0.0.1";

            _db.DocumentAuditLogs.Add(new DocumentAuditLog
            {
                EmployeeDocumentId = documentId,
                EmployeeId = employeeId,
                Action = action,
                PerformedById = http?.Session.GetInt32("UserId"),
                PerformedByName = http?.Session.GetString("UserName"),
                IpAddress = ip,
                UserAgent = http?.Request.Headers.UserAgent.ToString(),
                Timestamp = DateTime.Now,
                Details = details
            });
            await _db.SaveChangesAsync(ct);
        }

        private async Task ApplyTagsAsync(EmployeeDocument doc, string? tagsCsv, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(tagsCsv)) return;

            var names = tagsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => t.Length > 60 ? t[..60] : t)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var name in names)
            {
                var tag = await _db.DocumentTags.FirstOrDefaultAsync(t => t.Name == name, ct)
                          ?? _db.DocumentTags.Add(new DocumentTag { Name = name }).Entity;
                if (tag.Id == 0) await _db.SaveChangesAsync(ct);
                _db.DocumentTagMaps.Add(new DocumentTagMap { EmployeeDocumentId = doc.Id, DocumentTagId = tag.Id });
            }
        }
    }
}
