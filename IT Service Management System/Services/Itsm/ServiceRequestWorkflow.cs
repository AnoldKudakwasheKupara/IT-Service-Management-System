using IT_Service_Management_System.Models.Itsm;

namespace IT_Service_Management_System.Services.Itsm
{
    /// <summary>Central transition rules for service requests.</summary>
    public static class ServiceRequestWorkflow
    {
        public static bool CanTransition(ServiceRequestStatus current, ServiceRequestStatus next)
        {
            if (current == next) return true;

            return current switch
            {
                ServiceRequestStatus.AwaitingApproval => next is ServiceRequestStatus.Approved
                    or ServiceRequestStatus.Rejected or ServiceRequestStatus.Cancelled,
                ServiceRequestStatus.Approved => next is ServiceRequestStatus.InFulfillment
                    or ServiceRequestStatus.Cancelled,
                ServiceRequestStatus.InFulfillment => next is ServiceRequestStatus.OnHold
                    or ServiceRequestStatus.Fulfilled or ServiceRequestStatus.Cancelled,
                ServiceRequestStatus.OnHold => next is ServiceRequestStatus.InFulfillment
                    or ServiceRequestStatus.Cancelled,
                _ => false
            };
        }
    }
}
