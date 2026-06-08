namespace IT_Service_Management_System.Models
{
    public class SystemsAdminClearance
    {
        public int Id { get; set; }

        public int ExitClearanceId { get; set; }

        public bool LaptopReturned { get; set; }

        public bool LaptopBagReturned { get; set; }

        public bool AccessoriesReturned { get; set; }

        public bool LockerReturned { get; set; }

        public bool EndpointSecurityRemoved { get; set; }

        public bool SophosCredentialsRemoved { get; set; }

        public bool AccessRemoved { get; set; }

        public bool EmailDisabled { get; set; }

        public bool EmailRedirected { get; set; }

        public bool SocialMediaRemoved { get; set; }

        public string? Comments { get; set; }

        public int? ClearedById { get; set; }

        public DateTime? ClearedDate { get; set; }
    }
}