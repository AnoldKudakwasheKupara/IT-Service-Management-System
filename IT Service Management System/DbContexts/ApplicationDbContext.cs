using IT_Service_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.DbContexts
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<TicketMessage> TicketMessages { get; set; }
        public DbSet<TicketAttachment> TicketAttachments { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Asset> Assets { get; set; }
        public DbSet<AssetHistory> AssetHistories { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<SSLCertificate> SSLCertificates { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ✅ ENUMS AS STRINGS
            modelBuilder.Entity<Ticket>()
                .Property(t => t.Status)
                .HasConversion<string>();

            modelBuilder.Entity<Ticket>()
                .Property(t => t.Priority)
                .HasConversion<string>();

            modelBuilder.Entity<User>()
                .Property(u => u.Role)
                .HasConversion<string>();

            // ✅ USER CONSTRAINTS
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // ✅ TICKET RELATIONSHIPS
            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.CreatedBy)
                .WithMany(u => u.TicketsCreated)
                .HasForeignKey(t => t.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.AssignedTo)
                .WithMany()
                .HasForeignKey(t => t.AssignedToId)
                .OnDelete(DeleteBehavior.Restrict);

            // ✅ MESSAGE RELATIONSHIPS
            modelBuilder.Entity<TicketMessage>()
                .HasOne(m => m.Ticket)
                .WithMany(t => t.Messages)
                .HasForeignKey(m => m.TicketId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TicketMessage>()
                .HasOne(m => m.Sender)
                .WithMany(u => u.Messages)
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            // ✅ ATTACHMENTS
            modelBuilder.Entity<TicketAttachment>()
                .HasOne(a => a.Ticket)
                .WithMany(t => t.Attachments)
                .HasForeignKey(a => a.TicketId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TicketAttachment>()
                .HasOne(a => a.TicketMessage)
                .WithMany(m => m.Attachments)
                .HasForeignKey(a => a.TicketMessageId)
                .OnDelete(DeleteBehavior.Restrict);

            // ✅ OPTIONAL: REQUIRE EITHER TICKET OR MESSAGE (NOT BOTH NULL)
            modelBuilder.Entity<TicketAttachment>()
                .HasCheckConstraint("CK_Attachment_Owner",
                    "[TicketId] IS NOT NULL OR [TicketMessageId] IS NOT NULL");
        }
    }
}