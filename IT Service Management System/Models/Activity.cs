using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IT_Service_Management_System.Models
{
    public class Activity
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } 

        [Required]
        [MaxLength(100)]
        public string Title { get; set; }

        [MaxLength(500)]
        public string Description { get; set; }

        [Required]
        public DateTime StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        [NotMapped]
        public TimeSpan? Duration
        {
            get
            {
                if (EndTime.HasValue)
                    return EndTime.Value - StartTime;
                return null;
            }
        }

        public int CategoryId { get; set; }
        public ActivityCategory Category { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
