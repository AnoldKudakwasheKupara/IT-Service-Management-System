using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Helpers;
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

        private const long MaxFileBytes = 50L * 1024 * 1024; // 50 MB/file
        private static readonly string[] BlockedExtensions =
            { ".exe", ".dll", ".bat", ".cmd", ".com", ".scr", ".msi", ".ps1", ".sh", ".vbs", ".jar" };

        public EmployeeDocumentsController(ApplicationDbContext db, DocumentService docs)
        {
            _db = db;
            _docs = docs;
        }

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

        // ── digital file browser ───────────────────────────────────────────────────────
        public async Task<IActionResult> File(int id, int? folderId)
        {
            var employee = await _db.Users.Include(u => u.Department).FirstOrDefaultAsync(u => u.Id == id);
            if (employee == null) return NotFound();

            var folders = await _db.DocumentFolders.Where(f => f.IsActive)
                .OrderBy(f => f.SortOrder).ToListAsync();

            var counts = await _db.EmployeeDocuments
                .Where(d => d.EmployeeId == id && !d.IsArchived)
                .GroupBy(d => d.FolderId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            var docsQuery = _db.EmployeeDocuments
                .Include(d => d.Category)
                .Include(d => d.CurrentVersion)
                .Where(d => d.EmployeeId == id && !d.IsArchived);
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

            doc.ViewCount++;
            await _db.SaveChangesAsync();
            await _docs.LogAsync(DocumentAuditAction.Viewed, doc.Id, doc.EmployeeId, $"Viewed '{doc.Title}'");
            return View(doc);
        }

        // Inline preview (browser renders PDF/image).
        public async Task<IActionResult> Preview(int id)
        {
            var result = await _docs.OpenCurrentAsync(id);
            if (result == null) return NotFound();
            await _docs.LogAsync(DocumentAuditAction.Previewed, id, null, "Previewed document");
            Response.Headers.ContentDisposition = "inline";
            return File(result.Value.Stream, result.Value.Version.ContentType);
        }

        // Force download (attachment).
        public async Task<IActionResult> Download(int id)
        {
            var result = await _docs.OpenCurrentAsync(id);
            if (result == null) return NotFound();
            await _docs.LogAsync(DocumentAuditAction.Downloaded, id, null, $"Downloaded '{result.Value.Version.FileName}'");
            return File(result.Value.Stream, result.Value.Version.ContentType, result.Value.Version.FileName);
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
    }
}
