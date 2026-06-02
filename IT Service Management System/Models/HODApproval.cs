namespace IT_Service_Management_System.Models
{
    public class HODApproval
    {
        public int Id { get; set; }

        public int ClearanceRequestId { get; set; }

        public string HeadOfDepartment { get; set; }

        public string Signature { get; set; }

        public DateTime ApprovalDate { get; set; }

        public bool Approved { get; set; }
    }
}