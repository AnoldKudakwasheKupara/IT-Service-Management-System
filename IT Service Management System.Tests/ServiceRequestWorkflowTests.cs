using IT_Service_Management_System.Models.Itsm;
using IT_Service_Management_System.Services.Itsm;

namespace IT_Service_Management_System.Tests
{
    public class ServiceRequestWorkflowTests
    {
        [Theory]
        [InlineData(ServiceRequestStatus.AwaitingApproval, ServiceRequestStatus.Approved)]
        [InlineData(ServiceRequestStatus.AwaitingApproval, ServiceRequestStatus.Rejected)]
        [InlineData(ServiceRequestStatus.Approved, ServiceRequestStatus.InFulfillment)]
        [InlineData(ServiceRequestStatus.InFulfillment, ServiceRequestStatus.OnHold)]
        [InlineData(ServiceRequestStatus.OnHold, ServiceRequestStatus.InFulfillment)]
        [InlineData(ServiceRequestStatus.InFulfillment, ServiceRequestStatus.Fulfilled)]
        public void Valid_transitions_are_allowed(ServiceRequestStatus current, ServiceRequestStatus next)
            => Assert.True(ServiceRequestWorkflow.CanTransition(current, next));

        [Theory]
        [InlineData(ServiceRequestStatus.AwaitingApproval, ServiceRequestStatus.Fulfilled)]
        [InlineData(ServiceRequestStatus.Approved, ServiceRequestStatus.Fulfilled)]
        [InlineData(ServiceRequestStatus.Fulfilled, ServiceRequestStatus.InFulfillment)]
        [InlineData(ServiceRequestStatus.Rejected, ServiceRequestStatus.Approved)]
        [InlineData(ServiceRequestStatus.Cancelled, ServiceRequestStatus.Approved)]
        public void Invalid_transitions_are_rejected(ServiceRequestStatus current, ServiceRequestStatus next)
            => Assert.False(ServiceRequestWorkflow.CanTransition(current, next));
    }
}
