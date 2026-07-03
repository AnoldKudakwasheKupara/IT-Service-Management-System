namespace IT_Service_Management_System.Models.Efm
{
    /// <summary>Lifecycle / approval state of an employee document.</summary>
    public enum DocumentStatus
    {
        Draft = 0,
        PendingApproval = 1,
        Approved = 2,
        Rejected = 3,
        Active = 4,
        Archived = 5,
        Expired = 6
    }

    /// <summary>How sensitive a document is (drives who may view it).</summary>
    public enum ConfidentialityLevel
    {
        Public = 0,
        Internal = 1,
        Confidential = 2,
        Restricted = 3
    }

    /// <summary>Pluggable storage backend a version is physically stored in.</summary>
    public enum StorageProviderType
    {
        LocalDisk = 0,
        NetworkShare = 1,
        AzureBlob = 2,
        AwsS3 = 3
    }

    public enum ApprovalStatus
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2
    }

    /// <summary>Every auditable document action.</summary>
    public enum DocumentAuditAction
    {
        Viewed = 0,
        Previewed = 1,
        Downloaded = 2,
        Uploaded = 3,
        VersionUploaded = 4,
        Edited = 5,
        Deleted = 6,
        Moved = 7,
        Printed = 8,
        Shared = 9,
        Archived = 10,
        Restored = 11,
        Approved = 12,
        Rejected = 13,
        VersionRestored = 14
    }

    public enum DocumentNotificationType
    {
        DocumentExpiring = 0,
        DocumentExpired = 1,
        MissingDocument = 2,
        NewUpload = 3,
        ApprovalNeeded = 4,
        DocumentApproved = 5,
        DocumentRejected = 6,
        StorageAlmostFull = 7
    }

    /// <summary>What happens to a document when its retention period elapses.</summary>
    public enum RetentionAction
    {
        Flag = 0,
        Archive = 1,
        Delete = 2
    }
}
