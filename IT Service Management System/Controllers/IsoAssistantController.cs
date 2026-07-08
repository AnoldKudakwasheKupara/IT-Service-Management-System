using System.Text.RegularExpressions;
using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Filters;
using IT_Service_Management_System.Models.Ims;
using IT_Service_Management_System.ViewModels.Ims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>
    /// AI Compliance Assistant. A grounded, rules-based natural-language interface over the IMS: it
    /// interprets the question, queries the live management-system data and — for document questions —
    /// searches ONLY approved (Published) documents, so every answer is backed by real records.
    /// </summary>
    [RoleAuthorize("Admin", "SystemsAdmin", "QualityManager", "DocumentController", "DepartmentManager", "Auditor", "ExternalAuditor")]
    public class IsoAssistantController : Controller
    {
        private readonly ApplicationDbContext _db;

        public IsoAssistantController(ApplicationDbContext db) => _db = db;

        private static readonly string[] StopWords =
        {
            "what","is","are","our","the","a","an","of","for","to","in","on","show","me","list","find",
            "when","which","who","does","do","due","review","open","closed","all","please","tell","about",
            "belong","belongs","support","supports","evidence","documents","document","policy","policies"
        };

        private static readonly string[] Suggestions =
        {
            "What is our Leave Policy?",
            "When is the Password Policy due for review?",
            "Show overdue CAPAs",
            "What evidence supports ISO 9001 Clause 8.5?",
            "What documents belong to HR?",
            "Show open audit findings",
            "Which certificates are expiring?",
            "Show critical risks"
        };

        public IActionResult Index()
        {
            return View(new IsoAssistantVm
            {
                Suggestions = Suggestions.ToList(),
                Answer = "Ask me about your ISO documents, CAPAs, risks, audits, training, suppliers or compliance. I only surface approved (published) documents."
            });
        }

        [HttpGet]
        public async Task<IActionResult> Ask(string? q)
        {
            var vm = new IsoAssistantVm { Query = q, Suggestions = Suggestions.ToList() };
            if (string.IsNullOrWhiteSpace(q))
            {
                vm.Answer = "Please type a question.";
                return View(nameof(Index), vm);
            }

            var text = q.ToLowerInvariant();

            // ── CAPAs ──
            if (text.Contains("capa") || text.Contains("corrective"))
            {
                var overdue = text.Contains("overdue");
                var capas = await _db.Capas.Include(c => c.Responsible)
                    .Where(c => c.Status != CapaStatus.Closed && c.Status != CapaStatus.Verified).ToListAsync();
                if (overdue) capas = capas.Where(c => c.IsOverdue).ToList();
                vm.Answer = overdue ? $"There {(capas.Count == 1 ? "is" : "are")} {capas.Count} overdue CAPA(s)."
                                    : $"There {(capas.Count == 1 ? "is" : "are")} {capas.Count} open CAPA(s).";
                vm.Results = capas.Select(c => new AssistantResult
                {
                    Title = $"{c.Reference} — {c.Title}",
                    Subtitle = $"Due {(c.DueDate?.ToString("dd MMM yyyy") ?? "—")} · {c.Responsible?.FullName ?? "Unassigned"}",
                    Badge = c.Status.ToString(), Url = $"/Capa/Details/{c.Id}", Icon = "fa-screwdriver-wrench"
                }).ToList();
                return View(nameof(Index), vm);
            }

            // ── Audit findings ──
            if (text.Contains("finding") || (text.Contains("audit") && text.Contains("open")))
            {
                var findings = await _db.AuditFindings.Include(f => f.Audit)
                    .Where(f => f.Status != FindingStatus.Closed).OrderByDescending(f => f.Id).ToListAsync();
                vm.Answer = $"There {(findings.Count == 1 ? "is" : "are")} {findings.Count} open audit finding(s).";
                vm.Results = findings.Select(f => new AssistantResult
                {
                    Title = $"{f.Reference} — {f.Type}",
                    Subtitle = $"{f.Audit?.Reference ?? ""} · Clause {(f.ClauseReference ?? "—")}",
                    Badge = f.Status.ToString(), Url = $"/IsoAudits/FindingDetails/{f.Id}", Icon = "fa-magnifying-glass"
                }).ToList();
                return View(nameof(Index), vm);
            }

            // ── Evidence / clause ──
            if (text.Contains("evidence") || text.Contains("clause"))
            {
                var clause = Regex.Match(q, @"\d+(\.\d+)*").Value;
                var evidence = await _db.IsoEvidences
                    .Where(e => clause == "" || (e.IsoClause != null && e.IsoClause.Contains(clause))).ToListAsync();
                var docs = await _db.IsoDocuments
                    .Where(d => d.Status == DocumentStatus.Published && clause != "" && d.IsoClause != null && d.IsoClause.Contains(clause))
                    .ToListAsync();
                vm.Answer = clause == ""
                    ? $"Found {evidence.Count} evidence item(s) in the repository."
                    : $"Found {evidence.Count} evidence item(s) and {docs.Count} approved document(s) tagged to clause {clause}.";
                vm.Results = evidence.Select(e => new AssistantResult
                {
                    Title = $"{e.Reference} — {e.Title}", Subtitle = $"Clause {(e.IsoClause ?? "—")} · {e.Type}",
                    Badge = "Evidence", Url = $"/Evidence/Details/{e.Id}", Icon = "fa-paperclip"
                }).Concat(docs.Select(d => new AssistantResult
                {
                    Title = $"{d.DocumentNumber} — {d.Title}", Subtitle = $"Clause {(d.IsoClause ?? "—")}",
                    Badge = "Document", Url = $"/IsoDocuments/Details/{d.Id}", Icon = "fa-file-shield"
                })).ToList();
                return View(nameof(Index), vm);
            }

            // ── Training / certificate expiry ──
            if (text.Contains("certificate") || text.Contains("expir") || text.Contains("training") || text.Contains("competen"))
            {
                var today = DateTime.Now.Date;
                var recs = await _db.TrainingRecords.Include(t => t.User).Include(t => t.TrainingCourse)
                    .Where(t => t.CertificateExpiry != null && t.CertificateExpiry <= today.AddDays(60))
                    .OrderBy(t => t.CertificateExpiry).ToListAsync();
                vm.Answer = $"{recs.Count} certificate(s) are expired or expiring within 60 days.";
                vm.Results = recs.Select(t => new AssistantResult
                {
                    Title = $"{t.User?.FullName} — {t.CertificateName ?? t.TrainingCourse?.Title}",
                    Subtitle = $"Expires {t.CertificateExpiry:dd MMM yyyy}",
                    Badge = t.IsCertificateExpired ? "Expired" : "Expiring", Url = "/Training", Icon = "fa-graduation-cap"
                }).ToList();
                return View(nameof(Index), vm);
            }

            // ── Risks ──
            if (text.Contains("risk"))
            {
                var risks = await _db.Risks.Include(r => r.Owner).Where(r => r.Status != RiskStatus.Closed).ToListAsync();
                var critical = text.Contains("critical") || text.Contains("high");
                if (critical) risks = risks.Where(r => r.Band is RiskBand.Critical or RiskBand.High).ToList();
                risks = risks.OrderByDescending(r => r.Score).ToList();
                vm.Answer = critical ? $"There {(risks.Count == 1 ? "is" : "are")} {risks.Count} high/critical open risk(s)."
                                     : $"There {(risks.Count == 1 ? "is" : "are")} {risks.Count} open risk(s).";
                vm.Results = risks.Select(r => new AssistantResult
                {
                    Title = $"{r.Reference} — {r.Title}", Subtitle = $"Score {r.Score} · {r.Category}",
                    Badge = r.Band.ToString(), Url = $"/Risk/Details/{r.Id}", Icon = "fa-gauge-high"
                }).ToList();
                return View(nameof(Index), vm);
            }

            // ── Documents by department ──
            var dept = (await _db.Departments.ToListAsync())
                .FirstOrDefault(d => text.Contains(d.Name.ToLowerInvariant()));
            if (dept != null)
            {
                var docs = await _db.IsoDocuments.Include(d => d.Category)
                    .Where(d => d.Status == DocumentStatus.Published && d.DepartmentId == dept.Id).ToListAsync();
                vm.Answer = $"{dept.Name} has {docs.Count} approved document(s).";
                vm.Results = docs.Select(DocResult).ToList();
                return View(nameof(Index), vm);
            }

            // ── Default: full-text search over APPROVED documents (+ answer review dates when asked) ──
            var tokens = Regex.Split(text, @"[^a-z0-9]+")
                .Where(w => w.Length > 3 && !StopWords.Contains(w)).Distinct().ToList();

            var published = await _db.IsoDocuments.Include(d => d.Category).Include(d => d.Department)
                .Where(d => d.Status == DocumentStatus.Published).ToListAsync();

            var matches = tokens.Count == 0
                ? published
                : published.Where(d => tokens.Any(t =>
                        d.Title.ToLowerInvariant().Contains(t) ||
                        (d.Keywords ?? "").ToLowerInvariant().Contains(t) ||
                        (d.Summary ?? "").ToLowerInvariant().Contains(t)))
                    .OrderByDescending(d => tokens.Count(t => d.Title.ToLowerInvariant().Contains(t)))
                    .ToList();

            if (matches.Count == 0)
            {
                vm.Answer = "I couldn't find an approved document matching that. Try a policy or document name, a department, or ask about CAPAs, risks, findings or certificates.";
                return View(nameof(Index), vm);
            }

            var asksReview = text.Contains("review") || text.Contains("when");
            var top = matches.First();
            if (asksReview && top.ReviewDate.HasValue)
                vm.Answer = $"\"{top.Title}\" ({top.DocumentNumber}) is next due for review on {top.ReviewDate:dd MMMM yyyy}.";
            else
                vm.Answer = $"Found {matches.Count} approved document(s) matching your question. The closest match is \"{top.Title}\" ({top.DocumentNumber}).";

            vm.Results = matches.Take(10).Select(DocResult).ToList();
            return View(nameof(Index), vm);
        }

        private static AssistantResult DocResult(IsoDocument d) => new()
        {
            Title = $"{d.DocumentNumber} — {d.Title}",
            Subtitle = $"{d.Type} · {(d.Category?.Name ?? "")} · v{d.CurrentVersion}"
                       + (d.ReviewDate.HasValue ? $" · review {d.ReviewDate:dd MMM yyyy}" : ""),
            Badge = "Published", Url = $"/IsoDocuments/Details/{d.Id}", Icon = "fa-file-shield"
        };
    }
}
