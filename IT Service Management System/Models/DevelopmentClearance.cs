namespace IT_Service_Management_System.Models
{
    public class DevelopmentClearance
    {
        public int Id { get; set; }

        public int ExitClearanceId { get; set; }

        public string? BitbucketAsset { get; set; }
        public string? BitbucketReceivedBy { get; set; }
        public DateTime? BitbucketDate { get; set; }

        public string? ProjectManagementAsset { get; set; }
        public string? ProjectManagementReceivedBy { get; set; }
        public DateTime? ProjectManagementDate { get; set; }

        public string? ManageEngineAsset { get; set; }
        public string? ManageEngineReceivedBy { get; set; }
        public DateTime? ManageEngineDate { get; set; }

        public string? Comments { get; set; }

        public int? ClearedById { get; set; }

        public DateTime? ClearedDate { get; set; }
    }
}