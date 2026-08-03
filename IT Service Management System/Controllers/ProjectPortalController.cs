using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Filters;
using IT_Service_Management_System.Models.Pm;
using IT_Service_Management_System.Services.Pm;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>
    /// The client portal. A client user sees only the projects booked to their organisation, and
    /// only the parts of those projects marked visible to the client — progress, milestones,
    /// shared documents and the discussion thread. They can approve milestones, raise issues,
    /// upload documents and comment; nothing else.
    /// </summary>
    [RoleAuthorize("Client", "Admin", "SystemsAdmin")]
    public class ProjectPortalController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ProjectActivityService _activity;
        private readonly ProjectMetricsService _metrics;
        private readonly PmFileService _files;

        public ProjectPortalController(ApplicationDbContext db, ProjectActivityService activity,
            ProjectMetricsService metrics, PmFileService files)
        {
            _db = db; _activity = activity; _metrics = metrics; _files = files;
        }

        private int Uid => HttpContext.Session.GetInt32("UserId") ?? 0;
        private string? Role => HttpContext.Session.GetString("UserRole");
        private IActionResult AccessDenied() => RedirectToAction("AccessDenied", "Home");

        /// <summary>
        /// The client's own organisation name, taken from their user record's department. Projects
        /// are matched to it, so a client can never reach another client's work.
        /// </summary>
        private async Task<string?> ClientNameAsync()
        {
            var user = await _db.Users.AsNoTracking()
                .Include(u => u.Department)
                .Where(u => u.Id == Uid)
                .Select(u => new { u.FirstName, u.LastName, Department = u.Department!.Name })
                .FirstOrDefaultAsync();
            return user?.Department;
        }

        /// <summary>Every project this client is entitled to see.</summary>
        private async Task<List<int>> VisibleProjectIdsAsync()
        {
            var clientName = await ClientNameAsync();
            if (string.IsNullOrWhiteSpace(clientName)) return new List<int>();

            return await _db.Projects.AsNoTracking()
                .Where(p => p.Client == clientName && p.Status != ProjectStatus.Draft && p.Status != ProjectStatus.Archived)
                .Select(p => p.Id)
                .ToListAsync();
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Portal home
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Index()
        {
            var ids = await VisibleProjectIdsAsync();
            var clientName = await ClientNameAsync();

            var projects = await _db.Projects.AsNoTracking()
                .Include(p => p.ProjectManager)
                .Where(p => ids.Contains(p.Id))
                .OrderByDescending(p => p.Status == ProjectStatus.Active)
                .ThenBy(p => p.EndDate)
                .ToListAsync();

            ViewBag.ClientName = clientName;
            ViewBag.PendingApprovals = await _db.Milestones.CountAsync(m =>
                ids.Contains(m.ProjectId) && m.RequiresClientApproval && !m.ClientApproved);
            ViewBag.OpenIssues = await _db.ProjectIssues.CountAsync(i =>
                ids.Contains(i.ProjectId) && i.RaisedByClient
                && i.Status != IssueStatus.Resolved && i.Status != IssueStatus.Closed);
            ViewBag.SharedDocuments = await _db.ProjectDocuments.CountAsync(d =>
                ids.Contains(d.ProjectId) && d.VisibleToClient);

            return View(projects);
        }

        public async Task<IActionResult> Project(int id)
        {
            var ids = await VisibleProjectIdsAsync();
            if (!ids.Contains(id)) return AccessDenied();

            var project = await _db.Projects.AsNoTracking()
                .Include(p => p.ProjectManager)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (project == null) return NotFound();

            // Only client-facing content, deliberately: no budget, no internal tasks, no risks.
            ViewBag.Milestones = await _db.Milestones.AsNoTracking()
                .Where(m => m.ProjectId == id && m.Status != MilestoneStatus.Cancelled)
                .OrderBy(m => m.DueDate).ToListAsync();
            ViewBag.Deliverables = await _db.Deliverables.AsNoTracking()
                .Where(d => d.ProjectId == id).OrderBy(d => d.DueDate ?? DateTime.MaxValue).ToListAsync();
            ViewBag.Documents = await _db.ProjectDocuments.AsNoTracking()
                .Include(d => d.UploadedBy)
                .Where(d => d.ProjectId == id && d.VisibleToClient)
                .OrderByDescending(d => d.UpdatedAt ?? d.CreatedAt).ToListAsync();
            ViewBag.Issues = await _db.ProjectIssues.AsNoTracking()
                .Where(i => i.ProjectId == id && i.RaisedByClient)
                .OrderByDescending(i => i.CreatedAt).ToListAsync();
            ViewBag.Discussion = await _db.ProjectDiscussions.AsNoTracking()
                .Include(d => d.Author)
                .Where(d => d.ProjectId == id && d.VisibleToClient)
                .OrderByDescending(d => d.CreatedAt).Take(50).ToListAsync();

            return View(project);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Client actions
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>Sign a milestone off. Only milestones explicitly flagged for client approval.</summary>
        [HttpPost]
        public async Task<IActionResult> ApproveMilestone(int id, int milestoneId, string? notes)
        {
            var ids = await VisibleProjectIdsAsync();
            if (!ids.Contains(id)) return AccessDenied();

            var milestone = await _db.Milestones.FirstOrDefaultAsync(m => m.Id == milestoneId && m.ProjectId == id);
            if (milestone == null) return NotFound();

            if (!milestone.RequiresClientApproval)
            {
                TempData["Error"] = "That milestone is not awaiting your approval.";
                return RedirectToAction(nameof(Project), new { id });
            }

            milestone.ClientApproved = true;
            milestone.ClientApprovedAt = DateTime.Now;
            milestone.Notes = string.IsNullOrWhiteSpace(notes) ? milestone.Notes : notes;
            if (milestone.Status == MilestoneStatus.Planned) milestone.Status = MilestoneStatus.Achieved;
            milestone.AchievedDate ??= DateTime.Today;

            _activity.Log(id, nameof(Milestone), milestoneId, "ClientApproved", milestone.Name);
            _activity.NotifyMany(await _activity.ProjectAudienceAsync(id), PmNotificationType.MilestoneAchieved,
                $"Client approved: {milestone.Name}", notes,
                Url.Action("Milestones", "ProjectPlan", new { projectId = id }), id);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Milestone approved. Thank you.";
            return RedirectToAction(nameof(Project), new { id });
        }

        /// <summary>Raise an issue with the delivery team.</summary>
        [HttpPost]
        public async Task<IActionResult> RaiseIssue(int id, string title, string description, IssueSeverity severity)
        {
            var ids = await VisibleProjectIdsAsync();
            if (!ids.Contains(id)) return AccessDenied();

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
            {
                TempData["Error"] = "Please give the issue a title and a description.";
                return RedirectToAction(nameof(Project), new { id });
            }

            var issue = new ProjectIssue
            {
                ProjectId = id,
                Title = title.Trim(),
                Description = description.Trim(),
                Severity = severity,
                Priority = severity == IssueSeverity.Critical ? TaskPriority.Critical : TaskPriority.High,
                RaisedById = Uid,
                RaisedByClient = true,
                DueDate = DateTime.Today.AddDays(severity == IssueSeverity.Critical ? 2 : 7)
            };
            _db.ProjectIssues.Add(issue);
            await _db.SaveChangesAsync();

            _activity.Log(id, nameof(ProjectIssue), issue.Id, "Created", $"Raised by the client: {title}");
            _activity.NotifyMany(await _activity.ProjectAudienceAsync(id), PmNotificationType.IssueRaised,
                $"Client raised an issue: {title}", description,
                Url.Action("Issues", "ProjectRegisters", new { projectId = id }), id);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Your issue has been logged and the project team notified.";
            return RedirectToAction(nameof(Project), new { id });
        }

        /// <summary>Post to the project discussion. Client posts are always client-visible.</summary>
        [HttpPost]
        public async Task<IActionResult> Comment(int id, string body)
        {
            var ids = await VisibleProjectIdsAsync();
            if (!ids.Contains(id)) return AccessDenied();

            if (string.IsNullOrWhiteSpace(body))
                return RedirectToAction(nameof(Project), new { id });

            _db.ProjectDiscussions.Add(new ProjectDiscussion
            {
                ProjectId = id,
                Body = body.Trim(),
                AuthorId = Uid,
                VisibleToClient = true,
                CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();

            _activity.NotifyMany(await _activity.ProjectAudienceAsync(id), PmNotificationType.Comment,
                "New client comment", body.Length > 200 ? body[..200] : body,
                Url.Action("Discussion", "ProjectDocs", new { projectId = id }), id);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Project), new { id });
        }

        /// <summary>Upload a document for the delivery team — a signed contract, a specification.</summary>
        [HttpPost]
        [RequestSizeLimit(30_000_000)]
        public async Task<IActionResult> UploadDocument(int id, string title, IFormFile? file)
        {
            var ids = await VisibleProjectIdsAsync();
            if (!ids.Contains(id)) return AccessDenied();

            var saved = await _files.SaveAsync(file, "documents", id);
            if (saved == null)
            {
                TempData["Error"] = _files.LastError ?? "The document could not be uploaded.";
                return RedirectToAction(nameof(Project), new { id });
            }

            var document = new ProjectDocument
            {
                ProjectId = id,
                Title = string.IsNullOrWhiteSpace(title) ? saved.OriginalName : title.Trim(),
                Description = "Uploaded by the client through the portal.",
                Type = ProjectDocumentType.Other,
                Status = ProjectDocumentStatus.UnderReview,
                VisibleToClient = true,
                FileName = saved.OriginalName,
                StoredPath = saved.RelativePath,
                ContentType = saved.ContentType,
                SizeBytes = saved.Size,
                UploadedById = Uid
            };
            _db.ProjectDocuments.Add(document);
            await _db.SaveChangesAsync();

            _activity.Log(id, nameof(ProjectDocument), document.Id, "Uploaded", $"Client upload: {document.Title}");
            _activity.NotifyMany(await _activity.ProjectAudienceAsync(id), PmNotificationType.StatusChanged,
                "Client uploaded a document", document.Title,
                Url.Action("Details", "ProjectDocs", new { id = document.Id }), id);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Document uploaded. The project team has been notified.";
            return RedirectToAction(nameof(Project), new { id });
        }
    }
}
