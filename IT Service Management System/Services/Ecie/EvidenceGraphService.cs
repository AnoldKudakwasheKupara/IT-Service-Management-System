using System.Text.RegularExpressions;
using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Helpers.Ims;
using IT_Service_Management_System.Models;
using IT_Service_Management_System.Models.Ims;
using IT_Service_Management_System.ViewModels.Ecie;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Services.Ecie
{
    /// <summary>A node in the evidence graph (for the visual graph view).</summary>
    public record GraphNode(string Id, string Kind, string Label, string Icon);
    /// <summary>A directed relationship between two evidence nodes.</summary>
    public record GraphEdge(string From, string To, string Label);
    public class EvidenceGraph
    {
        public List<GraphNode> Nodes { get; } = new();
        public List<GraphEdge> Edges { get; } = new();
    }

    /// <summary>
    /// The Evidence Graph — the engine's reasoning substrate. It turns stored records into evidence
    /// references and traverses the compliance chain (Policy → Procedure → … → Objective) across every
    /// module using clauses, departments and direct foreign keys. Every specialist grounds its answer here.
    /// </summary>
    public class EvidenceGraphService
    {
        private readonly ApplicationDbContext _db;
        public EvidenceGraphService(ApplicationDbContext db) => _db = db;

        // ── Query parsing ──────────────────────────────────────────────────────────
        private static readonly HashSet<string> Stop = new(StringComparer.OrdinalIgnoreCase)
        {
            "what","is","are","our","the","a","an","of","for","to","in","on","show","me","list","find","which",
            "who","does","do","due","all","please","tell","about","related","belong","belongs","support","supports",
            "generate","explain","why","how","get","give","see","view","with","and","or","that","this","these","those"
        };

        public static List<string> Tokenize(string text) =>
            Regex.Split(text.ToLowerInvariant(), @"[^a-z0-9]+")
                 .Where(w => w.Length > 3 && !Stop.Contains(w)).Distinct().ToList();

        public static string? ExtractClause(string text)
        {
            var m = Regex.Match(text, @"\b\d+(?:\.\d+)+\b");   // prefer sub-clauses like 8.5 / 6.1.2
            if (m.Success) return m.Value;
            m = Regex.Match(text, @"clause\s+(\d+)", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value : null;
        }

        public async Task<Department?> ResolveDepartmentAsync(string text)
        {
            var lower = text.ToLowerInvariant();
            var depts = await _db.Departments.AsNoTracking().ToListAsync();
            return depts.FirstOrDefault(d => lower.Contains(d.Name.ToLowerInvariant()));
        }

        // ── Permission ──────────────────────────────────────────────────────────────
        /// <summary>Approved (published), permission-filtered documents only — the sole authoritative source.</summary>
        public static bool CanSeeDocument(string? role, IsoDocument d)
        {
            if (d.Status != DocumentStatus.Published) return false;
            return d.Classification switch
            {
                DocumentClassification.Restricted => ImsAccess.IsAdministrator(role) || ImsAccess.IsQualityManager(role),
                DocumentClassification.Confidential => ImsAccess.IsImsManager(role) || ImsAccess.IsDocumentController(role) || ImsAccess.IsAuditor(role) || ImsAccess.IsExternalAuditor(role),
                _ => true
            };
        }

        // ── Evidence-ref mappers (shared by every specialist) ───────────────────────
        public static EvidenceRef Doc(IsoDocument d) => new()
        {
            Kind = d.Type.ToString(), Reference = d.DocumentNumber, Title = d.Title,
            Subtitle = $"v{d.CurrentVersion} · {d.Status}" + (d.ReviewDate.HasValue ? $" · review {d.ReviewDate:dd MMM yyyy}" : ""),
            Url = $"/IsoDocuments/Details/{d.Id}", Icon = "fa-file-shield", Badge = d.Classification.ToString()
        };
        public static EvidenceRef Capa(Capa c) => new()
        {
            Kind = "CAPA", Reference = c.Reference, Title = c.Title,
            Subtitle = $"{c.Status}" + (c.DueDate.HasValue ? $" · due {c.DueDate:dd MMM yyyy}" : ""),
            Url = $"/Capa/Details/{c.Id}", Icon = "fa-screwdriver-wrench", Badge = c.Type.ToString()
        };
        public static EvidenceRef RiskRef(Risk r) => new()
        {
            Kind = "Risk", Reference = r.Reference, Title = r.Title,
            Subtitle = $"Score {r.Score} · {r.Band} · {r.Category}", Url = $"/Risk/Details/{r.Id}",
            Icon = "fa-gauge-high", Badge = r.Band.ToString()
        };
        public static EvidenceRef Finding(AuditFinding f) => new()
        {
            Kind = "Finding", Reference = f.Reference, Title = f.Description.Length > 80 ? f.Description[..80] + "…" : f.Description,
            Subtitle = $"{f.Type} · {f.Status}" + (f.ClauseReference != null ? $" · clause {f.ClauseReference}" : ""),
            Url = $"/IsoAudits/FindingDetails/{f.Id}", Icon = "fa-magnifying-glass", Badge = f.Type.ToString()
        };
        public static EvidenceRef AuditRef(Audit a) => new()
        {
            Kind = "Audit", Reference = a.Reference, Title = a.Title, Subtitle = $"{a.Type} · {a.Status}",
            Url = $"/IsoAudits/Details/{a.Id}", Icon = "fa-clipboard-check", Badge = a.Status.ToString()
        };
        public static EvidenceRef Meeting(ManagementReview m) => new()
        {
            Kind = "Meeting", Reference = m.Reference, Title = m.Title,
            Subtitle = $"{m.MeetingDate:dd MMM yyyy} · {m.Status}", Url = $"/ManagementReviews/Details/{m.Id}",
            Icon = "fa-people-group", Badge = m.Status.ToString()
        };
        public static EvidenceRef Training(TrainingCourse t) => new()
        {
            Kind = "Training", Reference = t.Reference, Title = t.Title, Subtitle = $"{t.Type}",
            Url = $"/Training/Details/{t.Id}", Icon = "fa-graduation-cap"
        };
        public static EvidenceRef SupplierRef(Supplier s, int? score = null) => new()
        {
            Kind = "Supplier", Reference = s.Reference, Title = s.Name,
            Subtitle = $"{s.Status}" + (score.HasValue ? $" · score {score}" : ""), Url = $"/Suppliers/Details/{s.Id}",
            Icon = "fa-truck-field", Badge = s.Status.ToString()
        };
        public static EvidenceRef ObjectiveRef(Objective o) => new()
        {
            Kind = "Objective", Reference = o.Reference, Title = o.Title, Subtitle = $"{o.Status}",
            Url = $"/Objectives/Details/{o.Id}", Icon = "fa-bullseye", Badge = o.Status.ToString()
        };
        public static EvidenceRef ClauseRef(IsoClause c) => new()
        {
            Kind = "Clause", Reference = c.ClauseNumber, Title = c.Title,
            Subtitle = IsoStandards.Label(c.Standard), Icon = "fa-list-ol", Badge = "ISO"
        };
        public static EvidenceRef EvidenceItem(IsoEvidence e) => new()
        {
            Kind = "Evidence", Reference = e.Reference, Title = e.Title,
            Subtitle = $"{e.Type}" + (e.IsoClause != null ? $" · clause {e.IsoClause}" : ""),
            Url = $"/Evidence/Details/{e.Id}", Icon = "fa-paperclip"
        };

        // ── Approved-document search (authoritative sources) ────────────────────────
        public async Task<List<IsoDocument>> SearchApprovedDocumentsAsync(string? role, List<string> tokens, int? departmentId = null, DocumentType? type = null)
        {
            var published = await _db.IsoDocuments.Include(d => d.Category).Include(d => d.Department)
                .Where(d => d.Status == DocumentStatus.Published).ToListAsync();
            published = published.Where(d => CanSeeDocument(role, d)).ToList();
            if (departmentId.HasValue) published = published.Where(d => d.DepartmentId == departmentId).ToList();
            if (type.HasValue) published = published.Where(d => d.Type == type).ToList();
            if (tokens.Count == 0) return published;

            return published
                .Select(d => (Doc: d, Hits: tokens.Count(t =>
                    d.Title.ToLowerInvariant().Contains(t) ||
                    (d.Keywords ?? "").ToLowerInvariant().Contains(t) ||
                    (d.Summary ?? "").ToLowerInvariant().Contains(t))))
                .Where(x => x.Hits > 0)
                .OrderByDescending(x => x.Hits)
                .Select(x => x.Doc).ToList();
        }

        // ── Clause expansion (ISO consultant / evidence generation) ─────────────────
        public async Task<(List<IsoClause> Clauses, List<IsoDocument> Docs, List<IsoEvidence> Evidence, List<AuditFinding> Findings)>
            ExpandClauseAsync(string? role, string clause)
        {
            var clauses = await _db.IsoClauses.Where(c => c.ClauseNumber == clause || c.ClauseNumber.StartsWith(clause + ".")).ToListAsync();
            var docs = (await _db.IsoDocuments.Include(d => d.Department)
                .Where(d => d.Status == DocumentStatus.Published && d.IsoClause != null && d.IsoClause.Contains(clause)).ToListAsync())
                .Where(d => CanSeeDocument(role, d)).ToList();
            var evidence = await _db.IsoEvidences.Where(e => e.IsoClause != null && e.IsoClause.Contains(clause)).ToListAsync();
            var findings = await _db.AuditFindings.Include(f => f.Audit)
                .Where(f => f.ClauseReference != null && f.ClauseReference.Contains(clause)).ToListAsync();
            return (clauses, docs, evidence, findings);
        }

        // ── Department expansion ────────────────────────────────────────────────────
        public async Task<(List<IsoDocument> Docs, List<Risk> Risks, List<Capa> Capas, List<Objective> Objectives, List<AuditFinding> Findings)>
            ExpandDepartmentAsync(string? role, int deptId)
        {
            var docs = (await _db.IsoDocuments.Where(d => d.Status == DocumentStatus.Published && d.DepartmentId == deptId).ToListAsync())
                .Where(d => CanSeeDocument(role, d)).ToList();
            var risks = await _db.Risks.Include(r => r.Owner).Where(r => r.DepartmentId == deptId && r.Status != RiskStatus.Closed).ToListAsync();
            var capas = await _db.Capas.Include(c => c.Responsible).Where(c => c.DepartmentId == deptId).ToListAsync();
            var objectives = await _db.Objectives.Where(o => o.DepartmentId == deptId).ToListAsync();
            var findings = await _db.AuditFindings.Include(f => f.Audit).Where(f => f.DepartmentId == deptId).ToListAsync();
            return (docs, risks, capas, objectives, findings);
        }

        // ── Visual graph around a document (evidence graph view) ────────────────────
        public async Task<EvidenceGraph> BuildDocumentGraphAsync(string? role, int docId)
        {
            var g = new EvidenceGraph();
            var doc = await _db.IsoDocuments.Include(d => d.Department).FirstOrDefaultAsync(d => d.Id == docId);
            if (doc == null) return g;

            var root = $"doc-{doc.Id}";
            g.Nodes.Add(new GraphNode(root, "Document", $"{doc.DocumentNumber}\n{doc.Title}", "fa-file-shield"));

            if (doc.Department != null)
            {
                var dn = $"dept-{doc.DepartmentId}";
                g.Nodes.Add(new GraphNode(dn, "Department", doc.Department.Name, "fa-building"));
                g.Edges.Add(new GraphEdge(root, dn, "owned by"));
            }

            // Training linked to this document
            foreach (var t in await _db.TrainingCourses.Where(t => t.LinkedDocumentId == doc.Id).ToListAsync())
            {
                var id = $"trn-{t.Id}";
                g.Nodes.Add(new GraphNode(id, "Training", t.Title, "fa-graduation-cap"));
                g.Edges.Add(new GraphEdge(root, id, "trains"));
            }
            // Clause-linked evidence, findings, capas
            if (!string.IsNullOrWhiteSpace(doc.IsoClause))
            {
                var clause = doc.IsoClause!;
                foreach (var e in await _db.IsoEvidences.Where(e => e.IsoClause != null && e.IsoClause.Contains(clause)).Take(6).ToListAsync())
                {
                    var id = $"evd-{e.Id}";
                    g.Nodes.Add(new GraphNode(id, "Evidence", e.Title, "fa-paperclip"));
                    g.Edges.Add(new GraphEdge(root, id, "evidenced by"));
                }
                foreach (var f in await _db.AuditFindings.Where(f => f.ClauseReference != null && f.ClauseReference.Contains(clause)).Take(6).ToListAsync())
                {
                    var id = $"fnd-{f.Id}";
                    g.Nodes.Add(new GraphNode(id, "Finding", f.Reference, "fa-magnifying-glass"));
                    g.Edges.Add(new GraphEdge(root, id, "audited by"));
                    if (f.CapaId.HasValue)
                    {
                        var cid = $"capa-{f.CapaId}";
                        if (g.Nodes.All(n => n.Id != cid))
                        {
                            var capa = await _db.Capas.FindAsync(f.CapaId);
                            g.Nodes.Add(new GraphNode(cid, "CAPA", capa?.Reference ?? "CAPA", "fa-screwdriver-wrench"));
                        }
                        g.Edges.Add(new GraphEdge(id, cid, "corrected by"));
                    }
                }
            }
            return g;
        }
    }
}
