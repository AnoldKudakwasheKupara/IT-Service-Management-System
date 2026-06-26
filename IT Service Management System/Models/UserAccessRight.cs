namespace IT_Service_Management_System.Models
{
    public class UserAccessRight
    {
        public int Id { get; set; }

        public string? SolutionSoftware { get; set; }

        public string? ProductionServerId { get; set; }

        public string? ClientName { get; set; }

        public string? VersionReleaseNo { get; set; }

        public string? SystemsAdmin { get; set; }

        public DateTime Date { get; set; }

        public ICollection<UserAccessRightItem> Users { get; set; }

        public string? AssignedBy { get; set; }

        public string? AssignedSignature { get; set; }

        public DateTime? AssignedDate { get; set; }

        public string? ReviewedBy { get; set; }

        public string? ReviewedSignature { get; set; }

        public DateTime? ReviewedDate { get; set; }

        public string? ApprovedBy { get; set; }

        public string? ApprovedSignature { get; set; }

        public DateTime? ApprovedDate { get; set; }
    }
}