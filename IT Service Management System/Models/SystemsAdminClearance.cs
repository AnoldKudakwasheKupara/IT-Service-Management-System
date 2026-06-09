namespace IT_Service_Management_System.Models
{
    public class SystemsAdminClearance
    {
        public int Id { get; set; }

        public int ExitClearanceId { get; set; }

        public string? LaptopAsset { get; set; }
        public string? LaptopReceivedBy { get; set; }
        public DateTime? LaptopDate { get; set; }

        public string? LaptopBagAsset { get; set; }
        public string? LaptopBagReceivedBy { get; set; }
        public DateTime? LaptopBagDate { get; set; }

        public string? AccessoriesAsset { get; set; }
        public string? AccessoriesReceivedBy { get; set; }
        public DateTime? AccessoriesDate { get; set; }

        public string? LockerAsset { get; set; }
        public string? LockerReceivedBy { get; set; }
        public DateTime? LockerDate { get; set; }

        public string? EndpointSecurityAsset { get; set; }
        public string? EndpointSecurityReceivedBy { get; set; }
        public DateTime? EndpointSecurityDate { get; set; }

        public string? SophosAsset { get; set; }
        public string? SophosReceivedBy { get; set; }
        public DateTime? SophosDate { get; set; }

        public string? AccessRemovalAsset { get; set; }
        public string? AccessRemovalReceivedBy { get; set; }
        public DateTime? AccessRemovalDate { get; set; }

        public string? EmailAsset { get; set; }
        public string? EmailReceivedBy { get; set; }
        public DateTime? EmailDate { get; set; }

        public string? SocialMediaAsset { get; set; }
        public string? SocialMediaReceivedBy { get; set; }
        public DateTime? SocialMediaDate { get; set; }

        public string? Comments { get; set; }

        public int? ClearedById { get; set; }

        public DateTime? ClearedDate { get; set; }
    }
}