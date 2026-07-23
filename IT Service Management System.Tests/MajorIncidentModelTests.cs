using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models.Itsm;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IT_Service_Management_System.Tests
{
    public class MajorIncidentModelTests
    {
        private static ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=MajorIncidentModelTests;Trusted_Connection=True")
                .Options;
            return new ApplicationDbContext(options);
        }

        [Theory]
        [InlineData(nameof(MajorIncident.Status))]
        [InlineData(nameof(MajorIncident.Severity))]
        public void Enum_properties_are_stored_as_strings(string property)
        {
            using var context = CreateContext();
            var entity = context.Model.FindEntityType(typeof(MajorIncident))!;
            var prop = entity.FindProperty(property)!;
            Assert.Equal("nvarchar", prop.GetColumnType()?.Split('(')[0] ?? prop.GetColumnType());
        }

        [Theory]
        [InlineData(typeof(MajorIncidentAffectedItem))]
        [InlineData(typeof(MajorIncidentTimelineEntry))]
        [InlineData(typeof(MajorIncidentUpdate))]
        [InlineData(typeof(MajorIncidentFollowUp))]
        public void Child_collections_cascade_with_parent(System.Type childType)
        {
            using var context = CreateContext();
            var entity = context.Model.FindEntityType(childType)!;
            var fk = entity.GetForeignKeys().Single(f =>
                f.PrincipalEntityType.ClrType == typeof(MajorIncident));

            Assert.Equal(DeleteBehavior.Cascade, fk.DeleteBehavior);
        }

        [Theory]
        [InlineData(nameof(MajorIncident.CommanderId))]
        [InlineData(nameof(MajorIncident.TechnicalLeadId))]
        [InlineData(nameof(MajorIncident.CommunicationsLeadId))]
        [InlineData(nameof(MajorIncident.DeclaredById))]
        [InlineData(nameof(MajorIncident.ReviewFacilitatorId))]
        public void User_links_never_cascade(string foreignKeyProperty)
        {
            using var context = CreateContext();
            var entity = context.Model.FindEntityType(typeof(MajorIncident))!;
            var fk = entity.GetForeignKeys().Single(f =>
                f.Properties.Count == 1 && f.Properties[0].Name == foreignKeyProperty);

            Assert.True(fk.DeleteBehavior is DeleteBehavior.NoAction or DeleteBehavior.Restrict);
        }

        [Fact]
        public void Source_ticket_link_sets_null_on_delete()
        {
            using var context = CreateContext();
            var entity = context.Model.FindEntityType(typeof(MajorIncident))!;
            var fk = entity.GetForeignKeys().Single(f =>
                f.Properties.Count == 1 && f.Properties[0].Name == nameof(MajorIncident.SourceTicketId));

            Assert.Equal(DeleteBehavior.SetNull, fk.DeleteBehavior);
        }
    }
}
