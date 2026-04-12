namespace IT_Service_Management_System.Models
{
    public class Payment
    {
        public int Id { get; set; }

        public string ServiceName { get; set; }

        public decimal Amount { get; set; }

        public DateTime DueDate { get; set; }

        public DateTime? PaidDate { get; set; }

        public string Status { get; set; }

        public int PaymentScheduleId { get; set; }
        public PaymentSchedule PaymentSchedule { get; set; }
    }
}