using System.IO.Compression;
using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Helpers.Efm;
using IT_Service_Management_System.Models.Efm;
using IT_Service_Management_System.Services.Efm;
using IT_Service_Management_System.ViewModels.Efm;
using IT_Service_Management_System.ViewModels.Reports;
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
        private readonly DocumentApprovalService _approvals;
        private readonly IOcrService _ocr;

        private const long MaxFileBytes = 50L * 1024 * 1024; // 50 MB/file
        private static readonly string[] BlockedExtensions =
            { ".exe", ".dll", ".bat", ".cmd", ".com", ".scr", ".msi", ".ps1", ".sh", ".vbs", ".jar" };

        public EmployeeDocumentsController(ApplicationDbContext db, DocumentService docs,
            DocumentMaintenanceService maint, DocumentApprovalService approvals, IOcrService ocr)
        {
            _db = db;
            _docs = docs;
            _maint = maint;
            _approvals = approvals;
            _ocr = ocr;
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
            DocumentStatus? status, string? expiry, DateTime? from, DateTime? to, bool archived = false, int page = 1)
        {
            IQueryable<EmployeeDocument> query = _db.EmployeeDocuments
                .Include(d => d.Employee).ThenInclude(u => u!.Department)
                .Include(d => d.Category)
                .Include(d => d.Folder)
                .Include(d => d.CurrentVersion)
                .Include(d => d.Tags).ThenInclude(t => t.Tag)
                .Where(d => d.IsArchived == archived);

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
            var (items, paging) = await query.PageAsync(page, 10);
            ViewBag.Paging = paging;

            return View(new DocumentSearchVm
            {
                Results = items,
                Folders = await _db.DocumentFolders.Where(f => f.IsActive).OrderBy(f => f.SortOrder).ToListAsync(),
                Categories = await _db.DocumentCategories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(),
                Q = q, FolderId = folderId, CategoryId = categoryId, Status = status, Expiry = expiry,
                From = from, To = to, Archived = archived
            });
        }

        // ── audit trail ────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Audit(string? q, DocumentAuditAction? action, int? employeeId,
            int? documentId, DateTime? from, DateTime? to, int page = 1)
        {
            var (logs, paging) = await BuildAuditQuery(q, action, employeeId, documentId, from, to)
                .OrderByDescending(a => a.Timestamp).PageAsync(page, 10);
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

        // ── analytics dashboard ────────────────────────────────────────────────────────
        public async Task<IActionResult> Dashboard()
        {
            var today = DateTime.Today;
            var docs = _db.EmployeeDocuments.AsNoTracking();

            var folderNames = await _db.DocumentFolders.ToDictionaryAsync(f => f.Id, f => f.Name);
            var categoryNames = await _db.DocumentCategories.ToDictionaryAsync(c => c.Id, c => c.Name);

            // Group by scalar FK (translatable + scale-friendly), then map names in memory.
            var byFolder = await docs.Where(d => !d.IsArchived).GroupBy(d => d.FolderId)
                .Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
            var byCategory = await docs.Where(d => !d.IsArchived).GroupBy(d => d.CategoryId)
                .Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
            var byStatus = await docs.GroupBy(d => d.Status)
                .Select(g => new { g.Key, Count = g.Count() }).ToListAsync();

            var vm = new EfmDashboardVm
            {
                GeneratedAt = DateTime.Now,
                TotalDocuments = await docs.CountAsync(d => !d.IsArchived),
                Expired = await docs.CountAsync(d => !d.IsArchived && d.ExpiryDate != null && d.ExpiryDate < today),
                ExpiringSoon = await docs.CountAsync(d => !d.IsArchived && d.ExpiryDate != null && d.ExpiryDate >= today && d.ExpiryDate <= today.AddDays(30)),
                PendingApproval = await docs.CountAsync(d => d.Status == DocumentStatus.PendingApproval),
                Archived = await docs.CountAsync(d => d.IsArchived),
                TotalVersions = await _db.DocumentVersions.CountAsync(),
                StorageBytes = await _db.DocumentVersions.SumAsync(v => (long?)v.FileSizeBytes) ?? 0,
                UnreadNotifications = await _db.DocumentNotifications.CountAsync(n => !n.IsRead),
                ByFolder = byFolder.OrderByDescending(x => x.Count).Take(10)
                    .Select(x => new NameCount(folderNames.GetValueOrDefault(x.Key, "?"), x.Count)).ToList(),
                ByCategory = byCategory.OrderByDescending(x => x.Count).Take(10)
                    .Select(x => new NameCount(categoryNames.GetValueOrDefault(x.Key, "?"), x.Count)).ToList(),
                ByStatus = byStatus.OrderByDescending(x => x.Count)
                    .Select(x => new NameCount(x.Key.ToString(), x.Count)).ToList(),
                RecentlyUploaded = await docs.Include(d => d.Employee).Include(d => d.Category)
                    .Where(d => !d.IsArchived).OrderByDescending(d => d.CreatedAt).Take(8).ToListAsync(),
                MostViewed = await docs.Include(d => d.Employee).Include(d => d.Category)
                    .Where(d => !d.IsArchived && d.ViewCount > 0).OrderByDescending(d => d.ViewCount).Take(8).ToListAsync()
            };
            return View(vm);
        }

        // ── employee document timeline ─────────────────────────────────────────────────
        public async Task<IActionResult> Timeline(int id)
        {
            var employee = await _db.Users.Include(u => u.Department).FirstOrDefaultAsync(u => u.Id == id);
            if (employee == null) return NotFound();
            ViewBag.Employee = employee;
            var docs = await _db.EmployeeDocuments.Include(d => d.Category).Include(d => d.Folder)
                .Where(d => d.EmployeeId == id)
                .OrderByDescending(d => d.IssueDate ?? d.CreatedAt).ToListAsync();
            return View(docs);
        }

        // ── compliance report (file completeness across employees) ─────────────────────
        public async Task<IActionResult> Compliance() => View(await BuildComplianceAsync());

        public async Task<IActionResult> ComplianceExport()
        {
            var rows = await BuildComplianceAsync();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Employee,Department,Present,Required,Percent,Missing");
            foreach (var r in rows)
                sb.AppendLine(string.Join(",", new[] {
                    r.EmployeeName, r.Department, r.PresentCount.ToString(), r.RequiredCount.ToString(),
                    r.Percent + "%", r.Missing }.Select(Csv)));
            return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv",
                $"file-completeness-{DateTime.Now:yyyyMMdd}.csv");
        }

        private async Task<List<ComplianceRow>> BuildComplianceAsync()
        {
            var employees = await _db.Users.Include(u => u.Department)
                .OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToListAsync();
            var rows = new List<ComplianceRow>();
            foreach (var e in employees)
            {
                var c = await _maint.GetCompletenessAsync(e.Id);
                rows.Add(new ComplianceRow
                {
                    EmployeeId = e.Id, EmployeeName = e.FullName, Department = e.Department?.Name,
                    RequiredCount = c.RequiredCount, PresentCount = c.PresentCount, Percent = c.Percent,
                    Missing = string.Join("; ", c.MissingCategories)
                });
            }
            return rows;
        }

        // ── secure share links ─────────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateShare(int id, int? expiresDays, string? password, int? maxDownloads)
        {
            var doc = await _db.EmployeeDocuments.FindAsync(id);
            if (doc == null) return NotFound();

            var share = new DocumentShare
            {
                EmployeeDocumentId = id,
                Token = Guid.NewGuid().ToString("N"),
                CreatedById = Uid,
                CreatedByName = HttpContext.Session.GetString("UserName"),
                CreatedAt = DateTime.Now,
                ExpiresAt = expiresDays.HasValue ? DateTime.Now.AddDays(expiresDays.Value) : null,
                PasswordHash = string.IsNullOrWhiteSpace(password) ? null : PasswordHasher.HashPassword(password),
                MaxDownloads = maxDownloads,
                IsReadOnly = true
            };
            _db.DocumentShares.Add(share);
            await _db.SaveChangesAsync();
            await _docs.LogAsync(DocumentAuditAction.Shared, id, doc.EmployeeId,
                $"Created share link (expires {(share.ExpiresAt?.ToString("MMM dd, yyyy") ?? "never")})");

            TempData["ShareLink"] = Url.Action("Shared", "EmployeeDocuments", new { token = share.Token }, Request.Scheme);
            TempData["Success"] = "Secure share link created.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevokeShare(int shareId, int documentId)
        {
            var share = await _db.DocumentShares.FindAsync(shareId);
            if (share != null && share.RevokedAt == null) { share.RevokedAt = DateTime.Now; await _db.SaveChangesAsync(); }
            TempData["Success"] = "Share link revoked.";
            return RedirectToAction(nameof(Details), new { id = documentId });
        }

        // Public, read-only share access — no login required.
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public async Task<IActionResult> Shared(string token, string? password, bool inline = false, bool dl = false)
        {
            var share = await _db.DocumentShares.Include(s => s.Document).ThenInclude(d => d!.CurrentVersion)
                .FirstOrDefaultAsync(s => s.Token == token);

            if (share == null || share.RevokedAt != null) { ViewBag.Error = "This link is invalid or has been revoked."; return View("SharedError"); }
            if (share.ExpiresAt != null && share.ExpiresAt < DateTime.Now) { ViewBag.Error = "This link has expired."; return View("SharedError"); }
            if (share.MaxDownloads != null && share.DownloadCount >= share.MaxDownloads) { ViewBag.Error = "This link has reached its download limit."; return View("SharedError"); }

            if (share.PasswordHash != null &&
                (string.IsNullOrEmpty(password) || !PasswordHasher.VerifyPassword(password, share.PasswordHash)))
            {
                ViewBag.NeedPassword = true;
                ViewBag.Token = token;
                if (!string.IsNullOrEmpty(password)) ViewBag.Error = "Incorrect password.";
                return View("Shared", share);
            }

            if (inline || dl)
            {
                var result = await _docs.OpenCurrentAsync(share.EmployeeDocumentId);
                if (result == null) return NotFound();
                if (dl)
                {
                    share.DownloadCount++;
                    await _db.SaveChangesAsync();
                    await _docs.LogAsync(DocumentAuditAction.Downloaded, share.EmployeeDocumentId, share.Document?.EmployeeId, "Downloaded via share link");
                    return File(result.Value.Stream, result.Value.Version.ContentType, result.Value.Version.FileName);
                }
                Response.Headers.ContentDisposition = "inline";
                return File(result.Value.Stream, result.Value.Version.ContentType);
            }

            ViewBag.Token = token;
            ViewBag.Password = password;
            return View("Shared", share);
        }

        // ── notifications + maintenance ────────────────────────────────────────────────
        public async Task<IActionResult> Notifications(int page = 1)
        {
            var query = _db.DocumentNotifications
                .OrderByDescending(n => !n.IsRead).ThenByDescending(n => n.CreatedAt);
            var (items, paging) = await query.PageAsync(page, 10);
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

        // Mark a single notification as read (per-item button on the Notifications page).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkNotificationRead(int id)
        {
            var notification = await _db.DocumentNotifications.FindAsync(id);
            if (notification == null) return NotFound();
            if (!notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.Now;
                await _db.SaveChangesAsync();
            }
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
                try
                {
                    await _docs.CreateFromUploadAsync(perFile, file, userId, userName);
                    saved++;
                }
                catch (UploadRejectedException ex) { skipped.Add(ex.Message); }
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
                .Include(d => d.Comments)
                .Include(d => d.Approvals)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (doc == null) return NotFound();
            if (!CanOpen(doc)) return Denied();

            ViewBag.IsStaff = EfmAccess.IsStaff(Role);
            if (EfmAccess.IsStaff(Role))
                ViewBag.Shares = await _db.DocumentShares
                    .Where(s => s.EmployeeDocumentId == id && s.RevokedAt == null)
                    .OrderByDescending(s => s.CreatedAt).ToListAsync();

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

            DocumentVersion v;
            try
            {
                v = await _docs.AddVersionAsync(id, file, changeNote,
                    HttpContext.Session.GetInt32("UserId"), HttpContext.Session.GetString("UserName"));
            }
            catch (UploadRejectedException ex)
            { TempData["Error"] = ex.Message; return RedirectToAction(nameof(Details), new { id }); }
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
            var skipped = new List<string>();
            foreach (var file in files)
            {
                if (file.Length == 0 || file.Length > MaxFileBytes) continue;
                if (BlockedExtensions.Contains(Path.GetExtension(file.FileName).ToLowerInvariant())) continue;

                try
                {
                    var doc = await _docs.CreateFromUploadAsync(new DocumentUploadInput
                    {
                        EmployeeId = uid.Value,
                        FolderId = folderId,
                        CategoryId = categoryId,
                        Confidentiality = ConfidentialityLevel.Confidential,
                        Description = description
                    }, file, uid, userName);

                    // Employee self-uploads await HR approval — record the approval step + notify HR.
                    await _approvals.SubmitForApprovalAsync(doc);
                    saved++;
                }
                catch (UploadRejectedException ex) { skipped.Add(ex.Message); }
            }

            if (skipped.Count > 0)
                TempData["Error"] = "Some files were rejected: " + string.Join("; ", skipped);
            TempData["Success"] = saved > 0
                ? $"{saved} document(s) uploaded and sent for HR approval."
                : "No files were uploaded.";
            return RedirectToAction(nameof(MyDocuments));
        }

        // ── approval workflow (HR queue + approve/reject) ──────────────────────────────
        public async Task<IActionResult> Approvals(bool history = false, int page = 1)
        {
            IQueryable<DocumentApproval> query = _db.DocumentApprovals
                .Include(a => a.Document).ThenInclude(d => d!.Employee)
                .Include(a => a.Document).ThenInclude(d => d!.Category)
                .Include(a => a.Document).ThenInclude(d => d!.Folder)
                .Include(a => a.Document).ThenInclude(d => d!.CurrentVersion);

            // Non-admin staff never see approval steps for Restricted documents.
            if (!EfmAccess.IsFullAccess(Role))
                query = query.Where(a => a.Document!.ConfidentialityLevel < ConfidentialityLevel.Restricted);

            query = history
                ? query.Where(a => a.Status != ApprovalStatus.Pending).OrderByDescending(a => a.DecidedAt)
                : query.Where(a => a.Status == ApprovalStatus.Pending).OrderBy(a => a.CreatedAt);

            var (items, paging) = await query.PageAsync(page, 10);
            ViewBag.Paging = paging;
            ViewBag.History = history;
            ViewBag.PendingCount = await _approvals.PendingCountAsync();
            return View(items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int approvalId, string? comments)
        {
            if (!EfmAccess.Can(Role, EfmPermission.Approve)) return Denied();
            var r = await _approvals.DecideAsync(approvalId, approve: true, comments,
                Uid, HttpContext.Session.GetString("UserName"));
            TempData[r.Ok ? "Success" : "Error"] = r.Message;
            return RedirectToAction(nameof(Approvals));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int approvalId, string? comments)
        {
            if (!EfmAccess.Can(Role, EfmPermission.Reject)) return Denied();
            if (string.IsNullOrWhiteSpace(comments))
            { TempData["Error"] = "A reason is required to reject a document."; return RedirectToAction(nameof(Approvals)); }

            var r = await _approvals.DecideAsync(approvalId, approve: false, comments,
                Uid, HttpContext.Session.GetString("UserName"));
            TempData[r.Ok ? "Success" : "Error"] = r.Message;
            return RedirectToAction(nameof(Approvals));
        }

        // ── manual archive / restore ───────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Archive(int id, string? returnUrl)
        {
            if (!EfmAccess.Can(Role, EfmPermission.Archive)) return Denied();
            var doc = await _db.EmployeeDocuments.FirstOrDefaultAsync(d => d.Id == id);
            if (doc == null) return NotFound();
            if (!CanOpen(doc)) return Denied();

            if (!doc.IsArchived)
            {
                doc.IsArchived = true;
                doc.ArchivedAt = DateTime.Now;
                doc.Status = DocumentStatus.Archived;
                doc.UpdatedAt = DateTime.Now;
                await _db.SaveChangesAsync();
                await _docs.LogAsync(DocumentAuditAction.Archived, doc.Id, doc.EmployeeId, $"Archived '{doc.Title}'");
            }
            TempData["Success"] = "Document archived.";
            return SafeRedirect(returnUrl) ?? RedirectToAction(nameof(File), new { id = doc.EmployeeId, folderId = doc.FolderId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id, string? returnUrl)
        {
            if (!EfmAccess.Can(Role, EfmPermission.Restore)) return Denied();
            var doc = await _db.EmployeeDocuments.FirstOrDefaultAsync(d => d.Id == id);
            if (doc == null) return NotFound();
            if (!CanOpen(doc)) return Denied();

            if (doc.IsArchived)
            {
                doc.IsArchived = false;
                doc.ArchivedAt = null;
                // Recompute a sensible live status: expired if past expiry, else active.
                doc.Status = doc.IsExpired ? DocumentStatus.Expired : DocumentStatus.Active;
                doc.UpdatedAt = DateTime.Now;
                await _db.SaveChangesAsync();
                await _docs.LogAsync(DocumentAuditAction.Restored, doc.Id, doc.EmployeeId, $"Restored '{doc.Title}' from archive");
            }
            TempData["Success"] = "Document restored from archive.";
            return SafeRedirect(returnUrl) ?? RedirectToAction(nameof(Details), new { id = doc.Id });
        }

        // ── bulk operations (download / delete / archive / move / tag) ──────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Bulk(string op, int[] ids, int? targetFolderId, string? tags,
            int? employeeId, string? returnUrl)
        {
            if (ids == null || ids.Length == 0)
            { TempData["Error"] = "No documents were selected."; return BulkReturn(returnUrl, employeeId); }

            // Only act on documents the current user is allowed to open (confidentiality-aware).
            var docs = await _db.EmployeeDocuments.Where(d => ids.Contains(d.Id)).ToListAsync();
            docs = docs.Where(CanOpen).ToList();
            if (docs.Count == 0) return Denied();

            switch ((op ?? "").ToLowerInvariant())
            {
                case "download":
                    return await BulkDownloadAsync(docs);

                case "delete":
                    if (!EfmAccess.Can(Role, EfmPermission.Delete)) return Denied();
                    foreach (var d in docs)
                    {
                        d.IsDeleted = true; d.UpdatedAt = DateTime.Now;
                        await _docs.LogAsync(DocumentAuditAction.Deleted, d.Id, d.EmployeeId, $"Bulk-deleted '{d.Title}'");
                    }
                    await _db.SaveChangesAsync();
                    TempData["Success"] = $"{docs.Count} document(s) deleted.";
                    break;

                case "archive":
                    if (!EfmAccess.Can(Role, EfmPermission.Archive)) return Denied();
                    foreach (var d in docs.Where(d => !d.IsArchived))
                    {
                        d.IsArchived = true; d.ArchivedAt = DateTime.Now; d.Status = DocumentStatus.Archived; d.UpdatedAt = DateTime.Now;
                        await _docs.LogAsync(DocumentAuditAction.Archived, d.Id, d.EmployeeId, $"Bulk-archived '{d.Title}'");
                    }
                    await _db.SaveChangesAsync();
                    TempData["Success"] = $"{docs.Count} document(s) archived.";
                    break;

                case "restore":
                    if (!EfmAccess.Can(Role, EfmPermission.Restore)) return Denied();
                    foreach (var d in docs.Where(d => d.IsArchived))
                    {
                        d.IsArchived = false; d.ArchivedAt = null;
                        d.Status = d.IsExpired ? DocumentStatus.Expired : DocumentStatus.Active; d.UpdatedAt = DateTime.Now;
                        await _docs.LogAsync(DocumentAuditAction.Restored, d.Id, d.EmployeeId, $"Bulk-restored '{d.Title}'");
                    }
                    await _db.SaveChangesAsync();
                    TempData["Success"] = $"{docs.Count} document(s) restored.";
                    break;

                case "move":
                    if (targetFolderId == null || !await _db.DocumentFolders.AnyAsync(f => f.Id == targetFolderId))
                    { TempData["Error"] = "Choose a destination folder."; break; }
                    foreach (var d in docs)
                    {
                        d.FolderId = targetFolderId.Value; d.UpdatedAt = DateTime.Now;
                        await _docs.LogAsync(DocumentAuditAction.Moved, d.Id, d.EmployeeId, $"Bulk-moved '{d.Title}'");
                    }
                    await _db.SaveChangesAsync();
                    TempData["Success"] = $"{docs.Count} document(s) moved.";
                    break;

                case "tag":
                    if (string.IsNullOrWhiteSpace(tags))
                    { TempData["Error"] = "Enter one or more tags."; break; }
                    int tagged = 0;
                    foreach (var d in docs) tagged += await _docs.AddTagsAsync(d.Id, tags);
                    TempData["Success"] = $"Added {tagged} tag association(s) across {docs.Count} document(s).";
                    break;

                default:
                    TempData["Error"] = "Unknown bulk action.";
                    break;
            }
            return BulkReturn(returnUrl, employeeId);
        }

        private async Task<IActionResult> BulkDownloadAsync(List<EmployeeDocument> docs)
        {
            var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var d in docs)
                {
                    var opened = await _docs.OpenCurrentAsync(d.Id);
                    if (opened == null) continue;
                    var name = MakeUniqueEntryName(opened.Value.Version.FileName, used);
                    var entry = zip.CreateEntry(name, CompressionLevel.Fastest);
                    await using var es = entry.Open();
                    await using (opened.Value.Stream) await opened.Value.Stream.CopyToAsync(es);
                    await _docs.LogAsync(DocumentAuditAction.Downloaded, d.Id, d.EmployeeId, "Downloaded via bulk export");
                }
            }
            ms.Position = 0;
            return File(ms, "application/zip", $"documents-{DateTime.Now:yyyyMMdd-HHmm}.zip");
        }

        private static string MakeUniqueEntryName(string fileName, HashSet<string> used)
        {
            var name = Path.GetFileName(fileName);
            if (used.Add(name)) return name;
            var stem = Path.GetFileNameWithoutExtension(name);
            var ext = Path.GetExtension(name);
            for (int i = 2; ; i++)
            {
                var candidate = $"{stem} ({i}){ext}";
                if (used.Add(candidate)) return candidate;
            }
        }

        private IActionResult BulkReturn(string? returnUrl, int? employeeId) =>
            SafeRedirect(returnUrl)
            ?? (employeeId.HasValue
                ? RedirectToAction(nameof(File), new { id = employeeId.Value })
                : RedirectToAction(nameof(Search)));

        private IActionResult? SafeRedirect(string? returnUrl) =>
            !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : null;

        // ── document comments (HR collaboration) ────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(int documentId, string body)
        {
            var doc = await _db.EmployeeDocuments.FirstOrDefaultAsync(d => d.Id == documentId);
            if (doc == null) return NotFound();
            if (!CanOpen(doc)) return Denied();
            if (string.IsNullOrWhiteSpace(body))
            { TempData["Error"] = "Comment cannot be empty."; return RedirectToAction(nameof(Details), new { id = documentId }); }

            _db.DocumentComments.Add(new DocumentComment
            {
                EmployeeDocumentId = documentId,
                AuthorId = Uid,
                AuthorName = HttpContext.Session.GetString("UserName"),
                Body = body.Length > 2000 ? body[..2000] : body,
                CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();
            TempData["Success"] = "Comment added.";
            return RedirectToAction(nameof(Details), new { id = documentId });
        }

        // ── OCR pre-scan: extract text + suggest metadata for the upload form ───────────
        [HttpPost]
        [RequestSizeLimit(MaxFileBytes + 1024)]
        public async Task<IActionResult> ScanDocument(IFormFile? file)
        {
            if (file == null || file.Length == 0) return Json(new { ok = false, message = "No file." });
            if (file.Length > MaxFileBytes) return Json(new { ok = false, message = "File too large." });
            if (BlockedExtensions.Contains(Path.GetExtension(file.FileName).ToLowerInvariant()))
                return Json(new { ok = false, message = "Blocked file type." });
            if (!_ocr.CanHandle(file.ContentType))
                return Json(new { ok = false, enabled = false, message = "OCR is not available for this file type." });

            string? text;
            await using (var s = file.OpenReadStream())
                text = await _ocr.ExtractTextAsync(s, file.ContentType);

            if (string.IsNullOrWhiteSpace(text))
                return Json(new { ok = false, enabled = true, message = "No readable text was found." });

            var meta = DocumentMetadataExtractor.Extract(text);
            return Json(new
            {
                ok = true,
                issueDate = meta.IssueDate?.ToString("yyyy-MM-dd"),
                expiryDate = meta.ExpiryDate?.ToString("yyyy-MM-dd"),
                documentNumber = meta.DocumentNumber,
                idNumber = meta.IdNumber
            });
        }

        // ── richer report exports (Excel + branded PDF) ────────────────────────────────
        public async Task<IActionResult> AuditExportExcel(string? q, DocumentAuditAction? action, int? employeeId,
            int? documentId, DateTime? from, DateTime? to)
        {
            var rows = await ResolveRowsAsync(await BuildAuditQuery(q, action, employeeId, documentId, from, to)
                .OrderByDescending(a => a.Timestamp).Take(10000).ToListAsync());
            return File(EfmExport.AuditXlsx(rows), EfmExport.XlsxContentType,
                $"document-audit-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
        }

        public async Task<IActionResult> AuditExportPdf(string? q, DocumentAuditAction? action, int? employeeId,
            int? documentId, DateTime? from, DateTime? to)
        {
            var rows = await ResolveRowsAsync(await BuildAuditQuery(q, action, employeeId, documentId, from, to)
                .OrderByDescending(a => a.Timestamp).Take(5000).ToListAsync());
            var scope = employeeId.HasValue ? (await _db.Users.FindAsync(employeeId.Value))?.FullName : null;
            return File(EfmExport.AuditPdf(rows, scope), "application/pdf",
                $"document-audit-{DateTime.Now:yyyyMMdd-HHmm}.pdf");
        }

        public async Task<IActionResult> ComplianceExportExcel()
        {
            var rows = await BuildComplianceAsync();
            return File(EfmExport.ComplianceXlsx(rows), EfmExport.XlsxContentType,
                $"file-completeness-{DateTime.Now:yyyyMMdd}.xlsx");
        }

        public async Task<IActionResult> ComplianceExportPdf()
        {
            var rows = await BuildComplianceAsync();
            return File(EfmExport.CompliancePdf(rows), "application/pdf",
                $"file-completeness-{DateTime.Now:yyyyMMdd}.pdf");
        }

        // ── employee self-service notifications + bell ─────────────────────────────────
        [IT_Service_Management_System.Filters.AllowAnyRole]
        public async Task<IActionResult> MyNotifications(int page = 1)
        {
            var uid = Uid;
            if (uid == null) return RedirectToAction("Login", "Account");

            var query = _db.DocumentNotifications
                .Where(n => n.RecipientUserId == uid)
                .OrderByDescending(n => !n.IsRead).ThenByDescending(n => n.CreatedAt);
            var (items, paging) = await query.PageAsync(page, 10);
            ViewBag.Paging = paging;
            ViewBag.UnreadCount = await _db.DocumentNotifications.CountAsync(n => n.RecipientUserId == uid && !n.IsRead);
            return View(items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [IT_Service_Management_System.Filters.AllowAnyRole]
        public async Task<IActionResult> MarkMyNotificationsRead()
        {
            var uid = Uid;
            if (uid == null) return RedirectToAction("Login", "Account");
            var unread = await _db.DocumentNotifications.Where(n => n.RecipientUserId == uid && !n.IsRead).ToListAsync();
            foreach (var n in unread) { n.IsRead = true; n.ReadAt = DateTime.Now; }
            await _db.SaveChangesAsync();
            TempData["Success"] = $"{unread.Count} notification(s) marked as read.";
            return RedirectToAction(nameof(MyNotifications));
        }
    }
}
