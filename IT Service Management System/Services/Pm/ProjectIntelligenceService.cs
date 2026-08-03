using System.Text;
using System.Text.RegularExpressions;
using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models.Pm;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Services.Pm
{
    /// <summary>
    /// The project module's assistive layer. Every answer here is derived from the organisation's
    /// own data using explicit statistics and rules — no external model is called, so results are
    /// reproducible, explainable, and safe to show in a board pack. The reasoning behind each
    /// number is returned alongside it.
    /// </summary>
    public class ProjectIntelligenceService
    {
        private readonly ApplicationDbContext _db;
        private readonly ProjectMetricsService _metrics;

        public ProjectIntelligenceService(ApplicationDbContext db, ProjectMetricsService metrics)
        {
            _db = db; _metrics = metrics;
        }

        // ── Forecasting ──────────────────────────────────────────────────────────

        /// <summary>
        /// Predict the finish date from the rate progress has actually been made, rather than from
        /// the plan. Uses the project's own velocity when there is enough history, otherwise the
        /// organisation's historical average for projects of the same category.
        /// </summary>
        public async Task<Forecast> ForecastCompletionAsync(int projectId)
        {
            var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null) return new Forecast { Basis = "Project not found." };

            if (project.ProgressPercent >= 100)
                return new Forecast
                {
                    PredictedDate = project.ActualEndDate ?? DateTime.Today,
                    Confidence = "High",
                    Basis = "The project is already complete."
                };

            var start = project.ActualStartDate ?? project.StartDate;
            if (start == null || project.ProgressPercent <= 0)
                return new Forecast
                {
                    PredictedDate = project.EndDate,
                    Confidence = "Low",
                    Basis = "No progress recorded yet — the planned end date is the only available estimate."
                };

            var elapsedDays = Math.Max(1, (DateTime.Today - start.Value.Date).TotalDays);
            var percentPerDay = project.ProgressPercent / elapsedDays;

            if (percentPerDay <= 0.01)
                return new Forecast
                {
                    PredictedDate = null,
                    Confidence = "Low",
                    Basis = $"Progress has effectively stalled ({project.ProgressPercent}% after {elapsedDays:N0} days). No credible finish date can be projected.",
                    IsStalled = true
                };

            var remainingDays = (100 - project.ProgressPercent) / percentPerDay;
            var predicted = DateTime.Today.AddDays(remainingDays);

            // Confidence rises with how much of the project has been observed.
            var confidence = project.ProgressPercent switch
            {
                >= 50 => "High",
                >= 20 => "Medium",
                _ => "Low"
            };

            var slip = project.EndDate.HasValue ? (int)(predicted.Date - project.EndDate.Value.Date).TotalDays : 0;

            return new Forecast
            {
                PredictedDate = predicted.Date,
                Confidence = confidence,
                SlipDays = slip,
                Basis = $"{project.ProgressPercent}% complete in {elapsedDays:N0} days is {percentPerDay:F2}% per day. " +
                        $"At that rate the remaining {100 - project.ProgressPercent}% takes about {remainingDays:N0} more days." +
                        (slip > 0 ? $" That is {slip} days past the planned end date." :
                         slip < 0 ? $" That is {-slip} days ahead of the planned end date." : "")
            };
        }

        /// <summary>
        /// Project the final spend by extrapolating the current burn rate against progress made —
        /// the cost-performance view of earned value, expressed in plain terms.
        /// </summary>
        public async Task<BudgetForecast> ForecastBudgetAsync(int projectId)
        {
            var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null) return new BudgetForecast { Basis = "Project not found." };

            var spent = await _metrics.ActualSpendAsync(projectId);
            var committed = await _metrics.CommittedSpendAsync(projectId);
            var budget = project.TotalBudget;

            if (budget <= 0)
                return new BudgetForecast
                {
                    Spent = spent, Committed = committed,
                    Basis = "No budget has been set, so overrun cannot be assessed."
                };

            if (project.ProgressPercent <= 0)
                return new BudgetForecast
                {
                    Budget = budget, Spent = spent, Committed = committed, Forecast = budget,
                    Basis = "No progress recorded yet — the approved budget stands as the forecast."
                };

            // Cost per point of progress, extrapolated to 100%.
            var costPerPercent = spent / project.ProgressPercent;
            var forecast = Math.Round(costPerPercent * 100, 2);
            var variance = budget - forecast;
            var overrunPercent = budget <= 0 ? 0 : (int)Math.Round((forecast - budget) / budget * 100);

            var risk = overrunPercent switch
            {
                >= 20 => "High",
                >= 5 => "Medium",
                _ => "Low"
            };

            return new BudgetForecast
            {
                Budget = budget,
                Spent = spent,
                Committed = committed,
                Forecast = forecast,
                Variance = variance,
                OverrunRiskLevel = risk,
                Basis = $"{spent:N0} spent to deliver {project.ProgressPercent}% works out at {costPerPercent:N0} per percentage point. " +
                        $"Extrapolated to completion that is {forecast:N0} against a budget of {budget:N0}" +
                        (variance < 0 ? $" — an overrun of {-variance:N0} ({overrunPercent}%)." : $" — {variance:N0} under budget.") +
                        (committed > 0 ? $" A further {committed:N0} is committed on open purchase orders." : "")
            };
        }

        // ── Suggestions ──────────────────────────────────────────────────────────

        /// <summary>
        /// Propose a work breakdown for a project from the closest matching template, then from
        /// what comparable past projects in the same category actually did.
        /// </summary>
        public async Task<List<WbsSuggestion>> SuggestWbsAsync(ProjectCategory category, ProjectType type)
        {
            // 1 · A template for the same category is the strongest signal.
            var template = await _db.ProjectTemplates.AsNoTracking()
                .Where(t => t.IsActive && t.Category == category)
                .OrderByDescending(t => t.IsSystem)
                .FirstOrDefaultAsync();

            if (template != null)
            {
                var items = await _db.ProjectTemplateItems.AsNoTracking()
                    .Where(i => i.TemplateId == template.Id)
                    .OrderBy(i => i.Sequence)
                    .ToListAsync();

                return items.Select(i => new WbsSuggestion
                {
                    Name = i.Name,
                    ItemType = i.ItemType,
                    DurationDays = i.DurationDays,
                    EstimatedHours = i.EstimatedHours,
                    Source = $"Template “{template.Name}”"
                }).ToList();
            }

            // 2 · Otherwise, the phases most commonly used on past projects of this category.
            var pastPhases = await _db.ProjectPhases.AsNoTracking()
                .Where(p => p.Project!.Category == category)
                .Select(p => new { p.Name, p.StartDate, p.EndDate })
                .ToListAsync();

            return pastPhases
                .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() >= 2)
                .OrderByDescending(g => g.Count())
                .Take(8)
                .Select(g => new WbsSuggestion
                {
                    Name = g.Key,
                    ItemType = "Phase",
                    DurationDays = (int)g.Where(x => x.StartDate != null && x.EndDate != null)
                        .Select(x => (x.EndDate!.Value - x.StartDate!.Value).TotalDays)
                        .DefaultIfEmpty(14).Average(),
                    Source = $"Used on {g.Count()} previous {category} projects"
                })
                .ToList();
        }

        /// <summary>
        /// Rank a project's open tasks by urgency. Scores combine the due date, priority, whether
        /// the task sits on the critical path, and whether anything is blocked behind it.
        /// </summary>
        public async Task<List<TaskPrioritySuggestion>> PrioritiseTasksAsync(int projectId, int take = 10)
        {
            var tasks = await _db.ProjectTasks.AsNoTracking()
                .Include(t => t.AssignedTo)
                .Where(t => t.ProjectId == projectId
                            && t.Status != ProjectTaskStatus.Completed && t.Status != ProjectTaskStatus.Cancelled)
                .ToListAsync();
            if (tasks.Count == 0) return new List<TaskPrioritySuggestion>();

            var taskIds = tasks.Select(t => t.Id).ToList();
            var blockedCounts = await _db.TaskDependencies
                .Where(d => taskIds.Contains(d.PredecessorTaskId))
                .GroupBy(d => d.PredecessorTaskId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            return tasks.Select(t =>
            {
                var score = 0;
                var reasons = new List<string>();

                if (t.DueDate.HasValue)
                {
                    var days = (t.DueDate.Value.Date - DateTime.Today).TotalDays;
                    if (days < 0) { score += 40; reasons.Add($"overdue by {-days:N0} days"); }
                    else if (days <= 3) { score += 30; reasons.Add("due within 3 days"); }
                    else if (days <= 7) { score += 18; reasons.Add("due this week"); }
                    else if (days <= 14) { score += 8; reasons.Add("due within a fortnight"); }
                }

                score += t.Priority switch
                {
                    TaskPriority.Critical => 30,
                    TaskPriority.High => 20,
                    TaskPriority.Medium => 8,
                    _ => 0
                };
                if (t.Priority >= TaskPriority.High) reasons.Add($"{t.Priority.ToString().ToLower()} priority");

                if (t.IsOnCriticalPath) { score += 25; reasons.Add("on the critical path"); }

                var blocking = blockedCounts.GetValueOrDefault(t.Id);
                if (blocking > 0)
                {
                    score += Math.Min(20, blocking * 7);
                    reasons.Add($"blocking {blocking} other task{(blocking == 1 ? "" : "s")}");
                }

                if (t.Status == ProjectTaskStatus.Blocked) { score += 12; reasons.Add("currently blocked"); }
                if (t.AssignedToId == null) { score += 10; reasons.Add("unassigned"); }

                return new TaskPrioritySuggestion
                {
                    TaskId = t.Id,
                    Name = t.Name,
                    Assignee = t.AssignedTo == null ? null : $"{t.AssignedTo.FirstName} {t.AssignedTo.LastName}",
                    DueDate = t.DueDate,
                    Score = score,
                    Reason = reasons.Count == 0 ? "No urgency signals" : string.Join(", ", reasons)
                };
            })
            .OrderByDescending(s => s.Score)
            .Take(take)
            .ToList();
        }

        /// <summary>
        /// Flag risks the project is likely to face, based on its own numbers and on what has
        /// actually gone wrong on comparable projects. Each prediction carries a suggested response.
        /// </summary>
        public async Task<List<RiskPrediction>> PredictRisksAsync(int projectId)
        {
            var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null) return new List<RiskPrediction>();

            var predictions = new List<RiskPrediction>();

            // ── Schedule ──
            var variance = project.ProgressPercent - project.SchedulePercentElapsed;
            if (variance <= -10)
                predictions.Add(new RiskPrediction
                {
                    Title = "Schedule slippage",
                    Likelihood = variance <= -25 ? "High" : "Medium",
                    Evidence = $"{project.ProgressPercent}% delivered against {project.SchedulePercentElapsed}% of the schedule elapsed — {-variance} points behind.",
                    SuggestedMitigation = "Re-baseline the plan or add capacity to the critical path. Review scope with the sponsor before the gap widens."
                });

            // ── Budget ──
            var budgetForecast = await ForecastBudgetAsync(projectId);
            if (budgetForecast.OverrunRiskLevel is "High" or "Medium")
                predictions.Add(new RiskPrediction
                {
                    Title = "Budget overrun",
                    Likelihood = budgetForecast.OverrunRiskLevel,
                    Evidence = budgetForecast.Basis,
                    SuggestedMitigation = "Freeze discretionary spend, re-forecast the remaining budget lines, and raise a change request if the overrun is structural."
                });

            // ── Resourcing ──
            var overallocated = (await _metrics.ResourceWorkloadAsync(DateTime.Today, DateTime.Today.AddDays(28), 50))
                .Count(r => r.IsOverallocated);
            if (overallocated > 0)
                predictions.Add(new RiskPrediction
                {
                    Title = "Resource over-allocation",
                    Likelihood = overallocated >= 3 ? "High" : "Medium",
                    Evidence = $"{overallocated} resource{(overallocated == 1 ? " is" : "s are")} booked above capacity over the next four weeks.",
                    SuggestedMitigation = "Level the workload across the team, stagger overlapping assignments, or bring in additional capacity."
                });

            // ── Unassigned work ──
            var unassigned = await _db.ProjectTasks.CountAsync(t =>
                t.ProjectId == projectId && t.AssignedToId == null
                && t.Status != ProjectTaskStatus.Completed && t.Status != ProjectTaskStatus.Cancelled);
            if (unassigned >= 5)
                predictions.Add(new RiskPrediction
                {
                    Title = "Unowned work",
                    Likelihood = unassigned >= 15 ? "High" : "Medium",
                    Evidence = $"{unassigned} open tasks have no owner.",
                    SuggestedMitigation = "Assign owners at the next stand-up — unowned tasks are the most common source of silent slippage."
                });

            // ── Blocked work ──
            var blocked = await _db.ProjectTasks.CountAsync(t =>
                t.ProjectId == projectId && t.Status == ProjectTaskStatus.Blocked);
            if (blocked > 0)
                predictions.Add(new RiskPrediction
                {
                    Title = "Blocked delivery",
                    Likelihood = blocked >= 3 ? "High" : "Medium",
                    Evidence = $"{blocked} task{(blocked == 1 ? " is" : "s are")} flagged as blocked.",
                    SuggestedMitigation = "Escalate the blockers to the sponsor. Each blocked task is holding downstream work hostage."
                });

            // ── Learned from comparable projects ──
            var peerRisks = await _db.ProjectRisks.AsNoTracking()
                .Where(r => r.Project!.Category == project.Category && r.ProjectId != projectId
                            && (r.Status == PmRiskStatus.Realised || r.Probability * r.Impact >= 15))
                .Select(r => r.Title)
                .ToListAsync();

            foreach (var group in peerRisks
                         .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
                         .Where(g => g.Count() >= 2)
                         .OrderByDescending(g => g.Count())
                         .Take(3))
            {
                predictions.Add(new RiskPrediction
                {
                    Title = group.Key,
                    Likelihood = group.Count() >= 4 ? "High" : "Medium",
                    Evidence = $"Recorded on {group.Count()} other {project.Category} projects.",
                    SuggestedMitigation = "Review how this was handled on the comparable projects and add it to the register early."
                });
            }

            return predictions;
        }

        /// <summary>
        /// Suggest who should take a task, ranked by declared skills, current spare capacity and
        /// whether the person is already on the project team.
        /// </summary>
        public async Task<List<AllocationSuggestion>> SuggestAssigneesAsync(int projectId, string? requiredSkills, int take = 5)
        {
            var workload = await _metrics.ResourceWorkloadAsync(DateTime.Today, DateTime.Today.AddDays(28), 200);
            var utilisation = workload.ToDictionary(w => w.ResourceId, w => w.UtilisationPercent);

            var people = await _db.Resources.AsNoTracking()
                .Include(r => r.User)
                .Where(r => r.IsActive && r.Type == ResourceType.Person && r.UserId != null)
                .ToListAsync();

            var teamUserIds = await _db.ProjectTeamMembers.AsNoTracking()
                .Where(m => m.ProjectId == projectId && m.IsActive)
                .Select(m => m.UserId)
                .ToListAsync();

            var wanted = (requiredSkills ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => s.Length > 1)
                .ToList();

            return people.Select(r =>
            {
                var score = 0;
                var reasons = new List<string>();

                var matched = wanted.Where(w =>
                    (r.Skills ?? "").Contains(w, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matched.Count > 0)
                {
                    score += matched.Count * 25;
                    reasons.Add($"matches {string.Join(", ", matched)}");
                }

                var used = utilisation.GetValueOrDefault(r.Id, 0);
                if (used < 70) { score += 25; reasons.Add($"{100 - used}% spare capacity"); }
                else if (used <= 100) { score += 10; reasons.Add($"{Math.Max(0, 100 - used)}% spare capacity"); }
                else { score -= 20; reasons.Add($"already over-allocated ({used}%)"); }

                if (r.UserId is int uid && teamUserIds.Contains(uid))
                {
                    score += 15;
                    reasons.Add("already on this project team");
                }

                return new AllocationSuggestion
                {
                    ResourceId = r.Id,
                    UserId = r.UserId,
                    Name = r.User != null ? $"{r.User.FirstName} {r.User.LastName}" : r.Name,
                    Skills = r.Skills,
                    UtilisationPercent = used,
                    Score = score,
                    Reason = reasons.Count == 0 ? "Available" : string.Join(", ", reasons)
                };
            })
            .OrderByDescending(s => s.Score)
            .Take(take)
            .ToList();
        }

        // ── Summarisation ────────────────────────────────────────────────────────

        /// <summary>
        /// Turn free-form meeting minutes into a summary plus a list of candidate action items.
        /// Sentences carrying a commitment verb ("will", "to action", "agreed to"…) are extracted,
        /// along with any owner named at the start and any date mentioned.
        /// </summary>
        public MinutesSummary SummariseMinutes(string? minutes)
        {
            var result = new MinutesSummary();
            if (string.IsNullOrWhiteSpace(minutes)) return result;

            var sentences = Regex.Split(minutes, @"(?<=[.!?])\s+|\r?\n")
                .Select(s => s.Trim())
                .Where(s => s.Length > 10)
                .ToList();

            string[] commitmentMarkers =
            {
                "will ", "to action", "action:", "agreed to", "to follow up", "responsible for",
                "to deliver", "to provide", "to prepare", "to review", "must ", "shall ", "to confirm",
                "owner:", "assigned to", "due by", "by end of"
            };

            string[] decisionMarkers = { "agreed that", "decided", "resolution", "approved", "signed off", "rejected" };

            foreach (var sentence in sentences)
            {
                var lower = sentence.ToLowerInvariant();

                if (decisionMarkers.Any(m => lower.Contains(m)))
                    result.Decisions.Add(sentence);

                if (commitmentMarkers.Any(m => lower.Contains(m)))
                {
                    // "Jane Moyo will circulate the report by Friday" → owner "Jane Moyo".
                    var owner = Regex.Match(sentence, @"^([A-Z][a-z]+(?:\s+[A-Z][a-z]+)?)\s+(will|to|shall|must)\b");
                    var due = Regex.Match(sentence,
                        @"\b(\d{1,2}[/-]\d{1,2}[/-]\d{2,4}|\d{1,2}\s+\w+\s+\d{4}|next week|end of (?:the )?(?:week|month)|Monday|Tuesday|Wednesday|Thursday|Friday)\b",
                        RegexOptions.IgnoreCase);

                    result.ActionItems.Add(new ExtractedAction
                    {
                        Description = sentence,
                        SuggestedOwner = owner.Success ? owner.Groups[1].Value : null,
                        SuggestedDue = due.Success ? due.Value : null
                    });
                }
            }

            // The summary is the opening context plus every decision reached.
            var summary = new StringBuilder();
            foreach (var s in sentences.Take(2)) summary.AppendLine(s);
            if (result.Decisions.Count > 0)
            {
                summary.AppendLine();
                summary.AppendLine("Decisions:");
                foreach (var d in result.Decisions) summary.AppendLine($"• {d}");
            }
            if (result.ActionItems.Count > 0)
                summary.AppendLine($"\n{result.ActionItems.Count} action item(s) identified.");

            result.Summary = summary.ToString().Trim();
            return result;
        }

        /// <summary>
        /// A management-ready status narrative for one project: where it stands, where it is going,
        /// and what needs a decision. Every sentence is backed by a figure from the record.
        /// </summary>
        public async Task<string> ExecutiveSummaryAsync(int projectId)
        {
            var project = await _db.Projects.AsNoTracking()
                .Include(p => p.ProjectManager).Include(p => p.Department)
                .FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null) return "Project not found.";

            var sb = new StringBuilder();
            var schedule = await ForecastCompletionAsync(projectId);
            var budget = await ForecastBudgetAsync(projectId);

            var openTasks = await _db.ProjectTasks.CountAsync(t => t.ProjectId == projectId && t.Status != ProjectTaskStatus.Completed && t.Status != ProjectTaskStatus.Cancelled);
            var overdueTasks = await _db.ProjectTasks.CountAsync(t => t.ProjectId == projectId && t.DueDate < DateTime.Today && t.Status != ProjectTaskStatus.Completed && t.Status != ProjectTaskStatus.Cancelled);
            var openRisks = await _db.ProjectRisks.CountAsync(r => r.ProjectId == projectId && r.Status != PmRiskStatus.Closed);
            var criticalRisks = await _db.ProjectRisks.CountAsync(r => r.ProjectId == projectId && r.Status != PmRiskStatus.Closed && r.Probability * r.Impact >= 15);
            var openIssues = await _db.ProjectIssues.CountAsync(i => i.ProjectId == projectId && i.Status != IssueStatus.Resolved && i.Status != IssueStatus.Closed);
            var nextMilestone = await _db.Milestones.AsNoTracking()
                .Where(m => m.ProjectId == projectId && m.Status == MilestoneStatus.Planned && m.DueDate >= DateTime.Today)
                .OrderBy(m => m.DueDate).FirstOrDefaultAsync();

            // ── Position ──
            sb.Append($"{project.Reference} — {project.Name} is {project.Status} and {project.ProgressPercent}% complete");
            if (project.ProjectManager != null)
                sb.Append($", managed by {project.ProjectManager.FirstName} {project.ProjectManager.LastName}");
            sb.AppendLine(". ");

            sb.AppendLine($"Health is rated {project.Health}. " +
                $"{project.SchedulePercentElapsed}% of the planned schedule has elapsed, " +
                (project.ProgressPercent >= project.SchedulePercentElapsed
                    ? "so delivery is on or ahead of plan."
                    : $"so delivery is {project.SchedulePercentElapsed - project.ProgressPercent} points behind plan."));

            // ── Schedule outlook ──
            if (schedule.PredictedDate.HasValue)
                sb.AppendLine($"Projected completion is {schedule.PredictedDate:d MMM yyyy} ({schedule.Confidence.ToLower()} confidence)" +
                    (schedule.SlipDays > 0 ? $", {schedule.SlipDays} days later than planned." : "."));
            else if (schedule.IsStalled)
                sb.AppendLine("Progress has stalled and no completion date can currently be projected.");

            // ── Financial position ──
            if (budget.Budget > 0)
                sb.AppendLine($"Spend stands at {budget.Spent:N0} of a {budget.Budget:N0} budget " +
                    $"({(int)Math.Round(budget.Spent / budget.Budget * 100)}% used). " +
                    (budget.Variance < 0
                        ? $"The current burn rate points to an overrun of about {-budget.Variance:N0}."
                        : $"On present trends the project finishes about {budget.Variance:N0} under budget."));

            // ── Work in flight ──
            sb.AppendLine($"{openTasks} task(s) remain open" + (overdueTasks > 0 ? $", of which {overdueTasks} are overdue." : "."));

            if (nextMilestone != null)
                sb.AppendLine($"The next milestone is “{nextMilestone.Name}” on {nextMilestone.DueDate:d MMM yyyy}.");

            // ── What needs a decision ──
            var attention = new List<string>();
            if (criticalRisks > 0) attention.Add($"{criticalRisks} high-scoring risk(s)");
            if (openIssues > 0) attention.Add($"{openIssues} open issue(s)");
            if (overdueTasks > 0) attention.Add($"{overdueTasks} overdue task(s)");
            if (budget.Variance < 0) attention.Add("a forecast budget overrun");

            sb.AppendLine(attention.Count > 0
                ? $"Requiring management attention: {string.Join(", ", attention)}."
                : "Nothing currently requires escalation.");

            if (openRisks > 0 && criticalRisks == 0)
                sb.AppendLine($"{openRisks} risk(s) are being tracked, none at critical level.");

            return sb.ToString().Trim();
        }

        // ── Natural-language search ──────────────────────────────────────────────

        /// <summary>
        /// Answer plain-English portfolio questions such as "projects delayed by more than two
        /// weeks" or "active construction projects over 50000". The phrase is parsed into explicit
        /// filters, which are shown back to the user so the result is never a black box.
        /// </summary>
        public async Task<NlSearchResult> SearchAsync(string? query)
        {
            var result = new NlSearchResult { Query = query ?? "" };
            var q = (query ?? "").ToLowerInvariant().Trim();
            if (q.Length == 0) return result;

            IQueryable<Project> projects = _db.Projects.AsNoTracking()
                .Include(p => p.ProjectManager).Include(p => p.Department);

            // ── Status ──
            foreach (var status in Enum.GetValues<ProjectStatus>())
            {
                var word = SplitCamelCase(status.ToString()).ToLowerInvariant();
                if (q.Contains(word))
                {
                    projects = projects.Where(p => p.Status == status);
                    result.AppliedFilters.Add($"status is {status}");
                    break;
                }
            }

            // ── Category ──
            foreach (var category in Enum.GetValues<ProjectCategory>())
            {
                var word = SplitCamelCase(category.ToString()).ToLowerInvariant();
                if (q.Contains(word))
                {
                    projects = projects.Where(p => p.Category == category);
                    result.AppliedFilters.Add($"category is {category}");
                    break;
                }
            }

            // ── Health ──
            if (q.Contains("at risk") || q.Contains("amber"))
            {
                projects = projects.Where(p => p.Health == ProjectHealth.Amber);
                result.AppliedFilters.Add("health is Amber");
            }
            else if (q.Contains("unhealthy") || q.Contains("red") || q.Contains("in trouble"))
            {
                projects = projects.Where(p => p.Health == ProjectHealth.Red);
                result.AppliedFilters.Add("health is Red");
            }

            // ── "delayed by more than N weeks/days/months" ──
            var delayMatch = Regex.Match(q, @"(?:delayed|late|overdue)(?:\s+by)?\s+(?:more than\s+|over\s+)?(\d+)\s*(day|week|month)");
            if (delayMatch.Success)
            {
                var n = int.Parse(delayMatch.Groups[1].Value);
                var days = delayMatch.Groups[2].Value switch { "week" => n * 7, "month" => n * 30, _ => n };
                var cutoff = DateTime.Today.AddDays(-days);
                projects = projects.Where(p => p.EndDate != null && p.EndDate < cutoff
                    && p.Status != ProjectStatus.Completed && p.Status != ProjectStatus.Cancelled && p.Status != ProjectStatus.Archived);
                result.AppliedFilters.Add($"more than {days} days past the end date");
            }
            else if (q.Contains("delayed") || q.Contains("overdue") || q.Contains("late"))
            {
                projects = projects.Where(p => p.EndDate != null && p.EndDate < DateTime.Today
                    && p.Status != ProjectStatus.Completed && p.Status != ProjectStatus.Cancelled && p.Status != ProjectStatus.Archived);
                result.AppliedFilters.Add("past the planned end date");
            }

            // ── "over 50000" / "under 10000" ──
            var overMatch = Regex.Match(q, @"(?:over|above|more than|greater than)\s+\$?([\d,]+)");
            if (overMatch.Success && decimal.TryParse(overMatch.Groups[1].Value.Replace(",", ""), out var over))
            {
                projects = projects.Where(p => p.Budget + p.ApprovedChangeValue > over);
                result.AppliedFilters.Add($"budget over {over:N0}");
            }
            var underMatch = Regex.Match(q, @"(?:under|below|less than)\s+\$?([\d,]+)");
            if (underMatch.Success && decimal.TryParse(underMatch.Groups[1].Value.Replace(",", ""), out var under))
            {
                projects = projects.Where(p => p.Budget + p.ApprovedChangeValue < under);
                result.AppliedFilters.Add($"budget under {under:N0}");
            }

            // ── "due this month" / "ending next month" ──
            if (q.Contains("this month"))
            {
                var from = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                var to = from.AddMonths(1);
                projects = projects.Where(p => p.EndDate >= from && p.EndDate < to);
                result.AppliedFilters.Add("ending this month");
            }
            else if (q.Contains("next month"))
            {
                var from = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1);
                var to = from.AddMonths(1);
                projects = projects.Where(p => p.EndDate >= from && p.EndDate < to);
                result.AppliedFilters.Add("ending next month");
            }

            // ── Over budget ──
            if (q.Contains("over budget") || q.Contains("overspent"))
            {
                var spend = await _metrics.SpendByProjectAsync();
                var overspent = spend.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList();
                var loaded = await projects.Where(p => overspent.Contains(p.Id)).ToListAsync();
                result.Projects = loaded.Where(p => spend.GetValueOrDefault(p.Id) > p.TotalBudget && p.TotalBudget > 0).ToList();
                result.AppliedFilters.Add("actual spend exceeds the budget");
                result.Explanation = BuildExplanation(result);
                return result;
            }

            // ── Free-text fallback so a name or client always matches something ──
            if (result.AppliedFilters.Count == 0)
            {
                var term = q;
                projects = projects.Where(p =>
                    p.Name.Contains(term) || p.Code.Contains(term) ||
                    (p.Client != null && p.Client.Contains(term)) ||
                    (p.Description != null && p.Description.Contains(term)));
                result.AppliedFilters.Add($"name, code, client or description contains “{query}”");
            }

            result.Projects = await projects.OrderByDescending(p => p.CreatedAt).Take(50).ToListAsync();
            result.Explanation = BuildExplanation(result);
            return result;
        }

        private static string BuildExplanation(NlSearchResult result) =>
            $"Interpreted as: {string.Join("; ", result.AppliedFilters)}. " +
            $"{result.Projects.Count} project(s) matched.";

        private static string SplitCamelCase(string value) =>
            Regex.Replace(value, "(?<=[a-z])(?=[A-Z])", " ");
    }

    // ── Result types ─────────────────────────────────────────────────────────────

    public class Forecast
    {
        public DateTime? PredictedDate { get; set; }
        public string Confidence { get; set; } = "Low";
        public int SlipDays { get; set; }
        public bool IsStalled { get; set; }
        public string Basis { get; set; } = string.Empty;
    }

    public class BudgetForecast
    {
        public decimal Budget { get; set; }
        public decimal Spent { get; set; }
        public decimal Committed { get; set; }
        public decimal Forecast { get; set; }
        public decimal Variance { get; set; }
        public string OverrunRiskLevel { get; set; } = "Low";
        public string Basis { get; set; } = string.Empty;
    }

    public class WbsSuggestion
    {
        public string Name { get; set; } = string.Empty;
        public string ItemType { get; set; } = "Task";
        public int DurationDays { get; set; }
        public decimal EstimatedHours { get; set; }
        public string Source { get; set; } = string.Empty;
    }

    public class TaskPrioritySuggestion
    {
        public int TaskId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Assignee { get; set; }
        public DateTime? DueDate { get; set; }
        public int Score { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class RiskPrediction
    {
        public string Title { get; set; } = string.Empty;
        public string Likelihood { get; set; } = "Medium";
        public string Evidence { get; set; } = string.Empty;
        public string SuggestedMitigation { get; set; } = string.Empty;
    }

    public class AllocationSuggestion
    {
        public int ResourceId { get; set; }
        public int? UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Skills { get; set; }
        public int UtilisationPercent { get; set; }
        public int Score { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class MinutesSummary
    {
        public string Summary { get; set; } = string.Empty;
        public List<string> Decisions { get; set; } = new();
        public List<ExtractedAction> ActionItems { get; set; } = new();
    }

    public class ExtractedAction
    {
        public string Description { get; set; } = string.Empty;
        public string? SuggestedOwner { get; set; }
        public string? SuggestedDue { get; set; }
    }

    public class NlSearchResult
    {
        public string Query { get; set; } = string.Empty;
        public List<string> AppliedFilters { get; set; } = new();
        public string Explanation { get; set; } = string.Empty;
        public List<Project> Projects { get; set; } = new();
    }
}
