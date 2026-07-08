namespace IT_Service_Management_System.Models.Ims
{
    // ── Cross-module IMS / ISO enumerations ─────────────────────────────────────
    // Module-specific enums live alongside their module (e.g. document-control enums in
    // IsoDocumentEnums.cs). Only genuinely cross-cutting values belong here.

    /// <summary>The management-system standard a record relates to.</summary>
    public enum IsoStandard
    {
        Iso9001,     // ISO 9001:2015  — Quality Management
        Iso27001,    // ISO/IEC 27001:2022 — Information Security Management
        Both,        // Applies to the integrated management system as a whole
        Other        // Another standard / regulatory framework
    }

    /// <summary>Friendly labels for <see cref="IsoStandard"/> (enum names are terse for storage).</summary>
    public static class IsoStandards
    {
        public static string Label(IsoStandard standard) => standard switch
        {
            IsoStandard.Iso9001 => "ISO 9001:2015",
            IsoStandard.Iso27001 => "ISO/IEC 27001:2022",
            IsoStandard.Both => "ISO 9001 & 27001 (IMS)",
            _ => "Other"
        };
    }
}
