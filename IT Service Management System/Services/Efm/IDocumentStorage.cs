using IT_Service_Management_System.Models.Efm;

namespace IT_Service_Management_System.Services.Efm
{
    /// <summary>Result of persisting a file to a storage backend.</summary>
    public record StoredFileResult(string StoredKey, long SizeBytes, string Sha256, string ContentType);

    /// <summary>
    /// Pluggable document storage. Files (bytes) are stored here — never in SQL. Implementations
    /// exist for local disk, network share, Azure Blob and S3 so the module scales from a single
    /// server to cloud object storage without touching the rest of the code.
    /// </summary>
    public interface IDocumentStorage
    {
        StorageProviderType ProviderType { get; }

        /// <summary>Streams <paramref name="content"/> to storage and returns its key, size and SHA-256.</summary>
        Task<StoredFileResult> SaveAsync(Stream content, string originalFileName, string contentType,
            CancellationToken ct = default);

        /// <summary>Opens a readable stream for a stored file.</summary>
        Task<Stream> OpenReadAsync(string storedKey, CancellationToken ct = default);

        Task<bool> ExistsAsync(string storedKey, CancellationToken ct = default);

        Task<bool> DeleteAsync(string storedKey, CancellationToken ct = default);
    }
}
