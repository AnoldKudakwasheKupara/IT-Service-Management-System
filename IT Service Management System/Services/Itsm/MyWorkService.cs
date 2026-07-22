using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Enums;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Models.Efm;
using IT_Service_Management_System.Models.Ims;
using IT_Service_Management_System.Models.Itsm;
using IT_Service_Management_System.ViewModels.Itsm;
using Microsoft.EntityFrameworkCore;
using static IT_Service_Management_System.Models.Ticket;

namespace IT_Service_Management_System.Services.Itsm
{
    public interface IMyWorkService
    {
        Task<List<WorkItemVm>> GetAsync(int userId, string? role, DateTime now, CancellationToken ct = default);
    }

    /// <summary>Aggregates actionable records across ITSM, IMS and document workflows.</summary>
    public class MyWorkService : IMyWorkService
    {
        private readonly ApplicationDbContext _db;
        public MyWorkService(ApplicationDbContext db) => _db = db;

        public async Task<List<WorkItemVm>> GetAsync(int userId, string? role, DateTime now, CancellationToken ct = default)
        {
            var items = new List<WorkItemVm>();
            var fullAccess = Roles.IsFullAccess(role);

            var tickets = await _db.Tickets.AsNoTracking()
                .Where(t => t.AssignedToId == userId && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed)
                .ToListAsync(ct);
            items.AddRange(tickets.Select(t => new WorkItemVm
            {
                Kind = "Ticket", Reference = t.Reference, Title = t.Title, Status = t.Status.ToString(),
                Source = "Helpdesk", Url = $"/Tickets/Details/{t.Id}", DueAt = t.DueAt, CreatedAt = t.CreatedAt,
                PriorityScore = (int)t.Priority + 1,
                IsSlaRisk = t.IsSlaBreached || (t.DueAt.HasValue && t.DueAt <= now.AddHours(4))
            }));

            var requests = await _db.ServiceRequests.AsNoTracking().Include(r => r.ServiceCatalogItem)
                .Where(r => r.AssignedToId == userId && r.Status != ServiceRequestStatus.AwaitingApproval &&
                            r.Status != ServiceRequestStatus.Fulfilled &&
                            r.Status != ServiceRequestStatus.Rejected && r.Status != ServiceRequestStatus.Cancelled)
                .ToListAsync(ct);
            items.AddRange(requests.Select(r => new WorkItemVm
            {
                Kind = "Service Request", Reference = r.Reference, Title = r.Subject,
                Status = r.Status.ToString(), Source = r.ServiceCatalogItem?.Name ?? "Service Catalogue",
                Url = $"/ServiceCatalog/RequestDetails/{r.Id}", DueAt = r.DueAt, CreatedAt = r.CreatedAt,
                PriorityScore = (int)r.Priority + 1, IsSlaRisk = r.IsOverdue
            }));

            var actions = await _db.ActionItems.AsNoTracking()
                .Where(a => a.AssignedToId == userId && a.Status != ActionItemStatus.Done).ToListAsync(ct);
            items.AddRange(actions.Select(a => new WorkItemVm
            {
                Kind = "Action Item", Reference = $"ACT-{a.Id:D5}", Title = a.Title,
                Status = a.Status.ToString(), Source = "Meeting Minutes", Url = $"/MeetingMinutes/Details/{a.MeetingId}",
                DueAt = a.DueDate, CreatedAt = a.CreatedAt, PriorityScore = (int)a.Priority + 1
            }));

            var capas = await _db.Capas.AsNoTracking()
                .Where(c => c.ResponsibleId == userId && c.Status != CapaStatus.Closed && c.Status != CapaStatus.Verified)
                .ToListAsync(ct);
            items.AddRange(capas.Select(c => new WorkItemVm
            {
                Kind = "CAPA", Reference = c.Reference, Title = c.Title, Status = c.Status.ToString(),
                Source = "IMS", Url = $"/Capa/Details/{c.Id}", DueAt = c.DueDate, CreatedAt = c.CreatedAt,
                PriorityScore = c.Escalated ? 4 : 2
            }));

            var findings = await _db.AuditFindings.AsNoTracking()
                .Where(f => f.AssignedToId == userId && f.Status != FindingStatus.Closed && f.Status != FindingStatus.Verified)
                .ToListAsync(ct);
            items.AddRange(findings.Select(f => new WorkItemVm
            {
                Kind = "Audit Finding", Reference = f.Reference, Title = f.Description,
                Status = f.Status.ToString(), Source = "Internal Audit", Url = $"/IsoAudits/FindingDetails/{f.Id}",
                DueAt = f.DueDate, CreatedAt = f.CreatedAt, PriorityScore = f.IsNonConformance ? 3 : 1
            }));

            var reviewActions = await _db.ManagementReviewActions.AsNoTracking()
                .Where(a => a.AssignedToId == userId && a.Status != ReviewActionStatus.Completed &&
                            a.Status != ReviewActionStatus.Cancelled).ToListAsync(ct);
            items.AddRange(reviewActions.Select(a => new WorkItemVm
            {
                Kind = "Review Action", Reference = a.Reference, Title = a.Description,
                Status = a.Status.ToString(), Source = "Management Review",
                Url = $"/ManagementReviews/Details/{a.ManagementReviewId}", DueAt = a.DueDate,
                CreatedAt = a.CreatedAt, PriorityScore = 2
            }));

            var documentApprovals = await _db.DocumentApprovals.AsNoTracking().Include(a => a.Document)
                .Where(a => a.Status == ApprovalStatus.Pending &&
                            (a.ApproverUserId == userId || (a.ApproverUserId == null && a.ApproverRole == role)))
                .ToListAsync(ct);
            items.AddRange(documentApprovals.Select(a => new WorkItemVm
            {
                Kind = "Approval", Reference = $"DOC-{a.EmployeeDocumentId:D5}",
                Title = a.Document?.Title ?? "Employee document approval", Status = "Pending",
                Source = "Employee Documents", Url = $"/EmployeeDocuments/Details/{a.EmployeeDocumentId}",
                CreatedAt = a.CreatedAt, PriorityScore = 3, RequiresDecision = true
            }));

            var acknowledgements = await _db.IsoDocumentAcknowledgements.AsNoTracking().Include(a => a.Document)
                .Where(a => a.UserId == userId && a.Status != AcknowledgementStatus.Acknowledged).ToListAsync(ct);
            items.AddRange(acknowledgements.Select(a => new WorkItemVm
            {
                Kind = "Acknowledgement", Reference = a.Document?.DocumentNumber ?? $"ISO-{a.IsoDocumentId:D5}",
                Title = a.Document?.Title ?? "ISO document acknowledgement", Status = a.Status.ToString(),
                Source = "Document Control", Url = $"/IsoDocuments/Details/{a.IsoDocumentId}",
                CreatedAt = a.AssignedAt, PriorityScore = 2, RequiresDecision = true
            }));

            if (fullAccess)
            {
                var requestApprovals = await _db.ServiceRequests.AsNoTracking().Include(r => r.ServiceCatalogItem)
                    .Where(r => r.Status == ServiceRequestStatus.AwaitingApproval).ToListAsync(ct);
                items.AddRange(requestApprovals.Select(r => new WorkItemVm
                {
                    Kind = "Approval", Reference = r.Reference, Title = r.Subject, Status = "AwaitingApproval",
                    Source = r.ServiceCatalogItem?.Name ?? "Service Catalogue",
                    Url = $"/ServiceCatalog/RequestDetails/{r.Id}", DueAt = r.DueAt, CreatedAt = r.CreatedAt,
                    PriorityScore = (int)r.Priority + 1, RequiresDecision = true
                }));

                var changes = await _db.ChangeRequests.AsNoTracking()
                    .Where(c => c.Status == ChangeStatus.SubmittedForApproval).ToListAsync(ct);
                items.AddRange(changes.Select(c => new WorkItemVm
                {
                    Kind = "Approval", Reference = c.ChangeRef, Title = c.Title, Status = c.Status.ToString(),
                    Source = "Change Management", Url = $"/Changes/Details/{c.Id}",
                    CreatedAt = c.CreatedAt, PriorityScore = c.Risk == ChangeRisk.High ? 4 : 3, RequiresDecision = true
                }));
            }

            await AddIncidentSignoffsAsync(items, userId, role, fullAccess, ct);
            return MyWorkOrdering.Prioritize(items, now).ToList();
        }

        private async Task AddIncidentSignoffsAsync(List<WorkItemVm> items, int userId, string? role,
            bool fullAccess, CancellationToken ct)
        {
            IQueryable<Incident> query = _db.Incidents.AsNoTracking().Where(i => i.Status != IncidentStatus.Closed);
            string? stage = null;
            if (role == Roles.DepartmentManager)
            {
                var departmentId = await _db.Users.Where(u => u.Id == userId).Select(u => u.DepartmentId).FirstOrDefaultAsync(ct);
                query = query.Where(i => i.DepartmentId == departmentId && i.DeptManagerSignedById == null);
                stage = "Department sign-off";
            }
            else if (role == Roles.QualityManager)
            {
                query = query.Where(i => i.QaSignedById == null);
                stage = "Quality sign-off";
            }
            else if (role == Roles.GeneralManager)
            {
                query = query.Where(i => i.Severity == IncidentSeverity.Major && i.GmSignedById == null);
                stage = "General Manager sign-off";
            }
            else if (fullAccess)
            {
                query = query.Where(i => i.QaSignedById == null ||
                    (i.Severity == IncidentSeverity.Major && i.GmSignedById == null));
                stage = "Management sign-off";
            }
            else return;

            var incidents = await query.ToListAsync(ct);
            items.AddRange(incidents.Select(i => new WorkItemVm
            {
                Kind = "Approval", Reference = i.Reference, Title = i.Title, Status = stage!, Source = "Incidents",
                Url = $"/Incidents/Details/{i.Id}", CreatedAt = i.CreatedAt,
                PriorityScore = i.Severity == IncidentSeverity.Major ? 4 : 3, RequiresDecision = true
            }));
        }
    }
}
