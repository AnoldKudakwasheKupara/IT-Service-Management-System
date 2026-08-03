using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Filters;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Helpers.Pm;
using IT_Service_Management_System.Models.Pm;
using IT_Service_Management_System.Services.Pm;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>
    /// Project documents with version control and check-out, project meetings with agendas,
    /// attendance, minutes and action items, and the project discussion feed.
    /// </summary>
    [RoleAuthorize("Admin", "SystemsAdmin", "ProjectManager", "GeneralManager", "TeamLead",
                   "DepartmentManager", "Finance", "Procurement", "Auditor", "Employee", "HR")]
    public class ProjectDocsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ProjectActivityService _activity;
        private readonly ProjectApprovalService _approvals;
        private readonly ProjectIntelligenceService _intelligence;
        private readonly PmFileService _files;

        public ProjectDocsController(ApplicationDbContext db, ProjectActivityService activity,
            ProjectApprovalService approvals, ProjectIntelligenceService intelligence, PmFileService files)
        {
            _db = db; _activity = activity; _approvals = approvals;
            _intelligence = intelligence; _files = files;
        }

        private int Uid => HttpContext.Session.GetInt32("UserId") ?? 0;
        private string? Role => HttpContext.Session.GetString("UserRole");
        private IActionResult AccessDenied() => RedirectToAction("AccessDenied", "Home");

        // ════════════════════════════════════════════════════════════════════════
        //  Documents
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Index(int projectId, ProjectDocumentType? type,
            ProjectDocumentStatus? status, string? q)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanView) return AccessDenied();

            IQueryable<ProjectDocument> query = _db.ProjectDocuments.AsNoTracking()
                .Include(d => d.UploadedBy).Include(d => d.CheckedOutBy).Include(d => d.ApprovedBy)
                .Where(d => d.ProjectId == projectId);

            if (type.HasValue) query = query.Where(d => d.Type == type.Value);
            if (status.HasValue) query = query.Where(d => d.Status == status.Value);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(d => d.Title.Contains(term)
                    || (d.Description != null && d.Description.Contains(term))
                    || (d.Tags != null && d.Tags.Contains(term))
                    || (d.FileName != null && d.FileName.Contains(term)));
            }

            var documents = await query.OrderByDescending(d => d.UpdatedAt ?? d.CreatedAt).ToListAsync();

            ViewBag.Type = type; ViewBag.Status = status; ViewBag.Q = q;
            ViewBag.Approved = documents.Count(d => d.Status == ProjectDocumentStatus.Approved);
            ViewBag.CheckedOut = documents.Count(d => d.IsCheckedOut);
            ViewBag.PendingReview = documents.Count(d => d.Status == ProjectDocumentStatus.UnderReview);

            return View(documents);
        }

        [HttpPost]
        [RequestSizeLimit(30_000_000)]
        public async Task<IActionResult> Upload(int projectId, ProjectDocument input, IFormFile? file)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanContribute) return AccessDenied();

            if (string.IsNullOrWhiteSpace(input.Title))
            {
                TempData["Error"] = "A document needs a title.";
                return RedirectToAction(nameof(Index), new { projectId });
            }

            var saved = await _files.SaveAsync(file, "documents", projectId);
            if (saved == null)
            {
                TempData["Error"] = _files.LastError ?? "The document could not be uploaded.";
                return RedirectToAction(nameof(Index), new { projectId });
            }

            var document = new ProjectDocument
            {
                ProjectId = projectId,
                Title = input.Title,
                Description = input.Description,
                Type = input.Type,
                Category = input.Category,
                Tags = input.Tags,
                VisibleToClient = input.VisibleToClient,
                Status = ProjectDocumentStatus.Draft,
                CurrentVersion = 1,
                FileName = saved.OriginalName,
                StoredPath = saved.RelativePath,
                ContentType = saved.ContentType,
                SizeBytes = saved.Size,
                UploadedById = Uid
            };
            _db.ProjectDocuments.Add(document);
            await _db.SaveChangesAsync();

            _activity.Log(projectId, nameof(ProjectDocument), document.Id, "Uploaded", $"{document.Title} v1");
            await _db.SaveChangesAsync();

            TempData["Success"] = "Document uploaded.";
            return RedirectToAction(nameof(Details), new { id = document.Id });
        }

        public async Task<IActionResult> Details(int id)
        {
            var document = await _db.ProjectDocuments.AsNoTracking()
                .Include(d => d.Project).Include(d => d.UploadedBy)
                .Include(d => d.CheckedOutBy).Include(d => d.ApprovedBy)
                .FirstOrDefaultAsync(d => d.Id == id);
            if (document == null) return NotFound();

            var ctx = await LoadContextAsync(document.ProjectId);
            if (ctx == null || !ctx.Value.CanView) return AccessDenied();

            ViewBag.Versions = await _db.ProjectDocumentVersions.AsNoTracking()
                .Include(v => v.UploadedBy)
                .Where(v => v.DocumentId == id)
                .OrderByDescending(v => v.VersionNumber).ToListAsync();

            return View(document);
        }

        /// <summary>Take exclusive edit rights on a document so two people cannot overwrite each other.</summary>
        [HttpPost]
        public async Task<IActionResult> CheckOut(int id)
        {
            var document = await _db.ProjectDocuments.FirstOrDefaultAsync(d => d.Id == id);
            if (document == null) return NotFound();

            var ctx = await LoadContextAsync(document.ProjectId);
            if (ctx == null || !ctx.Value.CanContribute) return AccessDenied();

            if (document.IsCheckedOut && document.CheckedOutById != Uid)
            {
                TempData["Error"] = "That document is already checked out by someone else.";
                return RedirectToAction(nameof(Details), new { id });
            }

            document.CheckedOutById = Uid;
            document.CheckedOutAt = DateTime.Now;
            _activity.Log(document.ProjectId, nameof(ProjectDocument), id, "CheckedOut", document.Title);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Document checked out to you. Check it back in when you are done.";
            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>
        /// Check a document back in, optionally with a replacement file. The outgoing file is kept
        /// as a numbered version so nothing is ever lost.
        /// </summary>
        [HttpPost]
        [RequestSizeLimit(30_000_000)]
        public async Task<IActionResult> CheckIn(int id, IFormFile? file, string? changeNote)
        {
            var document = await _db.ProjectDocuments.FirstOrDefaultAsync(d => d.Id == id);
            if (document == null) return NotFound();

            var ctx = await LoadContextAsync(document.ProjectId);
            if (ctx == null || !ctx.Value.CanContribute) return AccessDenied();

            // Only the holder (or an administrator breaking a stale lock) may check in.
            if (document.CheckedOutById != Uid && !Roles.IsFullAccess(Role))
            {
                TempData["Error"] = "Only the person who checked this document out can check it in.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (file != null && file.Length > 0)
            {
                var saved = await _files.SaveAsync(file, "documents", document.ProjectId);
                if (saved == null)
                {
                    TempData["Error"] = _files.LastError ?? "The new version could not be stored.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                // Archive the outgoing file before the pointer moves.
                _db.ProjectDocumentVersions.Add(new ProjectDocumentVersion
                {
                    DocumentId = document.Id,
                    VersionNumber = document.CurrentVersion,
                    FileName = document.FileName,
                    StoredPath = document.StoredPath,
                    ContentType = document.ContentType,
                    SizeBytes = document.SizeBytes,
                    ChangeNote = changeNote,
                    UploadedById = document.UploadedById,
                    UploadedAt = document.UpdatedAt ?? document.CreatedAt
                });

                document.CurrentVersion++;
                document.FileName = saved.OriginalName;
                document.StoredPath = saved.RelativePath;
                document.ContentType = saved.ContentType;
                document.SizeBytes = saved.Size;
                document.UploadedById = Uid;
                // A new revision invalidates the previous approval.
                if (document.Status == ProjectDocumentStatus.Approved)
                {
                    document.Status = ProjectDocumentStatus.Draft;
                    document.ApprovedById = null;
                    document.ApprovedAt = null;
                }

                _activity.Log(document.ProjectId, nameof(ProjectDocument), id, "NewVersion",
                    $"{document.Title} v{document.CurrentVersion}: {changeNote}");
            }

            document.CheckedOutById = null;
            document.CheckedOutAt = null;
            document.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            TempData["Success"] = file != null ? $"Checked in as version {document.CurrentVersion}." : "Check-out released.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> SubmitForApproval(int id)
        {
            var document = await _db.ProjectDocuments.FirstOrDefaultAsync(d => d.Id == id);
            if (document == null) return NotFound();

            var ctx = await LoadContextAsync(document.ProjectId);
            if (ctx == null || !ctx.Value.CanContribute) return AccessDenied();

            var steps = await _approvals.RequestAsync(ApprovalSubject.Document, id, document.Title,
                document.ProjectId, Uid);
            if (steps == 0)
            {
                TempData["Error"] = "No approver could be found for this document.";
                return RedirectToAction(nameof(Details), new { id });
            }

            document.Status = ProjectDocumentStatus.UnderReview;
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Sent for approval — {steps} step(s) raised.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var document = await _db.ProjectDocuments.FirstOrDefaultAsync(d => d.Id == id);
            if (document == null) return NotFound();

            var ctx = await LoadContextAsync(document.ProjectId);
            if (ctx == null || !ctx.Value.CanEdit) return AccessDenied();

            // Soft delete — the file stays on disk so an administrator can recover the record.
            document.IsDeleted = true;
            _activity.Log(document.ProjectId, nameof(ProjectDocument), id, "Deleted", document.Title);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Document deleted.";
            return RedirectToAction(nameof(Index), new { projectId = document.ProjectId });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Meetings
        // ════════════════════════════════════════════════════════════════════════

        public async Task<IActionResult> Meetings(int projectId, ProjectMeetingStatus? status)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanView) return AccessDenied();

            var meetings = await _db.ProjectMeetings.AsNoTracking()
                .Include(m => m.Organiser).Include(m => m.Attendees)
                .Where(m => m.ProjectId == projectId && (status == null || m.Status == status))
                .OrderByDescending(m => m.ScheduledAt).ToListAsync();

            var ids = meetings.Select(m => m.Id).ToList();
            ViewBag.OpenActions = await _db.ProjectMeetingActions.AsNoTracking()
                .Where(a => ids.Contains(a.MeetingId) && !a.IsDone)
                .GroupBy(a => a.MeetingId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            ViewBag.Status = status;
            ViewBag.Upcoming = meetings.Count(m => m.Status == ProjectMeetingStatus.Scheduled && m.ScheduledAt >= DateTime.Now);
            await PopulateUsersAsync();

            return View(meetings);
        }

        [HttpPost]
        public async Task<IActionResult> SaveMeeting(ProjectMeeting input, int[]? attendeeIds)
        {
            var ctx = await LoadContextAsync(input.ProjectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanContribute) return AccessDenied();

            if (string.IsNullOrWhiteSpace(input.Title))
            {
                TempData["Error"] = "A meeting needs a title.";
                return RedirectToAction(nameof(Meetings), new { projectId = input.ProjectId });
            }

            ProjectMeeting meeting;
            if (input.Id == 0)
            {
                input.OrganiserId = Uid;
                input.CreatedAt = DateTime.Now;
                _db.ProjectMeetings.Add(input);
                await _db.SaveChangesAsync();
                meeting = input;
                _activity.Log(input.ProjectId, nameof(ProjectMeeting), meeting.Id, "Created", meeting.Title);
            }
            else
            {
                var existing = await _db.ProjectMeetings.FirstOrDefaultAsync(m => m.Id == input.Id && m.ProjectId == input.ProjectId);
                if (existing == null) return NotFound();

                existing.Title = input.Title;
                existing.Agenda = input.Agenda;
                existing.ScheduledAt = input.ScheduledAt;
                existing.DurationMinutes = input.DurationMinutes;
                existing.Location = input.Location;
                existing.MeetingLink = input.MeetingLink;
                meeting = existing;
            }

            if (attendeeIds != null)
            {
                var current = await _db.ProjectMeetingAttendees.Where(a => a.MeetingId == meeting.Id).ToListAsync();
                foreach (var removed in current.Where(a => !attendeeIds.Contains(a.UserId)))
                    _db.ProjectMeetingAttendees.Remove(removed);

                foreach (var userId in attendeeIds.Distinct().Where(id => current.All(a => a.UserId != id)))
                {
                    _db.ProjectMeetingAttendees.Add(new ProjectMeetingAttendee { MeetingId = meeting.Id, UserId = userId });
                    _activity.Notify(userId, PmNotificationType.StatusChanged,
                        $"Meeting invitation: {meeting.Title}",
                        $"{meeting.ScheduledAt:ddd d MMM yyyy, HH:mm} · {meeting.Location}",
                        Url.Action(nameof(MeetingDetails), new { id = meeting.Id }), meeting.ProjectId);
                }
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "Meeting saved.";
            return RedirectToAction(nameof(MeetingDetails), new { id = meeting.Id });
        }

        public async Task<IActionResult> MeetingDetails(int id)
        {
            var meeting = await _db.ProjectMeetings.AsNoTracking()
                .Include(m => m.Project).Include(m => m.Organiser)
                .Include(m => m.Attendees).ThenInclude(a => a.User)
                .Include(m => m.Actions).ThenInclude(a => a.Owner)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (meeting == null) return NotFound();

            var ctx = await LoadContextAsync(meeting.ProjectId);
            if (ctx == null || !ctx.Value.CanView) return AccessDenied();

            // Offer an extracted summary and candidate actions once minutes exist.
            ViewBag.Extracted = string.IsNullOrWhiteSpace(meeting.Minutes)
                ? null
                : _intelligence.SummariseMinutes(meeting.Minutes);
            ViewBag.IsOrganiser = meeting.OrganiserId == Uid;
            await PopulateUsersAsync();

            return View(meeting);
        }

        [HttpPost]
        public async Task<IActionResult> SaveMinutes(int id, string? minutes, string? decisions, ProjectMeetingStatus status)
        {
            var meeting = await _db.ProjectMeetings.FirstOrDefaultAsync(m => m.Id == id);
            if (meeting == null) return NotFound();

            var ctx = await LoadContextAsync(meeting.ProjectId);
            if (ctx == null || !ctx.Value.CanContribute) return AccessDenied();

            meeting.Minutes = minutes;
            meeting.Decisions = decisions;
            meeting.Status = status;
            _activity.Log(meeting.ProjectId, nameof(ProjectMeeting), id, "MinutesRecorded", meeting.Title);

            // Everyone invited should see the minutes as soon as they are published.
            if (status == ProjectMeetingStatus.Completed)
            {
                var attendees = await _db.ProjectMeetingAttendees.Where(a => a.MeetingId == id).Select(a => a.UserId).ToListAsync();
                _activity.NotifyMany(attendees, PmNotificationType.StatusChanged,
                    $"Minutes published: {meeting.Title}", null,
                    Url.Action(nameof(MeetingDetails), new { id }), meeting.ProjectId);
            }

            await _db.SaveChangesAsync();

            TempData["Success"] = "Minutes saved.";
            return RedirectToAction(nameof(MeetingDetails), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> SetAttendance(int id, int attendeeId, AttendanceState state)
        {
            var meeting = await _db.ProjectMeetings.FirstOrDefaultAsync(m => m.Id == id);
            if (meeting == null) return NotFound();

            var attendee = await _db.ProjectMeetingAttendees.FirstOrDefaultAsync(a => a.Id == attendeeId && a.MeetingId == id);
            if (attendee == null) return NotFound();

            // You mark your own attendance; the organiser marks anyone's.
            var ctx = await LoadContextAsync(meeting.ProjectId);
            if (attendee.UserId != Uid && meeting.OrganiserId != Uid && !Roles.IsFullAccess(Role)) return AccessDenied();
            if (ctx == null) return NotFound();

            attendee.State = state;
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(MeetingDetails), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> AddAction(int id, ProjectMeetingAction input, bool createTask = false)
        {
            var meeting = await _db.ProjectMeetings.FirstOrDefaultAsync(m => m.Id == id);
            if (meeting == null) return NotFound();

            var ctx = await LoadContextAsync(meeting.ProjectId);
            if (ctx == null || !ctx.Value.CanContribute) return AccessDenied();

            if (string.IsNullOrWhiteSpace(input.Description))
            {
                TempData["Error"] = "An action item needs a description.";
                return RedirectToAction(nameof(MeetingDetails), new { id });
            }

            input.MeetingId = id;

            // Promoting an action to a task puts it on the board where it will actually be worked.
            if (createTask)
            {
                var task = new ProjectTask
                {
                    ProjectId = meeting.ProjectId,
                    Name = input.Description.Length > 250 ? input.Description[..250] : input.Description,
                    Description = $"Action from the meeting “{meeting.Title}” on {meeting.ScheduledAt:d MMM yyyy}.",
                    AssignedToId = input.OwnerId,
                    DueDate = input.DueDate,
                    StartDate = DateTime.Today,
                    Status = input.OwnerId.HasValue ? ProjectTaskStatus.Assigned : ProjectTaskStatus.NotStarted,
                    Column = input.OwnerId.HasValue ? KanbanColumn.Ready : KanbanColumn.Backlog,
                    CreatedById = Uid
                };
                _db.ProjectTasks.Add(task);
                await _db.SaveChangesAsync();
                input.LinkedTaskId = task.Id;
            }

            _db.ProjectMeetingActions.Add(input);

            if (input.OwnerId is int owner)
                _activity.Notify(owner, PmNotificationType.TaskAssigned,
                    $"Action from {meeting.Title}", input.Description,
                    Url.Action(nameof(MeetingDetails), new { id }), meeting.ProjectId);

            await _db.SaveChangesAsync();

            TempData["Success"] = createTask ? "Action recorded and added to the task board." : "Action recorded.";
            return RedirectToAction(nameof(MeetingDetails), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleAction(int id, int actionId)
        {
            var meeting = await _db.ProjectMeetings.FirstOrDefaultAsync(m => m.Id == id);
            if (meeting == null) return NotFound();

            var ctx = await LoadContextAsync(meeting.ProjectId);
            if (ctx == null || !ctx.Value.CanContribute) return AccessDenied();

            var action = await _db.ProjectMeetingActions.FirstOrDefaultAsync(a => a.Id == actionId && a.MeetingId == id);
            if (action != null)
            {
                action.IsDone = !action.IsDone;
                action.CompletedAt = action.IsDone ? DateTime.Now : null;
                await _db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(MeetingDetails), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteMeeting(int id)
        {
            var meeting = await _db.ProjectMeetings.FirstOrDefaultAsync(m => m.Id == id);
            if (meeting == null) return NotFound();

            var ctx = await LoadContextAsync(meeting.ProjectId);
            if (ctx == null || (!ctx.Value.CanEdit && meeting.OrganiserId != Uid)) return AccessDenied();

            var projectId = meeting.ProjectId;
            _db.ProjectMeetings.Remove(meeting);
            _activity.Log(projectId, nameof(ProjectMeeting), id, "Deleted", meeting.Title);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Meeting removed.";
            return RedirectToAction(nameof(Meetings), new { projectId });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Discussion feed
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>The project's message board — announcements pinned, then threaded discussion.</summary>
        public async Task<IActionResult> Discussion(int projectId)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanView) return AccessDenied();

            var posts = await _db.ProjectDiscussions.AsNoTracking()
                .Include(d => d.Author)
                .Where(d => d.ProjectId == projectId)
                .OrderByDescending(d => d.CreatedAt)
                .Take(200)
                .ToListAsync();

            ViewBag.Announcements = posts.Where(p => p.IsAnnouncement && p.ParentId == null).ToList();
            ViewBag.Threads = posts.Where(p => !p.IsAnnouncement && p.ParentId == null).ToList();
            ViewBag.Replies = posts.Where(p => p.ParentId != null).GroupBy(p => p.ParentId!.Value)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.CreatedAt).ToList());
            ViewBag.CanAnnounce = ctx.Value.CanEdit;

            return View(posts);
        }

        [HttpPost]
        public async Task<IActionResult> Post(int projectId, ProjectDiscussion input)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();
            if (!ctx.Value.CanView) return AccessDenied();

            if (string.IsNullOrWhiteSpace(input.Body))
            {
                TempData["Error"] = "The message is empty.";
                return RedirectToAction(nameof(Discussion), new { projectId });
            }

            // Announcements push to the whole team, so only project owners may raise one.
            if (input.IsAnnouncement && !ctx.Value.CanEdit) input.IsAnnouncement = false;

            var mentioned = await _activity.ResolveMentionsAsync(input.Body);
            input.ProjectId = projectId;
            input.AuthorId = Uid;
            input.CreatedAt = DateTime.Now;
            input.MentionedUserIds = mentioned.Count > 0 ? string.Join(",", mentioned) : null;
            if (input.ParentId == 0) input.ParentId = null;

            _db.ProjectDiscussions.Add(input);
            await _db.SaveChangesAsync();

            var url = Url.Action(nameof(Discussion), new { projectId });
            var excerpt = input.Body.Length > 200 ? input.Body[..200] : input.Body;

            if (input.IsAnnouncement)
                _activity.NotifyMany(await _activity.ProjectAudienceAsync(projectId), PmNotificationType.StatusChanged,
                    input.Subject ?? "Project announcement", excerpt, url, projectId);
            else
                foreach (var userId in mentioned)
                    _activity.Notify(userId, PmNotificationType.Mention, "You were mentioned in a project discussion",
                        excerpt, url, projectId);

            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Discussion), new { projectId });
        }

        [HttpPost]
        public async Task<IActionResult> DeletePost(int projectId, int postId)
        {
            var ctx = await LoadContextAsync(projectId);
            if (ctx == null) return NotFound();

            var post = await _db.ProjectDiscussions.FirstOrDefaultAsync(d => d.Id == postId && d.ProjectId == projectId);
            if (post == null) return NotFound();

            // Authors delete their own posts; project owners can moderate any of them.
            if (post.AuthorId != Uid && !ctx.Value.CanEdit) return AccessDenied();

            post.IsDeleted = true;
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Discussion), new { projectId });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Helpers
        // ════════════════════════════════════════════════════════════════════════

        private async Task<(Project Project, bool CanView, bool CanEdit, bool CanContribute)?> LoadContextAsync(int projectId)
        {
            var (project, team) = await ProjectsController.LoadAsync(_db, projectId);
            if (project == null) return null;

            var teamIds = team.Select(t => t.UserId).ToList();
            var canView = PmAccess.CanView(project, Uid, Role, teamIds);
            var canEdit = PmAccess.CanEdit(project, Uid, Role);
            var canContribute = PmAccess.CanContribute(project, Uid, Role, teamIds);

            ViewBag.Project = project;
            ViewBag.Team = team;
            ViewBag.CanEdit = canEdit;
            ViewBag.CanContribute = canContribute;
            return (project, canView, canEdit, canContribute);
        }

        private async Task PopulateUsersAsync() =>
            ViewBag.Users = await _db.Users.AsNoTracking()
                .Where(u => u.IsActive).OrderBy(u => u.FirstName)
                .Select(u => new { u.Id, Name = u.FirstName + " " + u.LastName }).ToListAsync();
    }
}
