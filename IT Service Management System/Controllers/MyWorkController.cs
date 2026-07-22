using IT_Service_Management_System.Services.Itsm;
using IT_Service_Management_System.ViewModels.Itsm;
using Microsoft.AspNetCore.Mvc;

namespace IT_Service_Management_System.Controllers
{
    public class MyWorkController : Controller
    {
        private readonly IMyWorkService _service;
        public MyWorkController(IMyWorkService service) => _service = service;

        public async Task<IActionResult> Index(string? kind, string? bucket, string? q, CancellationToken ct)
        {
            var userId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var role = HttpContext.Session.GetString("UserRole");
            var now = DateTime.Now;
            var all = await _service.GetAsync(userId, role, now, ct);
            var filtered = all.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(kind)) filtered = filtered.Where(i => i.Kind == kind);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                filtered = filtered.Where(i => i.Reference.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    i.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    i.Source.Contains(term, StringComparison.OrdinalIgnoreCase));
            }
            filtered = bucket switch
            {
                "overdue" => filtered.Where(i => i.IsOverdue(now)),
                "today" => filtered.Where(i => i.IsDueToday(now)),
                "approvals" => filtered.Where(i => i.RequiresDecision),
                "sla" => filtered.Where(i => i.IsSlaRisk),
                _ => filtered
            };

            return View(new MyWorkVm
            {
                Items = filtered.ToList(), Kind = kind, Bucket = bucket, Query = q,
                Total = all.Count, Overdue = all.Count(i => i.IsOverdue(now)),
                DueToday = all.Count(i => i.IsDueToday(now)), Approvals = all.Count(i => i.RequiresDecision),
                SlaRisk = all.Count(i => i.IsSlaRisk), Kinds = all.Select(i => i.Kind).Distinct().OrderBy(x => x).ToList()
            });
        }
    }
}
