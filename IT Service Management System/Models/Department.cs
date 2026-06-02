namespace IT_Service_Management_System.Models
{
    public class Department
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string? Description { get; set; }

        // HOD
        public int? HodId { get; set; }
        public User? Hod { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<User> Users { get; set; } = new List<User>();
    }
}