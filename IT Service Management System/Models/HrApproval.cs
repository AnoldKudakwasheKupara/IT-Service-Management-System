namespace IT_Service_Management_System.Models
{
    public class HrApproval
    {
        public int Id { get; set; }

        public int ExitClearanceId { get; set; }

        public bool ResignationLetterReceived { get; set; }

        public bool StaffIdCardReturned { get; set; }

        public bool MedicalAidCancelled { get; set; }

        public bool NSSACancelled { get; set; }

        public bool FuneralPolicyCancelled { get; set; }

        public bool ExitInterviewCompleted { get; set; }

        public bool HandoverReportSubmitted { get; set; }

        public string? Comments { get; set; }

        public int HrUserId { get; set; }

        public bool Approved { get; set; }

        public DateTime? ApprovedDate { get; set; }
    }
}