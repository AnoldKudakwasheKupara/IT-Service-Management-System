using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IT_Service_Management_System.Models.Itsm
{
    /// <summary>
    /// A Configuration Item (CI) in the CMDB — a server, application, service, database, etc.
    /// Incidents (tickets), problems and changes reference CIs so impact can be traced.
    /// Optionally linked to a physical <see cref="Asset"/>.
    /// </summary>
    public class ConfigurationItem
    {
        public int Id { get; set; }

        [Required, StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [NotMapped]
        public string CiCode => $"CI-{Id:D5}";

        public CiType Type { get; set; } = CiType.Application;
        public CiStatus Status { get; set; } = CiStatus.Active;
        public CiCriticality Criticality { get; set; } = CiCriticality.Medium;
        public CiEnvironment Environment { get; set; } = CiEnvironment.Production;

        [StringLength(1000)]
        public string? Description { get; set; }

        [StringLength(150)]
        public string? Location { get; set; }

        [StringLength(150)]
        public string? Vendor { get; set; }

        [StringLength(60)]
        public string? Version { get; set; }

        [StringLength(200)]
        public string? IpOrHostname { get; set; }

        public int? OwnerId { get; set; }
        [ValidateNever]
        public User? Owner { get; set; }

        /// <summary>Optional link to the physical asset record this CI represents.</summary>
        public int? AssetId { get; set; }
        [ValidateNever]
        public Asset? Asset { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ValidateNever]
        public ICollection<Ticket> Incidents { get; set; } = new List<Ticket>();
        [ValidateNever]
        public ICollection<Problem> Problems { get; set; } = new List<Problem>();
        [ValidateNever]
        public ICollection<ChangeRequest> Changes { get; set; } = new List<ChangeRequest>();
    }
}
