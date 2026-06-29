namespace IT_Service_Management_System.Models
{
    public class SSLCertificate
    {
        public int Id { get; set; }

        public string SystemName { get; set; } = string.Empty;
        public string URL { get; set; } = string.Empty;

        public DateTime ExpiryDate { get; set; }

        public bool IsRenewed { get; set; } = false;

        public DateTime? LastRenewedDate { get; set; }

        public int DaysRemaining => (ExpiryDate - DateTime.Now).Days;
    }
}