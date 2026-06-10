namespace IT_Service_Management_System.Models
{
    public class HrApproval
    {
        public int Id { get; set; }

        public int ExitClearanceId { get; set; }

        public bool ResignationLetterReceived { get; set; }
        public string? ResignationLetterReceivedBy { get; set; }
        public DateTime? ResignationLetterDate { get; set; }

        public bool StaffIdReturned { get; set; }
        public string? StaffIdReceivedBy { get; set; }
        public DateTime? StaffIdDate { get; set; }

        public bool MedicalAidCancelled { get; set; }
        public string? MedicalAidReceivedBy { get; set; }
        public DateTime? MedicalAidDate { get; set; }

        public bool NSSACancelled { get; set; }
        public string? NSSAReceivedBy { get; set; }
        public DateTime? NSSADate { get; set; }

        public bool FuneralPolicyCancelled { get; set; }
        public string? FuneralPolicyReceivedBy { get; set; }
        public DateTime? FuneralPolicyDate { get; set; }

        public bool ExitInterviewCompleted { get; set; }
        public string? ExitInterviewReceivedBy { get; set; }
        public DateTime? ExitInterviewDate { get; set; }

        public bool HandoverReportSubmitted { get; set; }
        public string? HandoverReportReceivedBy { get; set; }
        public DateTime? HandoverReportDate { get; set; }

        public string? Comments { get; set; }

        public int? ApprovedById { get; set; }

        public DateTime? ApprovedDate { get; set; }
    }
}