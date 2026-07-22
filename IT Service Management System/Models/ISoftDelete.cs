namespace IT_Service_Management_System.Models
{
    /// <summary>
    /// Marks an entity as soft-deletable. A global EF query filter excludes rows where
    /// IsDeleted is true, so deleted records are retained (for audit/compliance) but hidden
    /// from normal queries.
    /// </summary>
    public interface ISoftDelete
    {
        bool IsDeleted { get; set; }
        DateTime? DeletedAt { get; set; }
    }
}
