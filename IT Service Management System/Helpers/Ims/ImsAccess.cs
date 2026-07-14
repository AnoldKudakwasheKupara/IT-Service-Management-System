namespace IT_Service_Management_System.Helpers.Ims
{
    /// <summary>
    /// Granular capabilities across the Integrated Management System (ISO 9001:2015 &amp; ISO/IEC 27001:2022) module.
    /// Every IMS controller action is gated first by the session-role check (<c>RoleAuthorize</c>) and then,
    /// where finer control is required, by <see cref="ImsAccess.Can"/>. Record-level checks (ownership,
    /// department, classification) are layered on top of these role capabilities by the individual modules.
    /// </summary>
    public enum ImsPermission
    {
        // ── Document control &amp; lifecycle ──
        ViewDocuments,
        CreateDocument,
        EditDocument,
        DeleteDocument,
        SubmitForReview,
        DepartmentReview,
        QualityReview,
        ManagementApprove,
        PublishDocument,
        ReviseDocument,
        RestoreVersion,
        ArchiveDocument,
        ManageDistribution,
        AcknowledgeDocument,

        // ── Internal audits &amp; findings ──
        ViewAudits,
        ManageAuditProgramme,
        ConductAudit,
        RaiseFinding,

        // ── CAPA &amp; non-conformance ──
        ViewCapa,
        RaiseCapa,
        AssignCapa,
        InvestigateCapa,
        VerifyCapa,
        CloseCapa,
        ViewNonConformance,
        RaiseNonConformance,

        // ── Incident management ──
        ViewIncidents,
        ManageIncidents,

        // ── Risk &amp; opportunity ──
        ViewRisk,
        ManageRisk,

        // ── Supplier management ──
        ViewSuppliers,
        ManageSuppliers,

        // ── Training &amp; competency ──
        ViewTraining,
        ManageTraining,

        // ── Management review, objectives, improvement, compliance ──
        ViewManagementReview,
        ManageManagementReview,
        ViewObjectives,
        ManageObjectives,
        ViewImprovements,
        ManageImprovements,
        ViewCompliance,
        ManageCompliance,

        // ── Cross-cutting ──
        ViewReports,
        ManageEvidence,
        ViewAuditTrail,
        ManageConfiguration
    }

    /// <summary>
    /// Central permission map for the IMS module. Maps the application's session roles onto the seven ISO
    /// role tiers from the specification:
    ///   Admin / SystemsAdmin → System Administrator (full control, including configuration)
    ///   QualityManager       → Quality Manager (runs the whole ISO system except system configuration)
    ///   DocumentController    → owns the document lifecycle; read-only elsewhere
    ///   DepartmentManager     → department-level review &amp; sign-off; raises issues in their area
    ///   Auditor               → plans/conducts audits, raises findings, NCs and CAPAs
    ///   ExternalAuditor       → read-only across the entire module
    ///   Employee              → self-service (read published material, acknowledge, view own training)
    /// </summary>
    public static class ImsAccess
    {
        // ── Session-role predicates (strings mirror Helpers.Roles constants) ──
        public static bool IsAdministrator(string? role) => role is "Admin" or "SystemsAdmin";
        public static bool IsQualityManager(string? role) => role == "QualityManager";
        public static bool IsDocumentController(string? role) => role == "DocumentController";
        public static bool IsDepartmentManager(string? role) => role == "DepartmentManager";
        public static bool IsAuditor(string? role) => role == "Auditor";
        public static bool IsExternalAuditor(string? role) => role == "ExternalAuditor";
        public static bool IsEmployee(string? role) => role == "Employee";

        /// <summary>Administrators and the Quality Manager run the management system.</summary>
        public static bool IsImsManager(string? role) => IsAdministrator(role) || IsQualityManager(role);

        /// <summary>Every role permitted to open the IMS / ISO module at all.</summary>
        public static bool CanAccessModule(string? role) =>
            IsImsManager(role) || IsDocumentController(role) || IsDepartmentManager(role)
            || IsAuditor(role) || IsExternalAuditor(role) || IsEmployee(role);

        /// <summary>
        /// The read-only capability set — everything a view-only role (external auditor) may do, and the
        /// floor that most contributing roles also inherit for modules outside their remit.
        /// </summary>
        private static readonly HashSet<ImsPermission> ReadOnlySet = new()
        {
            ImsPermission.ViewDocuments, ImsPermission.ViewAudits, ImsPermission.ViewCapa,
            ImsPermission.ViewNonConformance, ImsPermission.ViewRisk, ImsPermission.ViewSuppliers,
            ImsPermission.ViewTraining, ImsPermission.ViewManagementReview, ImsPermission.ViewObjectives,
            ImsPermission.ViewImprovements, ImsPermission.ViewCompliance, ImsPermission.ViewReports,
            ImsPermission.ViewAuditTrail, ImsPermission.ViewIncidents
        };

        /// <summary>
        /// Does the role hold a capability, before record-level ownership / department / classification checks?
        /// </summary>
        public static bool Can(string? role, ImsPermission p)
        {
            // System Administrator — full control.
            if (IsAdministrator(role)) return true;

            // Quality Manager — runs the ISO system end to end, except system configuration.
            if (IsQualityManager(role))
                return p != ImsPermission.ManageConfiguration;

            // External Auditor — may view everything, change nothing.
            if (IsExternalAuditor(role))
                return ReadOnlySet.Contains(p);

            // Document Controller — owns the document lifecycle; read-only elsewhere.
            if (IsDocumentController(role))
                return p is ImsPermission.CreateDocument or ImsPermission.EditDocument
                    or ImsPermission.SubmitForReview or ImsPermission.PublishDocument
                    or ImsPermission.ReviseDocument or ImsPermission.RestoreVersion
                    or ImsPermission.ArchiveDocument or ImsPermission.ManageDistribution
                    or ImsPermission.ManageEvidence or ImsPermission.AcknowledgeDocument
                    || ReadOnlySet.Contains(p);

            // Department Manager — department-level review &amp; sign-off; raises issues in their area.
            if (IsDepartmentManager(role))
                return p is ImsPermission.DepartmentReview or ImsPermission.AcknowledgeDocument
                    or ImsPermission.RaiseNonConformance or ImsPermission.RaiseFinding
                    or ImsPermission.RaiseCapa or ImsPermission.AssignCapa
                    or ImsPermission.InvestigateCapa or ImsPermission.ManageObjectives
                    or ImsPermission.ManageImprovements or ImsPermission.ManageIncidents
                    || ReadOnlySet.Contains(p);

            // Auditor — plans/conducts audits, raises findings, NCs and CAPAs, verifies effectiveness.
            if (IsAuditor(role))
                return p is ImsPermission.ConductAudit or ImsPermission.ManageAuditProgramme
                    or ImsPermission.RaiseFinding or ImsPermission.RaiseNonConformance
                    or ImsPermission.RaiseCapa or ImsPermission.VerifyCapa
                    or ImsPermission.AcknowledgeDocument or ImsPermission.ManageIncidents
                    || ReadOnlySet.Contains(p);

            // Employee — self-service: read published material, acknowledge policies, view own training.
            if (IsEmployee(role))
                return p is ImsPermission.ViewDocuments or ImsPermission.AcknowledgeDocument
                    or ImsPermission.ViewTraining;

            return false;
        }
    }
}
