using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Filters;
using IT_Service_Management_System.Services;
using IT_Service_Management_System.Services.Ecie;
using IT_Service_Management_System.ViewModels.Ecie;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    /// <summary>
    /// The Enterprise Compliance Intelligence Engine (ECIE) — a deterministic, evidence-grounded reasoning
    /// layer over the whole management system. It never invents facts: every answer is assembled from stored,
    /// approved records via the Evidence Graph and routed through domain "specialists". Also exposes the
    /// continuous Compliance Health Score, the Evidence Map and Audit Mode.
    /// </summary>
    [RoleAuthorize("Admin", "SystemsAdmin", "QualityManager", "DocumentController", "DepartmentManager", "Auditor", "ExternalAuditor")]
    public class EcieController : Controller
    {
        private readonly EcieOrchestrator _engine;
        private readonly ComplianceHealthService _health;
        private readonly EvidenceGraphService _graph;
        private readonly IAiProvider _ai;
        private readonly ApplicationDbContext _db;
        private readonly AuditService _audit;

        public EcieController(EcieOrchestrator engine, ComplianceHealthService health, EvidenceGraphService graph,
            IAiProvider ai, ApplicationDbContext db, AuditService audit)
        {
            _engine = engine;
            _health = health;
            _graph = graph;
            _ai = ai;
            _db = db;
            _audit = audit;
        }

        private string? Role => HttpContext.Session.GetString("UserRole");
        private int? Uid => HttpContext.Session.GetInt32("UserId");

        // ── Intelligence console ─────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Index(string? q)
        {
            ViewBag.AiEnabled = _ai.IsEnabled;
            if (string.IsNullOrWhiteSpace(q))
            {
                return View("Ask", new EcieResponse
                {
                    Summary = "Ask the Compliance Intelligence Engine anything about your approved documents, CAPAs, risks, audits, findings, training, suppliers, meetings, objectives or compliance. Every answer is backed by stored evidence — nothing is invented.",
                    Suggestions = EcieOrchestrator.SampleQuestions.ToList()
                });
            }

            var response = await _engine.AnswerAsync(q, Role, Uid);
            await _audit.LogAsync("Query", "Ecie", null, $"\"{q}\" → {response.Specialist} ({response.EvidenceUsed.Count} evidence)");
            return View("Ask", response);
        }

        // Alias so links can post to /Ecie/Ask
        [HttpGet]
        public Task<IActionResult> Ask(string? q) => Index(q);

        // ── Compliance Health Score ──────────────────────────────────────────────
        public async Task<IActionResult> Health()
        {
            var vm = await _health.ComputeAsync();
            vm.AiEnabled = _ai.IsEnabled;
            return View(vm);
        }

        // ── Evidence Map (visual graph around a document) ────────────────────────
        public async Task<IActionResult> Graph(int id)
        {
            var doc = await _db.IsoDocuments.FirstOrDefaultAsync(d => d.Id == id);
            if (doc == null) return NotFound();
            if (!EvidenceGraphService.CanSeeDocument(Role, doc)) return RedirectToAction("AccessDenied", "Home");
            ViewBag.Doc = doc;
            var graph = await _graph.BuildDocumentGraphAsync(Role, id);
            return View(graph);
        }

        // ── Audit Mode (the engine acts as an external auditor) ──────────────────
        public async Task<IActionResult> AuditMode()
        {
            var vm = new AuditModeVm
            {
                Health = await _health.ComputeAsync(),
                Readiness = await _engine.AnswerAsync("Prepare for our ISO surveillance audit and identify gaps", Role, Uid),
                Questions = new List<string>
                {
                    "Show me your controlled document register and its review status.",
                    "Provide evidence for ISO 9001 Clause 8.5 and ISO 27001 Clause 6.1.",
                    "List all open non-conformities and their corrective actions.",
                    "Which management-review actions from the last review remain open?",
                    "Show the risk register and the treatment status of critical risks.",
                    "Demonstrate that employees have acknowledged the current policies.",
                    "Provide the training and competency records for affected staff.",
                    "Show the last internal audit programme and its findings."
                }
            };
            vm.Health.AiEnabled = _ai.IsEnabled;
            return View(vm);
        }
    }
}
