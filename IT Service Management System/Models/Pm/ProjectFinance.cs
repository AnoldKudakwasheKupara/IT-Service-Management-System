using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Pm
{
    /// <summary>
    /// One line of a project's cost breakdown — the planned figure per category, against which
    /// actual spend (expenses, invoices and costed time) is tracked.
    /// </summary>
    public class BudgetLine
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        [ValidateNever] public Project? Project { get; set; }

        public int? PhaseId { get; set; }
        [ValidateNever] public ProjectPhase? Phase { get; set; }

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        public CostCategory Category { get; set; } = CostCategory.Other;

        [Column(TypeName = "decimal(18,2)")]
        public decimal PlannedAmount { get; set; }

        /// <summary>Spend booked directly against this line (over and above linked expenses).</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal ActualAmount { get; set; }

        /// <summary>Latest estimate of the final outturn for this line.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal ForecastAmount { get; set; }

        /// <summary>Committed but not yet invoiced — approved purchase orders.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal CommittedAmount { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>Under-spend is positive, over-spend negative.</summary>
        [NotMapped]
        public decimal Variance => PlannedAmount - ActualAmount;

        [NotMapped]
        public int UtilisationPercent =>
            PlannedAmount <= 0 ? 0 : (int)Math.Round(ActualAmount / PlannedAmount * 100);
    }

    /// <summary>A cost incurred on a project, routed through an approval workflow before reimbursement.</summary>
    public class ProjectExpense
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        [ValidateNever] public Project? Project { get; set; }

        public int? BudgetLineId { get; set; }
        [ValidateNever] public BudgetLine? BudgetLine { get; set; }

        public int? TaskId { get; set; }
        [ValidateNever] public ProjectTask? Task { get; set; }

        [Required, StringLength(250)]
        public string Description { get; set; } = string.Empty;

        public CostCategory Category { get; set; } = CostCategory.Other;

        [Column(TypeName = "decimal(18,2)")]
        [Range(0, double.MaxValue)]
        public decimal Amount { get; set; }

        [StringLength(10)]
        public string Currency { get; set; } = "USD";

        [DataType(DataType.Date)]
        public DateTime ExpenseDate { get; set; } = DateTime.Today;

        [StringLength(200)]
        public string? Supplier { get; set; }

        [StringLength(120)]
        public string? ReceiptReference { get; set; }

        /// <summary>Path (relative to wwwroot) of the uploaded receipt image or PDF.</summary>
        [StringLength(500)]
        public string? ReceiptPath { get; set; }

        public ExpenseStatus Status { get; set; } = ExpenseStatus.Draft;

        public int SubmittedById { get; set; }
        [ValidateNever] public User? SubmittedBy { get; set; }

        public int? ApprovedById { get; set; }
        [ValidateNever] public User? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }

        [StringLength(500)]
        public string? DecisionNote { get; set; }

        public DateTime? ReimbursedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [NotMapped]
        public string Reference => $"EXP-{Id:D5}";
    }

    /// <summary>
    /// A procurement record covering the full request → RFQ → order → receipt → invoice → payment
    /// chain for goods or services bought for a project.
    /// </summary>
    public class ProcurementRequest
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        [ValidateNever] public Project? Project { get; set; }

        public int? BudgetLineId { get; set; }
        [ValidateNever] public BudgetLine? BudgetLine { get; set; }

        [Required, StringLength(250)]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        public CostCategory Category { get; set; } = CostCategory.Other;

        [Column(TypeName = "decimal(18,2)")]
        public decimal EstimatedCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OrderedAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal InvoicedAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PaidAmount { get; set; }

        [StringLength(10)]
        public string Currency { get; set; } = "USD";

        public ProcurementStatus Status { get; set; } = ProcurementStatus.Draft;

        /// <summary>Supplier chosen after the RFQ round. Links to the existing ISO supplier register.</summary>
        public int? SelectedSupplierId { get; set; }

        [StringLength(200)]
        public string? SelectedSupplierName { get; set; }

        [StringLength(2000)]
        public string? SupplierSelectionRationale { get; set; }

        [StringLength(60)] public string? PurchaseOrderNumber { get; set; }
        [StringLength(60)] public string? GoodsReceivedNoteNumber { get; set; }
        [StringLength(60)] public string? InvoiceNumber { get; set; }

        [DataType(DataType.Date)] public DateTime? RequiredByDate { get; set; }
        [DataType(DataType.Date)] public DateTime? OrderedDate { get; set; }
        [DataType(DataType.Date)] public DateTime? ReceivedDate { get; set; }
        [DataType(DataType.Date)] public DateTime? InvoiceDate { get; set; }
        [DataType(DataType.Date)] public DateTime? PaidDate { get; set; }

        public int RequestedById { get; set; }
        [ValidateNever] public User? RequestedBy { get; set; }

        public int? ApprovedById { get; set; }
        [ValidateNever] public User? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [NotMapped]
        public string Reference => $"PR-{Id:D5}";

        /// <summary>Outstanding balance on the invoice.</summary>
        [NotMapped]
        public decimal OutstandingAmount => InvoicedAmount - PaidAmount;
    }

    /// <summary>A quotation received against a procurement request, for side-by-side comparison.</summary>
    public class ProcurementQuote
    {
        public int Id { get; set; }

        public int ProcurementRequestId { get; set; }
        [ValidateNever] public ProcurementRequest? ProcurementRequest { get; set; }

        [Required, StringLength(200)]
        public string SupplierName { get; set; } = string.Empty;

        public int? SupplierId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal QuotedAmount { get; set; }

        [StringLength(10)]
        public string Currency { get; set; } = "USD";

        public int LeadTimeDays { get; set; }

        /// <summary>Evaluation score out of 100 across price, quality and delivery.</summary>
        [Range(0, 100)]
        public int Score { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        public bool IsSelected { get; set; }

        [DataType(DataType.Date)] public DateTime? ReceivedDate { get; set; }
    }

    /// <summary>An asset (laptop, vehicle, tool…) issued to a project and tracked until returned.</summary>
    public class ProjectAsset
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        [ValidateNever] public Project? Project { get; set; }

        /// <summary>Links to the organisation-wide asset register when the item is already recorded there.</summary>
        public int? AssetId { get; set; }
        [ValidateNever] public Asset? Asset { get; set; }

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(120)]
        public string? AssetTag { get; set; }

        [StringLength(60)]
        public string? Kind { get; set; }

        public int? IssuedToId { get; set; }
        [ValidateNever] public User? IssuedTo { get; set; }

        [DataType(DataType.Date)] public DateTime? IssuedDate { get; set; }
        [DataType(DataType.Date)] public DateTime? DueBackDate { get; set; }
        [DataType(DataType.Date)] public DateTime? ReturnedDate { get; set; }

        [StringLength(60)]
        public string? ConditionOnIssue { get; set; }

        [StringLength(60)]
        public string? ConditionOnReturn { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        [NotMapped]
        public bool IsOutstanding => ReturnedDate == null;
    }
}
