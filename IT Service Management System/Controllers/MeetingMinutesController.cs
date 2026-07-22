using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Enums;
using IT_Service_Management_System.Filters;
using IT_Service_Management_System.Models;
using IT_Service_Management_System.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>
    /// Weekly meeting minutes: attendance register + action-item tracking that carries forward
    /// across meetings. Admins/SystemsAdmins manage meetings, the roster, attendance and action
    /// items; any signed-in user can view meetings and post progress updates on items assigned
    /// to them (see the [AllowAnyRole] actions).
    /// </summary>
    [RoleAuthorize("Admin", "SystemsAdmin")]
    public class MeetingMinutesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditService _audit;

        public MeetingMinutesController(ApplicationDbContext context, AuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        private int? CurrentUserId => HttpContext.Session.GetInt32("UserId");
        private bool IsManager => HttpContext.Session.GetString("UserRole") is "Admin" or "SystemsAdmin";

        private void LoadUsers() =>
            ViewBag.Users = _context.Users
                .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
                .ToList();

        private void LoadDepartments() =>
            ViewBag.Departments = _context.Departments
                .OrderBy(d => d.Name)
                .ToList();

        // ── LIST (all staff) ───────────────────────────────────────────────────────────
        [AllowAnyRole]
        public async Task<IActionResult> Index(int? departmentId)
        {
            var query = _context.Meetings
                .Include(m => m.Department)
                .Include(m => m.Facilitator)
                .Include(m => m.Attendances)
                .Include(m => m.ActionItems)
                .AsQueryable();

            // null = all; 0 = organization-wide (no department); >0 = that department.
            if (departmentId == 0)
                query = query.Where(m => m.DepartmentId == null);
            else if (departmentId.HasValue)
                query = query.Where(m => m.DepartmentId == departmentId.Value);

            var meetings = await query.OrderByDescending(m => m.Date).ToListAsync();

            ViewBag.TotalMeetings = meetings.Count;
            ViewBag.OpenActionItems = await _context.ActionItems
                .CountAsync(a => a.Status != ActionItemStatus.Done);
            ViewBag.RosterCount = await _context.MeetingRosterMembers.CountAsync();
            ViewBag.SelectedDepartmentId = departmentId;
            LoadDepartments();

            return View(meetings);
        }

        // ── DETAILS (all staff; management controls gated in the view) ───────────────────
        [AllowAnyRole]
        public async Task<IActionResult> Details(int id)
        {
            var meeting = await _context.Meetings
                .Include(m => m.Department)
                .Include(m => m.Facilitator)
                .Include(m => m.MinuteTaker)
                .Include(m => m.Attendances).ThenInclude(a => a.User).ThenInclude(u => u!.Department)
                .Include(m => m.ActionItems).ThenInclude(a => a.AssignedTo)
                .Include(m => m.ActionItems).ThenInclude(a => a.Updates).ThenInclude(u => u.UpdatedBy)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (meeting == null) return NotFound();

            ViewBag.IsManager = IsManager;
            ViewBag.CurrentUserId = CurrentUserId;
            if (IsManager) LoadUsers();

            return View(meeting);
        }

        // ── MY ACTION ITEMS (all staff) ──────────────────────────────────────────────────
        [AllowAnyRole]
        public async Task<IActionResult> MyActionItems()
        {
            var uid = CurrentUserId;
            if (uid == null) return RedirectToAction("Login", "Account");

            var items = await _context.ActionItems
                .Include(a => a.Meeting)
                .Include(a => a.Updates).ThenInclude(u => u.UpdatedBy)
                .Where(a => a.AssignedToId == uid)
                .OrderBy(a => a.Status == ActionItemStatus.Done)   // open first
                .ThenBy(a => a.DueDate ?? DateTime.MaxValue)
                .ToListAsync();

            return View(items);
        }

        // ── POST PROGRESS UPDATE (assignee or manager) ───────────────────────────────────
        [AllowAnyRole]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostUpdate(int actionItemId, string note, ActionItemStatus status, int meetingId = 0)
        {
            var uid = CurrentUserId;
            if (uid == null) return RedirectToAction("Login", "Account");

            var item = await _context.ActionItems.FirstOrDefaultAsync(a => a.Id == actionItemId);
            if (item == null) return NotFound();

            // Ownership guard: only the assignee or a manager may update this item.
            if (!IsManager && item.AssignedToId != uid)
                return RedirectToAction("AccessDenied", "Home");

            if (string.IsNullOrWhiteSpace(note))
            {
                TempData["Error"] = "Please enter a progress note.";
                return meetingId > 0
                    ? RedirectToAction(nameof(Details), new { id = meetingId })
                    : RedirectToAction(nameof(MyActionItems));
            }

            _context.ActionItemUpdates.Add(new ActionItemUpdate
            {
                ActionItemId = item.Id,
                MeetingId = item.MeetingId,
                Note = note.Trim(),
                StatusAtUpdate = status,
                UpdatedById = uid.Value,
                CreatedAt = DateTime.Now
            });

            item.Status = status;
            item.ClosedAt = status == ActionItemStatus.Done ? DateTime.Now : null;

            await _context.SaveChangesAsync();
            await _audit.LogAsync("Action Item Update", "ActionItem", item.Id,
                $"Progress update on '{item.Title}' → {status}");

            TempData["Success"] = "Progress update posted.";
            return meetingId > 0
                ? RedirectToAction(nameof(Details), new { id = meetingId })
                : RedirectToAction(nameof(MyActionItems));
        }

        // ── CREATE MEETING (admin) ───────────────────────────────────────────────────────
        public IActionResult Create(int? departmentId)
        {
            LoadUsers();
            LoadDepartments();
            return View(new Meeting { Date = DateTime.Today, DepartmentId = departmentId, Status = MeetingStatus.Held });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Meeting meeting)
        {
            if (!ModelState.IsValid)
            {
                LoadUsers();
                LoadDepartments();
                return View(meeting);
            }

            meeting.CreatedAt = DateTime.Now;
            meeting.CreatedById = CurrentUserId;

            _context.Meetings.Add(meeting);
            await _context.SaveChangesAsync();

            // Pre-load the roster for this meeting's department (null = org-wide roster) into the
            // attendance register, all Present by default.
            var rosterUserIds = await _context.MeetingRosterMembers
                .Where(r => r.DepartmentId == meeting.DepartmentId)
                .Select(r => r.UserId)
                .ToListAsync();
            foreach (var userId in rosterUserIds)
            {
                _context.MeetingAttendances.Add(new MeetingAttendance
                {
                    MeetingId = meeting.Id,
                    UserId = userId,
                    Status = AttendanceStatus.Present
                });
            }
            await _context.SaveChangesAsync();

            await _audit.LogAsync("Created", "Meeting", meeting.Id,
                $"Meeting created for {meeting.Date:dd MMM yyyy} ({meeting.WeekdayName}), {rosterUserIds.Count} roster attendee(s)");

            TempData["Success"] = rosterUserIds.Count > 0
                ? $"Meeting created. {rosterUserIds.Count} roster member(s) added to attendance."
                : "Meeting created. Add roster members for this department to auto-fill attendance next time.";
            return RedirectToAction(nameof(Details), new { id = meeting.Id });
        }

        // ── EDIT MEETING (admin) ─────────────────────────────────────────────────────────
        public async Task<IActionResult> Edit(int id)
        {
            var meeting = await _context.Meetings.FindAsync(id);
            if (meeting == null) return NotFound();
            LoadUsers();
            LoadDepartments();
            return View(meeting);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Meeting meeting)
        {
            if (!ModelState.IsValid)
            {
                LoadUsers();
                LoadDepartments();
                return View(meeting);
            }

            var existing = await _context.Meetings.FindAsync(meeting.Id);
            if (existing == null) return NotFound();

            existing.Date = meeting.Date;
            existing.DepartmentId = meeting.DepartmentId;
            existing.Title = meeting.Title;
            existing.Venue = meeting.Venue;
            existing.StartTime = meeting.StartTime;
            existing.EndTime = meeting.EndTime;
            existing.FacilitatorId = meeting.FacilitatorId;
            existing.MinuteTakerId = meeting.MinuteTakerId;
            existing.Objective = meeting.Objective;
            existing.Summary = meeting.Summary;
            existing.NextMeetingDate = meeting.NextMeetingDate;
            existing.Status = meeting.Status;

            await _context.SaveChangesAsync();
            await _audit.LogAsync("Updated", "Meeting", existing.Id, $"Meeting {existing.Date:dd MMM yyyy} updated");

            TempData["Success"] = "Meeting updated.";
            return RedirectToAction(nameof(Details), new { id = existing.Id });
        }

        // ── DELETE MEETING (admin) ───────────────────────────────────────────────────────
        public async Task<IActionResult> Delete(int id)
        {
            var meeting = await _context.Meetings
                .Include(m => m.ActionItems)
                .Include(m => m.Attendances)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (meeting == null) return NotFound();
            return View(meeting);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var meeting = await _context.Meetings
                .Include(m => m.ActionItems)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (meeting == null) return NotFound();

            // Action items are Restrict-linked (tracked work shouldn't vanish with a meeting).
            if (meeting.ActionItems.Any())
            {
                TempData["Error"] = "This meeting has action items. Reassign or close them before deleting the meeting.";
                return RedirectToAction(nameof(Details), new { id });
            }

            _context.Meetings.Remove(meeting);   // attendance rows cascade
            await _context.SaveChangesAsync();
            await _audit.LogAsync("Deleted", "Meeting", id, $"Meeting {meeting.Date:dd MMM yyyy} deleted");

            TempData["Success"] = "Meeting deleted.";
            return RedirectToAction(nameof(Index));
        }

        // ── ATTENDANCE (admin) ───────────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordAttendance(int meetingId, int[] attendanceId, AttendanceStatus[] status, string[] note)
        {
            var rows = await _context.MeetingAttendances
                .Where(a => a.MeetingId == meetingId)
                .ToListAsync();

            for (int i = 0; i < attendanceId.Length; i++)
            {
                var row = rows.FirstOrDefault(r => r.Id == attendanceId[i]);
                if (row == null) continue;
                if (i < status.Length) row.Status = status[i];
                if (note != null && i < note.Length) row.Note = string.IsNullOrWhiteSpace(note[i]) ? null : note[i].Trim();
            }

            await _context.SaveChangesAsync();
            await _audit.LogAsync("Attendance Recorded", "Meeting", meetingId, $"Attendance updated for {rows.Count} attendee(s)");

            TempData["Success"] = "Attendance saved.";
            return RedirectToAction(nameof(Details), new { id = meetingId });
        }

        // Add an ad-hoc attendee (guest) not on the standing roster.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAttendee(int meetingId, int userId)
        {
            bool exists = await _context.MeetingAttendances.AnyAsync(a => a.MeetingId == meetingId && a.UserId == userId);
            if (!exists)
            {
                _context.MeetingAttendances.Add(new MeetingAttendance
                {
                    MeetingId = meetingId,
                    UserId = userId,
                    Status = AttendanceStatus.Present
                });
                await _context.SaveChangesAsync();
                TempData["Success"] = "Attendee added.";
            }
            else
            {
                TempData["Error"] = "That person is already in the attendance list.";
            }
            return RedirectToAction(nameof(Details), new { id = meetingId });
        }

        // ── ACTION ITEMS (admin creates/edits; assignees update via PostUpdate) ──────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateActionItem(int meetingId, string title, string? details,
            int? assignedToId, string? assigneeLabel, DateTime? dueDate, ActionItemPriority priority)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                TempData["Error"] = "An action item needs a title.";
                return RedirectToAction(nameof(Details), new { id = meetingId });
            }

            var item = new ActionItem
            {
                MeetingId = meetingId,
                Title = title.Trim(),
                Details = string.IsNullOrWhiteSpace(details) ? null : details.Trim(),
                AssignedToId = assignedToId,
                AssigneeLabel = assignedToId.HasValue || string.IsNullOrWhiteSpace(assigneeLabel) ? null : assigneeLabel.Trim(),
                DueDate = dueDate,
                Priority = priority,
                Status = ActionItemStatus.Open,
                CreatedAt = DateTime.Now,
                CreatedById = CurrentUserId
            };
            _context.ActionItems.Add(item);
            await _context.SaveChangesAsync();
            await _audit.LogAsync("Action Item Created", "ActionItem", item.Id, $"'{item.Title}' raised in meeting {meetingId}");

            TempData["Success"] = "Action item added.";
            return RedirectToAction(nameof(Details), new { id = meetingId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditActionItem(int id, string title, string? details,
            int? assignedToId, string? assigneeLabel, DateTime? dueDate, ActionItemPriority priority, ActionItemStatus status)
        {
            var item = await _context.ActionItems.FindAsync(id);
            if (item == null) return NotFound();

            item.Title = string.IsNullOrWhiteSpace(title) ? item.Title : title.Trim();
            item.Details = string.IsNullOrWhiteSpace(details) ? null : details.Trim();
            item.AssignedToId = assignedToId;
            item.AssigneeLabel = assignedToId.HasValue || string.IsNullOrWhiteSpace(assigneeLabel) ? null : assigneeLabel.Trim();
            item.DueDate = dueDate;
            item.Priority = priority;
            item.Status = status;
            item.ClosedAt = status == ActionItemStatus.Done ? (item.ClosedAt ?? DateTime.Now) : null;

            await _context.SaveChangesAsync();
            await _audit.LogAsync("Action Item Updated", "ActionItem", item.Id, $"'{item.Title}' edited");

            TempData["Success"] = "Action item updated.";
            return RedirectToAction(nameof(Details), new { id = item.MeetingId });
        }

        // ── STANDING ROSTERS — one per department, plus an org-wide roster (admin) ─────────
        public async Task<IActionResult> Roster(int? departmentId)
        {
            var roster = await _context.MeetingRosterMembers
                .Include(r => r.User)
                .Include(r => r.Department)
                .OrderBy(r => r.User!.FirstName).ThenBy(r => r.User!.LastName)
                .ToListAsync();

            LoadUsers();          // all users for the add-member picker
            LoadDepartments();    // department picker (+ an "Organization-wide" option in the view)
            ViewBag.SelectedDepartmentId = departmentId;

            return View(roster);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddRosterMember(int userId, int? departmentId)
        {
            bool exists = await _context.MeetingRosterMembers
                .AnyAsync(r => r.UserId == userId && r.DepartmentId == departmentId);
            if (!exists)
            {
                _context.MeetingRosterMembers.Add(new MeetingRosterMember { UserId = userId, DepartmentId = departmentId });
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Roster Member Added", "MeetingRoster", userId,
                    $"Added to {(departmentId.HasValue ? "department" : "organization-wide")} meeting roster");
                TempData["Success"] = "Roster member added.";
            }
            else
            {
                TempData["Error"] = "That person is already on this roster.";
            }
            return RedirectToAction(nameof(Roster), new { departmentId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveRosterMember(int id)
        {
            var member = await _context.MeetingRosterMembers.FindAsync(id);
            if (member != null)
            {
                _context.MeetingRosterMembers.Remove(member);
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Roster Member Removed", "MeetingRoster", member.UserId, "Removed from standing meeting roster");
                TempData["Success"] = "Roster member removed.";
            }
            return RedirectToAction(nameof(Roster));
        }
    }
}
