using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Ims
{
    // ── Supplier management enumerations (ISO 9001 cl. 8.4) ──────────────────────
    public enum SupplierCategory { Goods, Services, Both }
    public enum SupplierStatus { Prospective, Approved, Conditional, Suspended, Deactivated }
    public enum EvaluationPeriod { Monthly, Quarterly, SemiAnnual, Annual, AdHoc }

    public class Supplier
    {
        public int Id { get; set; }

        [NotMapped] public string Reference => $"SUP-{Id:D5}";

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        public SupplierCategory Category { get; set; } = SupplierCategory.Goods;
        public SupplierStatus Status { get; set; } = SupplierStatus.Prospective;

        [StringLength(150)]
        public string? ContactName { get; set; }
        [StringLength(150), EmailAddress]
        public string? Email { get; set; }
        [StringLength(40), Phone]
        public string? Phone { get; set; }
        [StringLength(300)]
        public string? Address { get; set; }

        [StringLength(1000), Display(Name = "Products / Services")]
        public string? ProductsServices { get; set; }

        [Display(Name = "Approved Date"), DataType(DataType.Date)]
        public DateTime? ApprovedDate { get; set; }

        [Display(Name = "Contract Start"), DataType(DataType.Date)]
        public DateTime? ContractStart { get; set; }
        [Display(Name = "Contract End"), DataType(DataType.Date)]
        public DateTime? ContractEnd { get; set; }

        [StringLength(200), Display(Name = "Certificate")]
        public string? CertificateName { get; set; }
        [Display(Name = "Certificate Expiry"), DataType(DataType.Date)]
        public DateTime? CertificateExpiry { get; set; }

        [StringLength(2000)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? CreatedById { get; set; }
        [ValidateNever] public User? CreatedBy { get; set; }

        [ValidateNever] public ICollection<SupplierEvaluation> Evaluations { get; set; } = new List<SupplierEvaluation>();

        [NotMapped] public bool CertificateExpiringSoon =>
            CertificateExpiry.HasValue && CertificateExpiry.Value.Date <= DateTime.Now.Date.AddDays(30);
    }

    /// <summary>A periodic supplier evaluation scored 0–100 on five criteria; the overall drives the rating band.</summary>
    public class SupplierEvaluation
    {
        public int Id { get; set; }

        public int SupplierId { get; set; }
        [ValidateNever] public Supplier? Supplier { get; set; }

        [Display(Name = "Evaluation Date"), DataType(DataType.Date)]
        public DateTime EvaluationDate { get; set; } = DateTime.Now;

        public EvaluationPeriod Period { get; set; } = EvaluationPeriod.Quarterly;

        [Range(0, 100)] public int QualityScore { get; set; }
        [Range(0, 100)] public int DeliveryScore { get; set; }
        [Range(0, 100)] public int PricingScore { get; set; }
        [Range(0, 100)] public int SupportScore { get; set; }
        [Range(0, 100)] public int ComplianceScore { get; set; }

        [StringLength(2000)]
        public string? Comments { get; set; }

        public int? EvaluatedById { get; set; }
        [ValidateNever] public User? EvaluatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [NotMapped] public int OverallScore =>
            (int)Math.Round((QualityScore + DeliveryScore + PricingScore + SupportScore + ComplianceScore) / 5.0);

        [NotMapped] public string Rating => OverallScore switch
        {
            >= 85 => "Excellent",
            >= 70 => "Good",
            >= 50 => "Acceptable",
            _ => "Poor"
        };
    }
}
