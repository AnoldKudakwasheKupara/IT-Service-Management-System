using System.ComponentModel.DataAnnotations;

namespace IT_Service_Management_System.Models
{
    /// <summary>A reusable reply snippet staff can insert into a ticket conversation.</summary>
    public class CannedResponse
    {
        public int Id { get; set; }

        [Required]
        [StringLength(120)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(4000)]
        public string Body { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
