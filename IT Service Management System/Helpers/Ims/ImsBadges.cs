using IT_Service_Management_System.Models.Ims;

namespace IT_Service_Management_System.Helpers.Ims
{
    /// <summary>Maps IMS enums to the badge CSS classes / friendly labels defined in _ImsStyles.cshtml.</summary>
    public static class ImsBadges
    {
        public static string DocStatusClass(DocumentStatus s) => s switch
        {
            DocumentStatus.Draft => "b-draft",
            DocumentStatus.DepartmentReview => "b-deptreview",
            DocumentStatus.QualityReview => "b-qualreview",
            DocumentStatus.ManagementApproval => "b-approval",
            DocumentStatus.Published => "b-published",
            DocumentStatus.UnderReview => "b-review",
            DocumentStatus.Revision => "b-revision",
            DocumentStatus.Archived => "b-archived",
            DocumentStatus.Obsolete => "b-obsolete",
            DocumentStatus.Rejected => "b-rejected",
            _ => "b-draft"
        };

        public static string DocStatusLabel(DocumentStatus s) => s switch
        {
            DocumentStatus.DepartmentReview => "Department Review",
            DocumentStatus.QualityReview => "Quality Review",
            DocumentStatus.ManagementApproval => "Management Approval",
            DocumentStatus.UnderReview => "Under Review",
            _ => s.ToString()
        };

        public static string ClassificationClass(DocumentClassification c) => c switch
        {
            DocumentClassification.Public => "b-public",
            DocumentClassification.Internal => "b-internal",
            DocumentClassification.Confidential => "b-confidential",
            _ => "b-restricted"
        };

        public static string CapaStatusClass(CapaStatus s) => s switch
        {
            CapaStatus.Closed or CapaStatus.Verified => "b-closed",
            CapaStatus.Escalated => "b-critical",
            CapaStatus.Open => "b-open",
            _ => "b-medium"
        };

        /// <summary>Generic Low/Medium/High/Critical style scale for severities and priorities.</summary>
        public static string ScaleClass(string value) => value.ToLowerInvariant() switch
        {
            "low" or "minor" or "observation" or "conformity" => "b-low",
            "medium" or "moderate" => "b-medium",
            "high" or "major" => "b-high",
            "critical" or "majornonconformance" => "b-critical",
            _ => "b-medium"
        };
    }
}
