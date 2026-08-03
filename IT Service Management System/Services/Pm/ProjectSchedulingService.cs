using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models.Pm;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Services.Pm
{
    /// <summary>
    /// Schedule mathematics for the project plan: critical-path analysis over the task dependency
    /// graph, WBS outline numbering, and instantiating a project from a template.
    /// </summary>
    public class ProjectSchedulingService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<ProjectSchedulingService> _log;

        public ProjectSchedulingService(ApplicationDbContext db, ILogger<ProjectSchedulingService> log)
        {
            _db = db; _log = log;
        }

        // ── Critical path ────────────────────────────────────────────────────────

        /// <summary>
        /// Forward/backward pass over the project's tasks to find total float and flag the critical
        /// path. Only Finish-to-Start lag is modelled precisely; the other dependency types are
        /// treated as start-alignment constraints, which is enough for the Gantt view.
        /// Cycles in the graph are detected and ignored rather than hanging the pass.
        /// </summary>
        public async Task<int> RecalculateCriticalPathAsync(int projectId)
        {
            var tasks = await _db.ProjectTasks
                .Where(t => t.ProjectId == projectId && t.Status != ProjectTaskStatus.Cancelled)
                .ToListAsync();
            if (tasks.Count == 0) return 0;

            var taskIds = tasks.Select(t => t.Id).ToHashSet();
            var deps = await _db.TaskDependencies
                .Where(d => taskIds.Contains(d.TaskId) && taskIds.Contains(d.PredecessorTaskId))
                .ToListAsync();

            var byId = tasks.ToDictionary(t => t.Id);
            var predecessors = deps.GroupBy(d => d.TaskId)
                .ToDictionary(g => g.Key, g => g.ToList());
            var successors = deps.GroupBy(d => d.PredecessorTaskId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var order = TopologicalOrder(tasks.Select(t => t.Id).ToList(), predecessors);
            if (order == null)
            {
                _log.LogWarning("Circular task dependency detected on project {ProjectId}; critical path not computed.", projectId);
                return 0;
            }

            // Project day 0 = earliest planned start across the plan.
            var origin = tasks.Where(t => t.StartDate.HasValue).Select(t => t.StartDate!.Value).DefaultIfEmpty(DateTime.Today).Min().Date;

            int Duration(ProjectTask t)
            {
                if (t.StartDate.HasValue && t.DueDate.HasValue)
                    return Math.Max(1, (int)(t.DueDate.Value.Date - t.StartDate.Value.Date).TotalDays);
                // Fall back to the estimate at 8 hours a day.
                return Math.Max(1, (int)Math.Ceiling((double)t.EstimatedHours / 8));
            }

            // ── Forward pass: earliest start / earliest finish ──
            var earlyStart = new Dictionary<int, int>();
            var earlyFinish = new Dictionary<int, int>();
            foreach (var id in order)
            {
                var task = byId[id];
                var baseline = task.StartDate.HasValue
                    ? Math.Max(0, (int)(task.StartDate.Value.Date - origin).TotalDays)
                    : 0;

                var fromPredecessors = predecessors.TryGetValue(id, out var ps)
                    ? ps.Max(p => earlyFinish.GetValueOrDefault(p.PredecessorTaskId) + p.LagDays)
                    : 0;

                var es = Math.Max(baseline, fromPredecessors);
                earlyStart[id] = es;
                earlyFinish[id] = es + Duration(task);
            }

            var projectFinish = earlyFinish.Values.DefaultIfEmpty(0).Max();

            // ── Backward pass: latest start / latest finish ──
            var lateFinish = new Dictionary<int, int>();
            var lateStart = new Dictionary<int, int>();
            foreach (var id in Enumerable.Reverse(order))
            {
                var task = byId[id];
                var lf = successors.TryGetValue(id, out var ss) && ss.Count > 0
                    ? ss.Min(s => lateStart.GetValueOrDefault(s.TaskId, projectFinish) - s.LagDays)
                    : projectFinish;

                lateFinish[id] = lf;
                lateStart[id] = lf - Duration(task);
            }

            // ── Float and critical flag ──
            var criticalCount = 0;
            foreach (var task in tasks)
            {
                var slack = lateStart.GetValueOrDefault(task.Id) - earlyStart.GetValueOrDefault(task.Id);
                task.FloatDays = Math.Max(0, slack);
                task.IsOnCriticalPath = slack <= 0;
                if (task.IsOnCriticalPath) criticalCount++;
            }

            await _db.SaveChangesAsync();
            return criticalCount;
        }

        /// <summary>
        /// Kahn's algorithm. Returns null when the graph contains a cycle, so the caller can bail
        /// out instead of looping forever.
        /// </summary>
        private static List<int>? TopologicalOrder(List<int> nodes, Dictionary<int, List<TaskDependency>> predecessors)
        {
            var indegree = nodes.ToDictionary(n => n, n => predecessors.TryGetValue(n, out var p) ? p.Count : 0);
            var queue = new Queue<int>(indegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
            var result = new List<int>();

            // Successor lookup, built once.
            var successors = new Dictionary<int, List<int>>();
            foreach (var (node, deps) in predecessors)
                foreach (var dep in deps)
                {
                    if (!successors.TryGetValue(dep.PredecessorTaskId, out var list))
                        successors[dep.PredecessorTaskId] = list = new List<int>();
                    list.Add(node);
                }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                result.Add(current);
                if (!successors.TryGetValue(current, out var next)) continue;
                foreach (var s in next)
                    if (--indegree[s] == 0) queue.Enqueue(s);
            }

            return result.Count == nodes.Count ? result : null;
        }

        /// <summary>
        /// True when adding the proposed dependency would create a cycle. Called before saving a new
        /// predecessor link so the plan can never become unschedulable.
        /// </summary>
        public async Task<bool> WouldCreateCycleAsync(int taskId, int predecessorTaskId)
        {
            if (taskId == predecessorTaskId) return true;

            var projectId = await _db.ProjectTasks.Where(t => t.Id == taskId).Select(t => t.ProjectId).FirstOrDefaultAsync();
            var deps = await _db.TaskDependencies
                .Where(d => d.Task!.ProjectId == projectId)
                .Select(d => new { d.TaskId, d.PredecessorTaskId })
                .ToListAsync();

            // Walk back from the proposed predecessor; if we reach the dependent task, it is a cycle.
            var seen = new HashSet<int>();
            var stack = new Stack<int>();
            stack.Push(predecessorTaskId);
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                if (node == taskId) return true;
                if (!seen.Add(node)) continue;
                foreach (var d in deps.Where(x => x.TaskId == node))
                    stack.Push(d.PredecessorTaskId);
            }
            return false;
        }

        // ── WBS numbering ────────────────────────────────────────────────────────

        /// <summary>Recompute the "1.2.3" outline codes for a project's whole WBS tree.</summary>
        public async Task RenumberWbsAsync(int projectId)
        {
            var items = await _db.WbsItems.Where(w => w.ProjectId == projectId).ToListAsync();
            var byParent = items.GroupBy(w => w.ParentId)
                .ToDictionary(g => g.Key ?? 0, g => g.OrderBy(x => x.Sequence).ThenBy(x => x.Id).ToList());

            void Walk(int parentKey, string prefix)
            {
                if (!byParent.TryGetValue(parentKey, out var children)) return;
                for (var i = 0; i < children.Count; i++)
                {
                    var code = string.IsNullOrEmpty(prefix) ? $"{i + 1}" : $"{prefix}.{i + 1}";
                    children[i].WbsCode = code;
                    children[i].Sequence = i + 1;
                    Walk(children[i].Id, code);
                }
            }

            Walk(0, "");
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Roll leaf-level progress, hours and cost up the WBS tree so parent nodes summarise their
        /// children rather than needing to be maintained by hand.
        /// </summary>
        public async Task RollUpWbsAsync(int projectId)
        {
            var items = await _db.WbsItems.Where(w => w.ProjectId == projectId).ToListAsync();
            if (items.Count == 0) return;

            var children = items.GroupBy(w => w.ParentId).ToDictionary(g => g.Key ?? 0, g => g.ToList());

            // Deepest-first so a parent always sees settled children.
            (decimal hours, decimal cost, int progress) Compute(WbsItem node)
            {
                if (!children.TryGetValue(node.Id, out var kids) || kids.Count == 0)
                    return (node.EstimatedHours, node.EstimatedCost, node.ProgressPercent);

                decimal hours = 0, cost = 0, weighted = 0;
                foreach (var kid in kids)
                {
                    var (h, c, p) = Compute(kid);
                    hours += h; cost += c;
                    weighted += (h > 0 ? h : 1) * p;
                }
                var denominator = kids.Sum(k => k.EstimatedHours > 0 ? k.EstimatedHours : 1);
                node.EstimatedHours = hours;
                node.EstimatedCost = cost;
                node.ProgressPercent = denominator <= 0 ? 0 : (int)Math.Clamp(Math.Round(weighted / denominator), 0, 100);
                return (hours, cost, node.ProgressPercent);
            }

            foreach (var root in children.GetValueOrDefault(0, new List<WbsItem>()))
                Compute(root);

            await _db.SaveChangesAsync();
        }

        // ── Templates ────────────────────────────────────────────────────────────

        /// <summary>
        /// Materialise a template into a live project: create the phases, then the tasks and
        /// milestones underneath them, with all dates offset from the project start date.
        /// </summary>
        public async Task ApplyTemplateAsync(int projectId, int templateId, int actingUserId)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null) return;

            var items = await _db.ProjectTemplateItems.AsNoTracking()
                .Where(i => i.TemplateId == templateId)
                .OrderBy(i => i.Sequence)
                .ToListAsync();
            if (items.Count == 0) return;

            var start = project.StartDate ?? DateTime.Today;

            // Phases first, so tasks and milestones can attach to them by template sequence.
            var phaseBySequence = new Dictionary<int, ProjectPhase>();
            foreach (var item in items.Where(i => i.ItemType == "Phase"))
            {
                var phase = new ProjectPhase
                {
                    ProjectId = projectId,
                    Name = item.Name,
                    Description = item.Description,
                    Sequence = item.Sequence,
                    StartDate = start.AddDays(item.StartOffsetDays),
                    EndDate = start.AddDays(item.StartOffsetDays + item.DurationDays),
                    Status = PhaseStatus.NotStarted
                };
                _db.ProjectPhases.Add(phase);
                phaseBySequence[item.Sequence] = phase;
            }
            await _db.SaveChangesAsync();   // assign phase ids

            foreach (var item in items.Where(i => i.ItemType != "Phase"))
            {
                var phaseId = item.ParentSequence is int seq && phaseBySequence.TryGetValue(seq, out var phase)
                    ? phase.Id
                    : (int?)null;

                if (item.ItemType == "Milestone")
                {
                    _db.Milestones.Add(new Milestone
                    {
                        ProjectId = projectId,
                        PhaseId = phaseId,
                        Name = item.Name,
                        Description = item.Description,
                        DueDate = start.AddDays(item.StartOffsetDays),
                        BaselineDate = start.AddDays(item.StartOffsetDays),
                        Status = MilestoneStatus.Planned
                    });
                }
                else
                {
                    _db.ProjectTasks.Add(new ProjectTask
                    {
                        ProjectId = projectId,
                        PhaseId = phaseId,
                        Name = item.Name,
                        Description = item.Description,
                        StartDate = start.AddDays(item.StartOffsetDays),
                        DueDate = start.AddDays(item.StartOffsetDays + item.DurationDays),
                        BaselineStartDate = start.AddDays(item.StartOffsetDays),
                        BaselineDueDate = start.AddDays(item.StartOffsetDays + item.DurationDays),
                        EstimatedHours = item.EstimatedHours,
                        Status = ProjectTaskStatus.NotStarted,
                        Column = KanbanColumn.Backlog,
                        CreatedById = actingUserId,
                        CreatedAt = DateTime.Now
                    });
                }
            }

            project.CreatedFromTemplateId = templateId;
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Freeze the current plan as the baseline — task and milestone dates are copied into their
        /// baseline fields so slippage can be measured against the committed schedule.
        /// </summary>
        public async Task SetBaselineAsync(int projectId)
        {
            var tasks = await _db.ProjectTasks.Where(t => t.ProjectId == projectId).ToListAsync();
            foreach (var task in tasks)
            {
                task.BaselineStartDate = task.StartDate;
                task.BaselineDueDate = task.DueDate;
            }

            var milestones = await _db.Milestones.Where(m => m.ProjectId == projectId).ToListAsync();
            foreach (var milestone in milestones)
                milestone.BaselineDate = milestone.DueDate;

            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project != null) project.BaselineEndDate = project.EndDate;

            await _db.SaveChangesAsync();
        }
    }
}
