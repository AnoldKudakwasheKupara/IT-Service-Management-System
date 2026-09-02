using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace IT_Service_Management_System.Filters
{
    /// <summary>
    /// Global filter that hides the modules retired from the UI. Their code and data
    /// are left intact, but every route below is unreachable, so a retired module
    /// cannot be entered by typing its URL after its nav link was removed.
    /// To bring a module back, drop its controller name from <see cref="Retired"/>
    /// and restore its link in _Layout.cshtml.
    /// </summary>
    public class ModuleAvailabilityFilter : IAuthorizationFilter
    {
        /// <summary>Controllers whose module is no longer part of the product.</summary>
        private static readonly HashSet<string> Retired = new(StringComparer.OrdinalIgnoreCase)
        {
            // Personal workspace / misc
            "MyWork", "MeetingMinutes",

            // Service management (ITIL)
            "MajorIncidents", "Problems", "Changes", "Cmdb", "SlaPolicies", "SlaCalendars",

            // ISO / IMS / compliance
            "Ecie", "Ims", "IsoDocuments", "IsoAudits", "IsoReports", "IsoAssistant",
            "Capa", "NonConformances", "Incidents", "Risk", "Suppliers", "Training",
            "ManagementReviews", "Objectives", "ComplianceRegister", "Compliance",
            "Improvements", "Evidence",

            // Project management
            "Projects", "ProjectTasks", "ProjectPlan", "ProjectRegisters", "ProjectFinance",
            "ProjectDocs", "ProjectApprovals", "ProjectReports", "ProjectResources",
            "ProjectTime", "ProjectPortal",

            // HR
            "Employees", "Leave", "Payroll", "Attendance", "Benefits", "Appraisals",
            "Onboarding", "Recruitment", "Disciplinary", "ExitClearance", "ExitInterview",
            "EngagementStayInterview", "TalentIdentification", "EmployeeDocuments",
            "EfmSettings"
        };

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            if (context.ActionDescriptor is not ControllerActionDescriptor action) return;

            if (!Retired.Contains(action.ControllerName)) return;

            // Anonymous endpoints (e.g. public share links) are retired too.
            context.Result = new NotFoundResult();
        }
    }
}
