namespace IT_Service_Management_System.Models
{
    public class StockHandoverItem
    {
        public int Id { get; set; }

        public int ClearanceRequestId { get; set; }

        public string ItemDescription { get; set; }

        public string Condition { get; set; }

        public int Quantity { get; set; }

        public string Remarks { get; set; }
    }
}