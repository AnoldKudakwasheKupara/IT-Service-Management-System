using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Filters;
using IT_Service_Management_System.Helpers.Ims;
using IT_Service_Management_System.Models.Ims;
using IT_Service_Management_System.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>
    /// Training &amp; Competency (ISO 9001/27001 cl. 7.2) — the training register (courses, attendance and
    /// completion records with certificate &amp; expiry tracking) and the competency matrix (skills assessed
    /// against employees, level vs. required level, and revalidation).
    /// </summary>
    [IT_Service_Management_System.Filters.RoleAuthorize("Admin", "SystemsAdmin", "QualityManager", "DocumentController", "DepartmentManager", "Auditor", "ExternalAuditor")]
    public class TrainingController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditService _audit;

        public TrainingController(ApplicationDbContext db, AuditService audit)
        {
            _db = db;
            _audit = audit;
        }

        private int? Uid => HttpContext.Session.GetInt32("UserId");
        private string? Role => HttpContext.Session.GetString("UserRole");
        private bool Can(ImsPermission p) => ImsAccess.Can(Role, p);

        private IActionResult Denied() => RedirectToAction("AccessDenied", "Home");

        private void LoadLookups()
        {
            ViewBag.Users = _db.Users.Where(u => u.IsActive).OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToList();
            ViewBag.Documents = _db.IsoDocuments.OrderBy(d => d.Title).ToList();
        }

        // ── LIST ─────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Index(TrainingType? type, string? q)
        {
            var query = _db.TrainingCourses
                .Include(c => c.LinkedDocument)
                .Include(c => c.Records)
                .AsQueryable();

            if (type.HasValue) query = query.Where(c => c.Type == type.Value);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(c => c.Title.Contains(term)
                    || (c.Provider != null && c.Provider.Contains(term))
                    || (c.Description != null && c.Description.Contains(term)));
            }

            var list = await query.OrderByDescending(c => c.CreatedAt).ToListAsync();

            var allRecords = await _db.TrainingRecords.ToListAsync();
            var totalRecords = allRecords.Count;
            var completed = allRecords.Count(r => r.Status == AttendanceStatus.Completed);

            ViewBag.Type = type;
            ViewBag.Query = q;
            ViewBag.CourseCount = await _db.TrainingCourses.CountAsync();
            ViewBag.RecordCount = totalRecords;
            ViewBag.CompletionPct = totalRecords == 0 ? 0 : (int)Math.Round(completed * 100.0 / totalRecords);
            ViewBag.ExpiringCerts = allRecords.Count(r => r.IsCertificateExpired || r.IsCertificateExpiringSoon);
            ViewBag.CanManage = Can(ImsPermission.ManageTraining);

            return View(list);
        }

        // ── DETAILS ────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Details(int id)
        {
            var course = await _db.TrainingCourses
                .Include(c => c.LinkedDocument)
                .Include(c => c.CreatedBy)
                .Include(c => c.Records).ThenInclude(r => r.User)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (course == null) return NotFound();

            ViewBag.CanManage = Can(ImsPermission.ManageTraining);
            LoadLookups();
            return View(course);
        }

        // ── CREATE ───────────────────────────────────────────────────────────────
        public IActionResult Create()
        {
            if (!Can(ImsPermission.ManageTraining)) return Denied();
            LoadLookups();
            return View(new TrainingCourse());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TrainingCourse model)
        {
            if (!Can(ImsPermission.ManageTraining)) return Denied();

            if (!ModelState.IsValid) { LoadLookups(); return View(model); }

            model.CreatedById = Uid;
            model.CreatedAt = DateTime.Now;
            _db.TrainingCourses.Add(model);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Created", "TrainingCourse", model.Id, $"{model.Reference} — {model.Title}");
            TempData["Success"] = $"Training course {model.Reference} created.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        // ── EDIT ───────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Edit(int id)
        {
            if (!Can(ImsPermission.ManageTraining)) return Denied();
            var course = await _db.TrainingCourses.FindAsync(id);
            if (course == null) return NotFound();
            LoadLookups();
            return View(course);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TrainingCourse model)
        {
            if (!Can(ImsPermission.ManageTraining)) return Denied();
            var course = await _db.TrainingCourses.FindAsync(id);
            if (course == null) return NotFound();

            if (!ModelState.IsValid) { LoadLookups(); return View(model); }

            course.Title = model.Title;
            course.Type = model.Type;
            course.Standard = model.Standard;
            course.Description = model.Description;
            course.Provider = model.Provider;
            course.DurationHours = model.DurationHours;
            course.LinkedDocumentId = model.LinkedDocumentId;
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Updated", "TrainingCourse", course.Id, $"{course.Reference} — {course.Title}");
            TempData["Success"] = "Training course updated.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── DELETE ───────────────────────────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!Can(ImsPermission.ManageTraining)) return Denied();
            var course = await _db.TrainingCourses.Include(c => c.Records).FirstOrDefaultAsync(c => c.Id == id);
            if (course == null) return NotFound();
            _db.TrainingRecords.RemoveRange(course.Records);
            _db.TrainingCourses.Remove(course);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Deleted", "TrainingCourse", id, $"{course.Reference} — {course.Title}");
            TempData["Success"] = "Training course deleted.";
            return RedirectToAction(nameof(Index));
        }

        // ── ATTENDANCE / COMPLETION RECORDS ───────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddRecord(int id, TrainingRecord record)
        {
            if (!Can(ImsPermission.ManageTraining)) return Denied();
            var course = await _db.TrainingCourses.FindAsync(id);
            if (course == null) return NotFound();

            if (record.UserId == 0)
            {
                TempData["Error"] = "Please select an employee to record.";
                return RedirectToAction(nameof(Details), new { id });
            }

            record.TrainingCourseId = id;
            record.CreatedAt = DateTime.Now;
            _db.TrainingRecords.Add(record);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("AddRecord", "TrainingCourse", id, $"{course.Reference}: enrolled user {record.UserId} ({record.Status})");
            TempData["Success"] = "Attendance record added.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRecord(int id, int recordId)
        {
            if (!Can(ImsPermission.ManageTraining)) return Denied();
            var record = await _db.TrainingRecords.FirstOrDefaultAsync(r => r.Id == recordId && r.TrainingCourseId == id);
            if (record == null) return NotFound();
            _db.TrainingRecords.Remove(record);
            await _db.SaveChangesAsync();
            await _audit.LogAsync("DeleteRecord", "TrainingCourse", id, $"Record {recordId} removed.");
            TempData["Success"] = "Attendance record removed.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── COMPETENCY MATRIX ─────────────────────────────────────────────────────
        public async Task<IActionResult> Competency(string? category)
        {
            var query = _db.Competencies
                .Include(c => c.Assessments).ThenInclude(a => a.User)
                .Include(c => c.Assessments).ThenInclude(a => a.AssessedBy)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(c => c.Category == category);

            var list = await query.OrderBy(c => c.Category).ThenBy(c => c.Name).ToListAsync();

            var assessments = list.SelectMany(c => c.Assessments).ToList();
            ViewBag.CompetencyCount = list.Count;
            ViewBag.AssessmentCount = assessments.Count;
            ViewBag.MeetingCount = assessments.Count(a => a.MeetsRequirement);
            ViewBag.GapCount = assessments.Count(a => !a.MeetsRequirement);
            ViewBag.Categories = await _db.Competencies
                .Where(c => c.Category != null && c.Category != "")
                .Select(c => c.Category!).Distinct().OrderBy(c => c).ToListAsync();
            ViewBag.Category = category;
            ViewBag.CanManage = Can(ImsPermission.ManageTraining);
            LoadLookups();

            return View(list);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCompetency(Competency c)
        {
            if (!Can(ImsPermission.ManageTraining)) return Denied();

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "A competency name is required.";
                return RedirectToAction(nameof(Competency));
            }

            _db.Competencies.Add(c);
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Created", "Competency", c.Id, c.Name);
            TempData["Success"] = $"Competency \"{c.Name}\" added.";
            return RedirectToAction(nameof(Competency));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AssessUser(UserCompetency uc)
        {
            if (!Can(ImsPermission.ManageTraining)) return Denied();

            var competency = await _db.Competencies.FindAsync(uc.CompetencyId);
            if (competency == null) return NotFound();

            if (uc.UserId == 0)
            {
                TempData["Error"] = "Please select an employee to assess.";
                return RedirectToAction(nameof(Competency));
            }

            var existing = await _db.UserCompetencies
                .FirstOrDefaultAsync(x => x.CompetencyId == uc.CompetencyId && x.UserId == uc.UserId);

            if (existing != null)
            {
                existing.Level = uc.Level;
                existing.RequiredLevel = uc.RequiredLevel;
                existing.AssessedDate = uc.AssessedDate ?? DateTime.Now;
                existing.AssessedById = Uid;
                existing.ExpiryDate = uc.ExpiryDate;
                existing.Notes = uc.Notes;
            }
            else
            {
                uc.AssessedDate = uc.AssessedDate ?? DateTime.Now;
                uc.AssessedById = Uid;
                uc.CreatedAt = DateTime.Now;
                _db.UserCompetencies.Add(uc);
            }
            await _db.SaveChangesAsync();

            await _audit.LogAsync("Assessed", "Competency", uc.CompetencyId, $"{competency.Name}: user {uc.UserId} → {uc.Level}");
            TempData["Success"] = "Competency assessment recorded.";
            return RedirectToAction(nameof(Competency));
        }
    }
}
