namespace IT_Service_Management_System.Models.Ims
{
    // ── Document Control enumerations (ISO 9001 cl. 7.5 / ISO 27001 cl. 7.5) ─────

    /// <summary>The kind of controlled document — drives the Policies / Procedures / Work Instructions / Forms / Records views.</summary>
    public enum DocumentType
    {
        Policy,
        Procedure,
        WorkInstruction,
        Form,
        Record,
        Manual,
        Plan,
        Register,
        Guideline,
        Other
    }

    /// <summary>Lifecycle state. Mirrors the approval workflow:
    /// Draft → DepartmentReview → QualityReview → ManagementApproval → Published → (UnderReview → Revision) → Archived / Obsolete.</summary>
    public enum DocumentStatus
    {
        Draft,
        DepartmentReview,
        QualityReview,
        ManagementApproval,
        Published,
        UnderReview,
        Revision,
        Archived,
        Obsolete,
        Rejected
    }

    /// <summary>Information-security classification of the document.</summary>
    public enum DocumentClassification
    {
        Public,
        Internal,
        Confidential,
        Restricted
    }

    /// <summary>How often the document must be reviewed; used to compute the next review date.</summary>
    public enum ReviewFrequency
    {
        None,
        Monthly,
        Quarterly,
        SemiAnnual,
        Annual,
        Biennial,
        Triennial
    }

    /// <summary>A stage in the document approval workflow.</summary>
    public enum ApprovalStage
    {
        DepartmentReview,
        QualityReview,
        ManagementApproval
    }

    /// <summary>The outcome recorded by an approver at a workflow stage.</summary>
    public enum ApprovalDecision
    {
        Pending,
        Approved,
        Rejected,
        ReturnedForChanges
    }

    /// <summary>Progress of an individual employee's acknowledgement of a published document.</summary>
    public enum AcknowledgementStatus
    {
        Pending,
        Opened,
        Acknowledged
    }

    /// <summary>Result of a scheduled document review.</summary>
    public enum ReviewOutcome
    {
        Pending,
        NoChangeRequired,
        MinorRevision,
        MajorRevision,
        Withdrawn
    }

    /// <summary>Who a distribution-list entry targets (expanded into acknowledgements when the document is published).</summary>
    public enum DistributionTargetType
    {
        User,
        Department,
        Role,
        AllStaff
    }
}
