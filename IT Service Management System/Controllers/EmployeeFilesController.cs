using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using IT_Service_Management_System.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    [IT_Service_Management_System.Filters.RoleAuthorize("Admin", "SystemsAdmin", "HR")]
    public class EmployeeFilesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly AuditService _audit;

        private const long MaxFileBytes = 25 * 1024 * 1024; // 25 MB per file
        private static readonly string[] BlockedExtensions =
            { ".exe", ".dll", ".bat", ".cmd", ".com", ".scr", ".msi", ".ps1", ".sh", ".js", ".vbs", ".jar" };

        public EmployeeFilesController(ApplicationDbContext context, IWebHostEnvironment env, AuditService audit)
        {
            _context = context;
            _env = env;
            _audit = audit;
        }

        private string StorageRoot()
        {
            var path = Path.Combine(_env.ContentRootPath, "employee-files");
            Directory.CreateDirectory(path);
            return path;
        }

        // ── employee picker ────────────────────────────────────────────────────────────
        public async Task<IActionResult> Index(string? q)
        {
            IQueryable<User> query = _context.Users.Include(u => u.Department);

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(u => u.FirstName.Contains(q) || u.LastName.Contains(q) || u.Email.Contains(q));

            var employees = await query
                .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
                .ToListAsync();

            // File counts per employee (single query).
            var counts = await _context.EmployeeFiles
                .GroupBy(f => f.EmployeeId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            ViewBag.FileCounts = counts;
            ViewBag.Search = q;
            return View(employees);
        }

        // ── one employee's files + upload ────────────────────────────────────────────
        public async Task<IActionResult> Files(int id)
        {
            var employee = await _context.Users
                .Include(u => u.Department)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (employee == null) return NotFound();

            ViewBag.Files = await _context.EmployeeFiles
                .Where(f => f.EmployeeId == id)
                .OrderByDescending(f => f.UploadedAt)
                .ToListAsync();

            return View(employee);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(long.MaxValue)]
        public async Task<IActionResult> Upload(int employeeId, List<IFormFile> files, string? category, string? description)
        {
            var employee = await _context.Users.FindAsync(employeeId);
            if (employee == null) return NotFound();

            if (files == null || files.Count == 0 || files.All(f => f.Length == 0))
            {
                TempData["Error"] = "Please choose at least one file to upload.";
                return RedirectToAction(nameof(Files), new { id = employeeId });
            }

            var root = StorageRoot();
            var uploader = HttpContext.Session.GetString("UserName") ?? "HR";
            int saved = 0;

            foreach (var file in files)
            {
                if (file.Length == 0) continue;

                if (file.Length > MaxFileBytes)
                {
                    TempData["Error"] = $"\"{file.FileName}\" exceeds the 25 MB limit and was skipped.";
                    continue;
                }

                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (BlockedExtensions.Contains(ext))
                {
                    TempData["Error"] = $"\"{file.FileName}\" is a blocked file type and was skipped.";
                    continue;
                }

                var storedName = $"{Guid.NewGuid():N}{ext}";
                var fullPath = Path.Combine(root, storedName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                    await file.CopyToAsync(stream);

                _context.EmployeeFiles.Add(new EmployeeFile
                {
                    EmployeeId = employeeId,
                    FileName = Path.GetFileName(file.FileName),
                    StoredName = storedName,
                    ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                    FileSize = file.Length,
                    Category = string.IsNullOrWhiteSpace(category) ? null : category,
                    Description = string.IsNullOrWhiteSpace(description) ? null : description,
                    UploadedAt = DateTime.Now,
                    UploadedBy = uploader
                });
                saved++;
            }

            if (saved > 0)
            {
                await _context.SaveChangesAsync();
                await _audit.LogAsync("Employee File Uploaded", "EmployeeFile", employeeId,
                    $"{saved} file(s) uploaded for {employee.FullName}");
                TempData["Success"] = $"{saved} file(s) uploaded for {employee.FullName}.";
            }

            return RedirectToAction(nameof(Files), new { id = employeeId });
        }

        // Authorized download — the file never sits under wwwroot, so this is the only way to fetch it.
        public async Task<IActionResult> Download(int id)
        {
            var file = await _context.EmployeeFiles.FindAsync(id);
            if (file == null) return NotFound();

            var fullPath = Path.Combine(StorageRoot(), file.StoredName);
            if (!System.IO.File.Exists(fullPath))
            {
                TempData["Error"] = "The file is missing from storage.";
                return RedirectToAction(nameof(Files), new { id = file.EmployeeId });
            }

            await _audit.LogAsync("Employee File Downloaded", "EmployeeFile", file.EmployeeId,
                $"Downloaded '{file.FileName}'");

            var bytes = await System.IO.File.ReadAllBytesAsync(fullPath);
            return File(bytes, file.ContentType, file.FileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var file = await _context.EmployeeFiles.FindAsync(id);
            if (file == null) return NotFound();

            var employeeId = file.EmployeeId;
            var fullPath = Path.Combine(StorageRoot(), file.StoredName);

            _context.EmployeeFiles.Remove(file);
            await _context.SaveChangesAsync();

            try { if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath); }
            catch { /* record already removed; orphaned blob is harmless */ }

            await _audit.LogAsync("Employee File Deleted", "EmployeeFile", employeeId,
                $"Deleted '{file.FileName}'");

            TempData["Success"] = "File deleted.";
            return RedirectToAction(nameof(Files), new { id = employeeId });
        }
    }
}
