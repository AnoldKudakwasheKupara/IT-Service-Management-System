using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Helpers.Efm;
using IT_Service_Management_System.Models.Efm;
using IT_Service_Management_System.Services.Efm;
using IT_Service_Management_System.ViewModels.Efm;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>Employee File Management — digital personnel files, upload, browse, preview, download.</summary>
    [IT_Service_Management_System.Filters.RoleAuthorize("Admin", "SystemsAdmin", "HR")]
    public class EmployeeDocumentsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly DocumentService _docs;
        private readonly DocumentMaintenanceService _maint;

        private const long MaxFileBytes = 50L * 1024 * 1024; // 50 MB/file
        private static readonly string[] BlockedExtensions =
            { ".exe", ".dll", ".bat", ".cmd", ".com", ".scr", ".msi", ".ps1", ".sh", ".vbs", ".jar" };

        public EmployeeDocumentsController(ApplicationDbContext db, DocumentService docs, DocumentMaintenanceService maint)
        {
            _db = db;
            _docs = docs;
            _maint = maint;
        }

        private string? Role => HttpContext.Session.GetString("UserRole");
        private int? Uid => HttpContext.Session.GetInt32("UserId");

        // Can the current user open (view/preview/download) this document?
        // Staff see per confidentiality; everyone else only their own documents (self-service).
        private bool CanOpen(EmployeeDocument doc)
        {
            if (EfmAccess.IsFullAccess(Role)) return true;
            if (EfmAccess.IsStaff(Role)) return EfmAccess.CanSeeConfidentiality(Role, doc.ConfidentialityLevel);
            return doc.EmployeeId == Uid;
        }

        private IActionResult Denied() => RedirectToAction("AccessDenied", "Home");

        // ── employee picker ────────────────────────────────────────────────────────────
        public async Task<IActionResult> Index(string? q)
        {
            IQueryable<Models.User> query = _db.Users.Include(u => u.Department);
            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(u => u.FirstName.Contains(q) || u.LastName.Contains(q) || u.Email.Contains(q));

            var employees = await query.OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToListAsync();

            ViewBag.DocCounts = await _db.EmployeeDocuments
                .GroupBy(d => d.EmployeeId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);
            ViewBag.Search = q;
            return View(employees);
        }

        // ── cross-employee document search ─────────────────────────────────────────────
        public async Task<IActionResult> Search(string? q, int? folderId, int? categoryId,
            DocumentStatus? status, string? expiry, DateTime? from, DateTime? to, int page = 1)
        {
            IQueryable<EmployeeDocument> query = _db.EmployeeDocuments
                .Include(d => d.Employee).ThenInclude(u => u!.Department)
                .Include(d => d.Category)
                .Include(d => d.Folder)
                .Include(d => d.CurrentVersion)
                .Include(d => d.Tags).ThenInclude(t => t.Tag)
                .Where(d => !d.IsArchived);

            // HR officers / auditors cannot see Restricted documents in search.
            if (!EfmAccess.IsFullAccess(Role))
                query = query.Where(d => d.ConfidentialityLevel < ConfidentialityLevel.Restricted);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(d =>
                    d.Title.Contains(term)
                    || (d.DocumentNumber != null && d.DocumentNumber.Contains(term))
                    || (d.Keywords != null && d.Keywords.Contains(term))
                    || d.Employee!.FirstName.Contains(term)
                    || d.Employee!.LastName.Contains(term)
                    || d.Employee!.Email.Contains(term)
                    || (d.Employee!.Department != null && d.Employee.Department.Name.Contains(term))
                    || d.Category!.Name.Contains(term)
                    || d.Tags.Any(t => t.Tag!.Name.Contains(term))
                    || (d.CurrentVersion != null && d.CurrentVersion.OcrText != null && d.CurrentVersion.OcrText.Contains(term)));
            }

            if (folderId.HasValue) query = query.Where(d => d.FolderId == folderId.Value);
            if (categoryId.HasValue) query = query.Where(d => d.CategoryId == categoryId.Value);
            if (status.HasValue) query = query.Where(d => d.Status == status.Value);

            var today = DateTime.Today;
            if (expiry == "expired") query = query.Where(d => d.ExpiryDate != null && d.ExpiryDate < today);
            else if (expiry == "expiring") query = query.Where(d => d.ExpiryDate != null && d.ExpiryDate >= today && d.ExpiryDate <= today.AddDays(30));

            if (from.HasValue) query = query.Where(d => d.CreatedAt >= from.Value);
            if (to.HasValue) query = query.Where(d => d.CreatedAt <= to.Value.AddDays(1));

            query = query.OrderByDescending(d => d.CreatedAt);
            var (items, paging) = await query.PageAsync(page, 20);
            ViewBag.Paging = paging;

            return View(new DocumentSearchVm
            {
                Results = items,
                Folders = await _db.DocumentFolders.Where(f => f.IsActive).OrderBy(f => f.SortOrder).ToListAsync(),
                Categories = await _db.DocumentCategories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(),
                Q = q, FolderId = folderId, CategoryId = categoryId, Status = status, Expiry = expiry, From = from, To = to
            });
        }

        // ── audit trail ────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Audit(string? q, DocumentAuditAction? action, int? employeeId,
            int? documentId, DateTime? from, DateTime? to, int page = 1)
        {
            var (logs, paging) = await BuildAuditQuery(q, action, employeeId, documentId, from, to)
                .OrderByDescending(a => a.Timestamp).PageAsync(page, 30);
            ViewBag.Paging = paging;

            var vm = new DocumentAuditVm
            {
                Rows = await ResolveRowsAsync(logs),
                Q = q, Action = action, EmployeeId = employeeId, DocumentId = documentId, From = from, To = to
            };
            if (employeeId.HasValue)
                vm.EmployeeName = (await _db.Users.FindAsync(employeeId.Value))?.FullName;
            if (documentId.HasValue)
                vm.DocumentTitle = await _db.EmployeeDocuments.IgnoreQueryFilters()
                    .Where(d => d.Id == documentId.Value).Select(d => d.Title).FirstOrDefaultAsync();
            return View(vm);
        }

        public async Task<IActionResult> AuditExport(string? q, DocumentAuditAction? action, int? employeeId,
            int? documentId, DateTime? from, DateTime? to)
        {
            var logs = await BuildAuditQuery(q, action, employeeId, documentId, from, to)
                .OrderByDescending(a => a.Timestamp).Take(10000).ToListAsync();
            var rows = await ResolveRowsAsync(logs);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Timestamp,Action,PerformedBy,Employee,Document,IPAddress,UserAgent,Details");
            foreach (var r in rows)
                sb.AppendLine(string.Join(",", new[] {
                    r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"), r.Action.ToString(), r.PerformedByName,
                    r.EmployeeName, r.DocumentTitle, r.IpAddress, r.UserAgent, r.Details
                }.Select(Csv)));

            return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv",
                $"document-audit-{DateTime.Now:yyyyMMdd-HHmm}.csv");
        }

        private IQueryable<DocumentAuditLog> BuildAuditQuery(string? q, DocumentAuditAction? action,
            int? employeeId, int? documentId, DateTime? from, DateTime? to)
        {
            var query = _db.DocumentAuditLogs.AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(a =>
                    (a.PerformedByName != null && a.PerformedByName.Contains(term))
                    || (a.Details != null && a.Details.Contains(term))
                    || (a.IpAddress != null && a.IpAddress.Contains(term)));
            }
            if (action.HasValue) query = query.Where(a => a.Action == action.Value);
            if (employeeId.HasValue) query = query.Where(a => a.EmployeeId == employeeId.Value);
            if (documentId.HasValue) query = query.Where(a => a.EmployeeDocumentId == documentId.Value);
            if (from.HasValue) query = query.Where(a => a.Timestamp >= from.Value);
            if (to.HasValue) query = query.Where(a => a.Timestamp <= to.Value.AddDays(1));
            return query;
        }

        private async Task<List<DocumentAuditRow>> ResolveRowsAsync(List<DocumentAuditLog> logs)
        {
            var empIds = logs.Where(l => l.EmployeeId != null).Select(l => l.EmployeeId!.Value).Distinct().ToList();
            var emps = await _db.Users.Where(u => empIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FirstName + " " + u.LastName);
            var docIds = logs.Where(l => l.EmployeeDocumentId != null).Select(l => l.EmployeeDocumentId!.Value).Distinct().ToList();
            var docs = await _db.EmployeeDocuments.IgnoreQueryFilters().Where(d => docIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, d => d.Title);

            return logs.Select(l => new DocumentAuditRow
            {
                Id = l.Id,
                Timestamp = l.Timestamp,
                Action = l.Action,
                PerformedByName = l.PerformedByName,
                IpAddress = l.IpAddress,
                UserAgent = l.UserAgent,
                EmployeeId = l.EmployeeId,
                EmployeeName = l.EmployeeId != null && emps.TryGetValue(l.EmployeeId.Value, out var en) ? en : null,
                EmployeeDocumentId = l.EmployeeDocumentId,
                DocumentTitle = l.EmployeeDocumentId != null && docs.TryGetValue(l.EmployeeDocumentId.Value, out var dt) ? dt : null,
                Details = l.Details
            }).ToList();
        }

        private static string Csv(string? v)
        {
            v ??= "";
            return v.Contains(',') || v.Contains('"') || v.Contains('\n')
                ? "\"" + v.Replace("\"", "\"\"") + "\""
                : v;
        }

        // ── notifications + maintenance ────────────────────────────────────────────────
        public async Task<IActionResult> Notifications(int page = 1)
        {
            var query = _db.DocumentNotifications
                .OrderByDescending(n => !n.IsRead).ThenByDescending(n => n.CreatedAt);
            var (items, paging) = await query.PageAsync(page, 30);
            ViewBag.Paging = paging;
            ViewBag.UnreadCount = await _db.DocumentNotifications.CountAsync(n => !n.IsRead);

            var empIds = items.Where(n => n.EmployeeId != null).Select(n => n.EmployeeId!.Value).Distinct().ToList();
            ViewBag.Employees = await _db.Users.Where(u => empIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FirstName + " " + u.LastName);
            return View(items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkNotificationsRead()
        {
            var unread = await _db.DocumentNotifications.Where(n => !n.IsRead).ToListAsync();
            foreach (var n in unread) { n.IsRead = true; n.ReadAt = DateTime.Now; }
            await _db.SaveChangesAsync();
            TempData["Success"] = $"{unread.Count} notification(s) marked as read.";
            return RedirectToAction(nameof(Notifications));
        }

        // Manually trigger the expiry/retention scans (also runs automatically every 6 hours).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunMaintenance()
        {
            await _maint.SeedRequiredDocumentsAsync();
            var (alerts, expired) = await _maint.RunExpiryScanAsync();
            var (archived, deleted, flagged) = await _maint.RunRetentionScanAsync();
            TempData["Success"] =
                $"Maintenance complete — {alerts} expiry alert(s), {expired} marked expired, " +
                $"{archived} archived, {deleted} deleted, {flagged} flagged.";
            return RedirectToAction(nameof(Notifications));
        }

        // ── digital file browser ───────────────────────────────────────────────────────
        public async Task<IActionResult> File(int id, int? folderId)
        {
            var employee = await _db.Users.Include(u => u.Department).FirstOrDefaultAsync(u => u.Id == id);
            if (employee == null) return NotFound();

            var folders = await _db.DocumentFolders.Where(f => f.IsActive)
                .OrderBy(f => f.SortOrder).ToListAsync();

            // Non-admin staff (HR officers) cannot see Restricted documents.
            bool restrictConf = !EfmAccess.IsFullAccess(Role);

            var countsQuery = _db.EmployeeDocuments.Where(d => d.EmployeeId == id && !d.IsArchived);
            if (restrictConf) countsQuery = countsQuery.Where(d => d.ConfidentialityLevel < ConfidentialityLevel.Restricted);
            var counts = await countsQuery
                .GroupBy(d => d.FolderId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            var docsQuery = _db.EmployeeDocuments
                .Include(d => d.Category)
                .Include(d => d.CurrentVersion)
                .Where(d => d.EmployeeId == id && !d.IsArchived);
            if (restrictConf) docsQuery = docsQuery.Where(d => d.ConfidentialityLevel < ConfidentialityLevel.Restricted);
            if (folderId.HasValue)
                docsQuery = docsQuery.Where(d => d.FolderId == folderId.Value);

            var documents = await docsQuery.OrderByDescending(d => d.CreatedAt).ToListAsync();

            var vm = new EmployeeFileBrowserVm
            {
                Employee = employee,
                Folders = folders,
                FolderCounts = counts,
                SelectedFolderId = folderId,
                Documents = documents,
                Categories = await _db.DocumentCategories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(),
                TotalDocuments = counts.Values.Sum()
            };
            ViewBag.Completeness = await _maint.GetCompletenessAsync(id);
            return View(vm);
        }

        // ── upload (drag & drop / bulk) ────────────────────────────────────────────────
        [HttpPost]
        [RequestSizeLimit(long.MaxValue)]
        public async Task<IActionResult> Upload(DocumentUploadInput input, List<IFormFile> files)
        {
            if (files == null || files.Count == 0 || files.All(f => f.Length == 0))
                return BadRequest(new { success = false, message = "No files were provided." });

            if (!await _db.Users.AnyAsync(u => u.Id == input.EmployeeId))
                return NotFound(new { success = false, message = "Employee not found." });
            if (!await _db.DocumentFolders.AnyAsync(f => f.Id == input.FolderId))
                return BadRequest(new { success = false, message = "Invalid folder." });
            if (!await _db.DocumentCategories.AnyAsync(c => c.Id == input.CategoryId))
                return BadRequest(new { success = false, message = "Invalid category." });

            var userId = HttpContext.Session.GetInt32("UserId");
            var userName = HttpContext.Session.GetString("UserName");
            int saved = 0;
            var skipped = new List<string>();

            foreach (var file in files)
            {
                if (file.Length == 0) continue;
                if (file.Length > MaxFileBytes) { skipped.Add($"{file.FileName} (too large)"); continue; }
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (BlockedExtensions.Contains(ext)) { skipped.Add($"{file.FileName} (blocked type)"); continue; }

                // For multi-file uploads let each file keep its own title (from the file name).
                var perFile = new DocumentUploadInput
                {
                    EmployeeId = input.EmployeeId,
                    FolderId = input.FolderId,
                    CategoryId = input.CategoryId,
                    Title = files.Count == 1 ? input.Title : null,
                    Description = input.Description,
                    DocumentNumber = files.Count == 1 ? input.DocumentNumber : null,
                    IssueDate = input.IssueDate,
                    ExpiryDate = input.ExpiryDate,
                    Confidentiality = input.Confidentiality,
                    TagsCsv = input.TagsCsv,
                    Keywords = input.Keywords
                };
                await _docs.CreateFromUploadAsync(perFile, file, userId, userName);
                saved++;
            }

            var msg = $"{saved} document(s) uploaded." + (skipped.Count > 0 ? $" Skipped: {string.Join(", ", skipped)}" : "");
            TempData["Success"] = msg;
            return Ok(new { success = true, count = saved, skipped, message = msg });
        }

        // ── document detail ────────────────────────────────────────────────────────────
        [IT_Service_Management_System.Filters.AllowAnyRole]
        public async Task<IActionResult> Details(int id)
        {
            var doc = await _db.EmployeeDocuments
                .Include(d => d.Employee)
                .Include(d => d.Folder)
                .Include(d => d.Category)
                .Include(d => d.CurrentVersion)
                .Include(d => d.Versions)
                .Include(d => d.Tags).ThenInclude(t => t.Tag)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (doc == null) return NotFound();
            if (!CanOpen(doc)) return Denied();

            ViewBag.IsStaff = EfmAccess.IsStaff(Role);
            doc.ViewCount++;
            await _db.SaveChangesAsync();
            await _docs.LogAsync(DocumentAuditAction.Viewed, doc.Id, doc.EmployeeId, $"Viewed '{doc.Title}'");
            return View(doc);
        }

        // Inline preview (browser renders PDF/image).
        [IT_Service_Management_System.Filters.AllowAnyRole]
        public async Task<IActionResult> Preview(int id)
        {
            var doc = await _db.EmployeeDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
            if (doc == null) return NotFound();
            if (!CanOpen(doc)) return Denied();

            var result = await _docs.OpenCurrentAsync(id);
            if (result == null) return NotFound();
            await _docs.LogAsync(DocumentAuditAction.Previewed, id, doc.EmployeeId, "Previewed document");
            Response.Headers.ContentDisposition = "inline";
            return File(result.Value.Stream, result.Value.Version.ContentType);
        }

        // Force download (attachment).
        [IT_Service_Management_System.Filters.AllowAnyRole]
        public async Task<IActionResult> Download(int id)
        {
            var doc = await _db.EmployeeDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
            if (doc == null) return NotFound();
            if (!CanOpen(doc)) return Denied();

            var result = await _docs.OpenCurrentAsync(id);
            if (result == null) return NotFound();
            await _docs.LogAsync(DocumentAuditAction.Downloaded, id, doc.EmployeeId, $"Downloaded '{result.Value.Version.FileName}'");
            return File(result.Value.Stream, result.Value.Version.ContentType, result.Value.Version.FileName);
        }

        // ── version control ────────────────────────────────────────────────────────────
        // Uploads a NEW version of an existing document (never overwrites).
        [HttpPost]
        [RequestSizeLimit(long.MaxValue)]
        public async Task<IActionResult> UploadVersion(int id, IFormFile? file, string? changeNote)
        {
            var doc = await _db.EmployeeDocuments.FirstOrDefaultAsync(d => d.Id == id);
            if (doc == null) return NotFound();

            if (file == null || file.Length == 0)
            { TempData["Error"] = "Please choose a file."; return RedirectToAction(nameof(Details), new { id }); }
            if (file.Length > MaxFileBytes)
            { TempData["Error"] = "File exceeds the 50 MB limit."; return RedirectToAction(nameof(Details), new { id }); }
            if (BlockedExtensions.Contains(Path.GetExtension(file.FileName).ToLowerInvariant()))
            { TempData["Error"] = "That file type is not allowed."; return RedirectToAction(nameof(Details), new { id }); }

            var v = await _docs.AddVersionAsync(id, file, changeNote,
                HttpContext.Session.GetInt32("UserId"), HttpContext.Session.GetString("UserName"));
            TempData["Success"] = $"Version {v.VersionNumber} uploaded and set as current.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Downloads a specific historical version.
        public async Task<IActionResult> DownloadVersion(int versionId)
        {
            var result = await _docs.OpenVersionAsync(versionId);
            if (result == null) return NotFound();
            await _docs.LogAsync(DocumentAuditAction.Downloaded, result.Value.Version.EmployeeDocumentId, null,
                $"Downloaded v{result.Value.Version.VersionNumber} ({result.Value.Version.FileName})");
            return File(result.Value.Stream, result.Value.Version.ContentType, result.Value.Version.FileName);
        }

        // Restores an older version by promoting it to a new current version.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreVersion(int documentId, int versionId)
        {
            var restored = await _docs.RestoreVersionAsync(documentId, versionId,
                HttpContext.Session.GetInt32("UserId"), HttpContext.Session.GetString("UserName"));
            if (restored == null) return NotFound();
            TempData["Success"] = $"Restored as version {restored.VersionNumber} (now current).";
            return RedirectToAction(nameof(Details), new { id = documentId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var doc = await _db.EmployeeDocuments.FirstOrDefaultAsync(d => d.Id == id);
            if (doc == null) return NotFound();

            doc.IsDeleted = true;
            doc.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            await _docs.LogAsync(DocumentAuditAction.Deleted, doc.Id, doc.EmployeeId, $"Deleted '{doc.Title}'");

            TempData["Success"] = "Document deleted.";
            return RedirectToAction(nameof(File), new { id = doc.EmployeeId, folderId = doc.FolderId });
        }

        // ── employee self-service (any signed-in user, scoped to their own file) ────────
        [IT_Service_Management_System.Filters.AllowAnyRole]
        public async Task<IActionResult> MyDocuments()
        {
            var uid = Uid;
            if (uid == null) return RedirectToAction("Login", "Account");

            var employee = await _db.Users.Include(u => u.Department).FirstOrDefaultAsync(u => u.Id == uid.Value);
            if (employee == null) return NotFound();

            var vm = new EmployeeFileBrowserVm
            {
                Employee = employee,
                Documents = await _db.EmployeeDocuments
                    .Include(d => d.Category).Include(d => d.Folder).Include(d => d.CurrentVersion)
                    .Where(d => d.EmployeeId == uid.Value)
                    .OrderByDescending(d => d.CreatedAt).ToListAsync(),
                Folders = await _db.DocumentFolders.Where(f => f.IsActive).OrderBy(f => f.SortOrder).ToListAsync(),
                Categories = await _db.DocumentCategories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync()
            };
            vm.TotalDocuments = vm.Documents.Count;
            return View(vm);
        }

        [HttpPost]
        [IT_Service_Management_System.Filters.AllowAnyRole]
        [RequestSizeLimit(long.MaxValue)]
        public async Task<IActionResult> MyUpload(int folderId, int categoryId, List<IFormFile> files, string? description)
        {
            var uid = Uid;
            if (uid == null) return RedirectToAction("Login", "Account");

            if (files == null || files.Count == 0 || files.All(f => f.Length == 0))
            { TempData["Error"] = "Please choose a file."; return RedirectToAction(nameof(MyDocuments)); }
            if (!await _db.DocumentFolders.AnyAsync(f => f.Id == folderId) ||
                !await _db.DocumentCategories.AnyAsync(c => c.Id == categoryId))
            { TempData["Error"] = "Invalid folder or category."; return RedirectToAction(nameof(MyDocuments)); }

            var userName = HttpContext.Session.GetString("UserName");
            int saved = 0;
            foreach (var file in files)
            {
                if (file.Length == 0 || file.Length > MaxFileBytes) continue;
                if (BlockedExtensions.Contains(Path.GetExtension(file.FileName).ToLowerInvariant())) continue;

                var doc = await _docs.CreateFromUploadAsync(new DocumentUploadInput
                {
                    EmployeeId = uid.Value,
                    FolderId = folderId,
                    CategoryId = categoryId,
                    Confidentiality = ConfidentialityLevel.Confidential,
                    Description = description
                }, file, uid, userName);

                // Employee self-uploads await HR approval.
                doc.Status = DocumentStatus.PendingApproval;
                await _db.SaveChangesAsync();
                saved++;
            }

            TempData["Success"] = saved > 0
                ? $"{saved} document(s) uploaded and sent for HR approval."
                : "No files were uploaded.";
            return RedirectToAction(nameof(MyDocuments));
        }
    }
}
