namespace IT_Service_Management_System.Models
{
    public class PaymentSchedule
    {
        public int Id { get; set; }

        public string ServiceName { get; set; }

        public decimal Amount { get; set; }

        public int? DayOfMonth { get; set; }

        public DateTime? FixedDate { get; set; }

        public PaymentFrequency Frequency { get; set; }

        public string Departments { get; set; }

        public DateTime NextRunDate { get; set; }

        public bool IsActive { get; set; }
    }
}

public enum PaymentFrequency
{
    Monthly,
    Annual
}