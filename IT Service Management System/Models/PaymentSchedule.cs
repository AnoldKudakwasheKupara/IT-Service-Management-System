namespace IT_Service_Management_System.Models
{
    public enum PaymentFrequency
    {
        Monthly,
        Annual
    }

    public class PaymentSchedule
    {
        public int Id { get; set; }

        public string ServiceName { get; set; }

        public decimal Amount { get; set; }

        public DateTime PaymentDate { get; set; }

        public PaymentFrequency Frequency { get; set; }

        public string Departments { get; set; }

        public DateTime NextRunDate { get; set; }

        public bool IsActive { get; set; }
    }
}