using IT_Service_Management_System.Models.Efm;

namespace IT_Service_Management_System.Services.Efm
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Cloud storage providers. The interface is identical to LocalDisk, so switching
    // to object storage for large-scale deployments is a config change, not a rewrite.
    // These ship as architecture stubs: implement the four methods with the provider
    // SDK (Azure.Storage.Blobs / AWSSDK.S3) and set EFM:StorageProvider to activate.
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Azure Blob Storage provider (stub — add Azure.Storage.Blobs to implement).</summary>
    public class AzureBlobDocumentStorage : IDocumentStorage
    {
        private const string NotConfigured =
            "Azure Blob storage is not implemented. Add the Azure.Storage.Blobs package, wire up " +
            "EFM:Azure:ConnectionString + EFM:Azure:Container, and implement the four IDocumentStorage methods.";

        public StorageProviderType ProviderType => StorageProviderType.AzureBlob;

        public Task<StoredFileResult> SaveAsync(Stream content, string originalFileName, string contentType, CancellationToken ct = default)
            => throw new NotSupportedException(NotConfigured);
        public Task<Stream> OpenReadAsync(string storedKey, CancellationToken ct = default)
            => throw new NotSupportedException(NotConfigured);
        public Task<bool> ExistsAsync(string storedKey, CancellationToken ct = default)
            => throw new NotSupportedException(NotConfigured);
        public Task<bool> DeleteAsync(string storedKey, CancellationToken ct = default)
            => throw new NotSupportedException(NotConfigured);
    }

    /// <summary>AWS S3 provider (stub — add AWSSDK.S3 to implement).</summary>
    public class AwsS3DocumentStorage : IDocumentStorage
    {
        private const string NotConfigured =
            "AWS S3 storage is not implemented. Add the AWSSDK.S3 package, wire up EFM:S3:Bucket + " +
            "credentials, and implement the four IDocumentStorage methods.";

        public StorageProviderType ProviderType => StorageProviderType.AwsS3;

        public Task<StoredFileResult> SaveAsync(Stream content, string originalFileName, string contentType, CancellationToken ct = default)
            => throw new NotSupportedException(NotConfigured);
        public Task<Stream> OpenReadAsync(string storedKey, CancellationToken ct = default)
            => throw new NotSupportedException(NotConfigured);
        public Task<bool> ExistsAsync(string storedKey, CancellationToken ct = default)
            => throw new NotSupportedException(NotConfigured);
        public Task<bool> DeleteAsync(string storedKey, CancellationToken ct = default)
            => throw new NotSupportedException(NotConfigured);
    }
}
