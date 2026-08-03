using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models
{
    /// <summary>
    /// One year of KPI achievement on a talent assessment.
    /// <para>
    /// This replaces the fixed <c>KPI2023</c>–<c>KPI2026</c> columns. Those worked until the
    /// calendar moved: recording 2027 meant a model change, a migration and a view change, every
    /// year, forever — and there was no way to record more than four years or to start before 2023.
    /// A row per year costs nothing and never needs a schema change again.
    /// </para>
    /// </summary>
    public class TalentKpiYear
    {
        public int Id { get; set; }

        public int TalentIdentificationId { get; set; }
        [ValidateNever] public TalentIdentification? TalentIdentification { get; set; }

        /// <summary>The performance year this achievement belongs to.</summary>
        [Range(1990, 2100, ErrorMessage = "Enter a year between 1990 and 2100.")]
        public int Year { get; set; }

        [StringLength(1000)]
        [Display(Name = "KPI achievement")]
        public string Achievement { get; set; } = string.Empty;

        /// <summary>Optional score or rating for the year, where the organisation records one.</summary>
        [StringLength(60)]
        public string? Rating { get; set; }
    }
}
