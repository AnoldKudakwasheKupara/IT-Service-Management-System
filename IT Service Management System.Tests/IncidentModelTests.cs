using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models.Ims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace IT_Service_Management_System.Tests
{
    public class IncidentModelTests
    {
        private static ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=IncidentModelTests;Trusted_Connection=True")
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public void Incident_reference_index_is_unique()
        {
            using var context = CreateContext();
            var entity = context.Model.FindEntityType(typeof(Incident))!;
            var index = entity.GetIndexes().Single(i =>
                i.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(Incident.Year), nameof(Incident.IncidentNo) }));

            Assert.True(index.IsUnique);
        }

        [Theory]
        [InlineData(nameof(Incident.DeptManagerSignedById))]
        [InlineData(nameof(Incident.QaSignedById))]
        [InlineData(nameof(Incident.GmSignedById))]
        public void Incident_signer_relationships_preserve_audit_identity(string foreignKeyProperty)
        {
            using var context = CreateContext();
            var entity = context.Model.FindEntityType(typeof(Incident))!;
            var relationship = entity.GetForeignKeys().Single(fk =>
                fk.Properties.Count == 1 && fk.Properties[0].Name == foreignKeyProperty);

            Assert.Equal(DeleteBehavior.Restrict, relationship.DeleteBehavior);
        }
    }
}
