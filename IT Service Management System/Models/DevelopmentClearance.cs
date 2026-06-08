namespace IT_Service_Management_System.Models
{
    public class DevelopmentClearance
    {
        public int Id { get; set; }

        public int ExitClearanceId { get; set; }

        public bool BitbucketRemoved { get; set; }

        public bool ProjectManagementRemoved { get; set; }

        public bool ManageEngineRemoved { get; set; }

        public string? Comments { get; set; }

        public int? ClearedById { get; set; }

        public DateTime? ClearedDate { get; set; }
    }
}