using IT_Service_Management_System.Helpers.Ims;
using Xunit;

namespace IT_Service_Management_System.Tests
{
    public class ImsAccessTests
    {
        [Theory]
        [InlineData("Admin")]
        [InlineData("SystemsAdmin")]
        public void Administrators_can_do_everything(string role)
        {
            Assert.True(ImsAccess.Can(role, ImsPermission.ManageConfiguration));
            Assert.True(ImsAccess.Can(role, ImsPermission.ManagementApprove));
            Assert.True(ImsAccess.Can(role, ImsPermission.DeleteDocument));
        }

        [Fact]
        public void QualityManager_can_do_everything_except_configuration()
        {
            Assert.True(ImsAccess.Can("QualityManager", ImsPermission.ManageAuditProgramme));
            Assert.True(ImsAccess.Can("QualityManager", ImsPermission.ManagementApprove));
            Assert.False(ImsAccess.Can("QualityManager", ImsPermission.ManageConfiguration));
        }

        [Fact]
        public void ExternalAuditor_is_read_only()
        {
            Assert.True(ImsAccess.Can("ExternalAuditor", ImsPermission.ViewDocuments));
            Assert.True(ImsAccess.Can("ExternalAuditor", ImsPermission.ViewAuditTrail));
            Assert.False(ImsAccess.Can("ExternalAuditor", ImsPermission.CreateDocument));
            Assert.False(ImsAccess.Can("ExternalAuditor", ImsPermission.EditDocument));
            Assert.False(ImsAccess.Can("ExternalAuditor", ImsPermission.ManagementApprove));
        }

        [Fact]
        public void Employee_is_self_service_only()
        {
            Assert.True(ImsAccess.Can("Employee", ImsPermission.ViewDocuments));
            Assert.True(ImsAccess.Can("Employee", ImsPermission.AcknowledgeDocument));
            Assert.True(ImsAccess.Can("Employee", ImsPermission.ViewTraining));
            Assert.False(ImsAccess.Can("Employee", ImsPermission.EditDocument));
            Assert.False(ImsAccess.Can("Employee", ImsPermission.ViewRisk));
        }

        [Fact]
        public void Auditor_conducts_audits_but_does_not_edit_documents()
        {
            Assert.True(ImsAccess.Can("Auditor", ImsPermission.ConductAudit));
            Assert.True(ImsAccess.Can("Auditor", ImsPermission.RaiseFinding));
            Assert.False(ImsAccess.Can("Auditor", ImsPermission.EditDocument));
        }

        [Fact]
        public void DocumentController_owns_document_lifecycle_but_not_management_approval()
        {
            Assert.True(ImsAccess.Can("DocumentController", ImsPermission.CreateDocument));
            Assert.True(ImsAccess.Can("DocumentController", ImsPermission.PublishDocument));
            Assert.False(ImsAccess.Can("DocumentController", ImsPermission.ManagementApprove));
        }

        [Fact]
        public void Unknown_or_null_role_can_do_nothing()
        {
            Assert.False(ImsAccess.Can(null, ImsPermission.ViewDocuments));
            Assert.False(ImsAccess.Can("Finance", ImsPermission.ViewDocuments));
        }

        [Theory]
        [InlineData("Admin", true)]
        [InlineData("QualityManager", true)]
        [InlineData("Auditor", true)]
        [InlineData("Employee", true)]
        [InlineData("Finance", false)]
        [InlineData(null, false)]
        public void CanAccessModule_reflects_ims_membership(string? role, bool expected)
            => Assert.Equal(expected, ImsAccess.CanAccessModule(role));
    }
}
