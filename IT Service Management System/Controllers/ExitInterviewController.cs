using IT_Service_Management_System.DbContexts;
using IT_Service_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IT_Service_Management_System.Controllers
{
    [IT_Service_Management_System.Filters.RoleAuthorize("Admin", "SystemsAdmin", "HR")]
    public class ExitInterviewController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ExitInterviewController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: ExitInterview
        public async Task<IActionResult> Index()
        {
            return View(await _context.ExitInterviews.ToListAsync());
        }

        // GET: ExitInterview/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var interview = await _context.ExitInterviews
                .FirstOrDefaultAsync(m => m.Id == id);

            if (interview == null)
                return NotFound();

            return View(interview);
        }

        // GET: ExitInterview/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: ExitInterview/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ExitInterview model)
        {
            if (ModelState.IsValid)
            {
                model.CreatedDate = DateTime.Now;

                _context.Add(model);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Exit Interview saved successfully.";

                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        // GET: ExitInterview/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var interview = await _context.ExitInterviews.FindAsync(id);

            if (interview == null)
                return NotFound();

            return View(interview);
        }

        // POST: ExitInterview/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ExitInterview model)
        {
            if (id != model.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                var existing = await _context.ExitInterviews.FindAsync(model.Id);
                if (existing == null)
                    return NotFound();

                // Employee Information
                existing.EmployeeName = model.EmployeeName;
                existing.Position = model.Position;
                existing.Client = model.Client;
                existing.DateOfResignation = model.DateOfResignation;
                existing.LastWorkingDay = model.LastWorkingDay;
                existing.InterviewConductedBy = model.InterviewConductedBy;
                existing.InterviewDate = model.InterviewDate;

                // Primary Reason for Leaving
                existing.PrimaryReasonForDeparture = model.PrimaryReasonForDeparture;

                // Section 1: Overall Views
                existing.CareerGrowthOpportunities = model.CareerGrowthOpportunities;
                existing.CompensationAndBenefits = model.CompensationAndBenefits;
                existing.WorkLifeBalanceRating = model.WorkLifeBalanceRating;
                existing.ManagementLeadershipStyle = model.ManagementLeadershipStyle;
                existing.CompanyCultureWorkEnvironment = model.CompanyCultureWorkEnvironment;
                existing.JobResponsibilities = model.JobResponsibilities;
                existing.RelationshipWithManagerRating = model.RelationshipWithManagerRating;
                existing.OtherReasonDescription = model.OtherReasonDescription;
                existing.OtherRating = model.OtherRating;

                // Section 2: Job Satisfaction & Role Clarity
                existing.RoleMetExpectations = model.RoleMetExpectations;
                existing.ResponsibilitiesClearlyDefined = model.ResponsibilitiesClearlyDefined;
                existing.AdequateTrainingAndResources = model.AdequateTrainingAndResources;
                existing.JobSatisfactionComments = model.JobSatisfactionComments;

                // Section 3: Management & Team Dynamics
                existing.RelationshipWithManagerDescription = model.RelationshipWithManagerDescription;
                existing.SupportedByTeamAndLeadership = model.SupportedByTeamAndLeadership;
                existing.CommunicationCollaborationSuggestions = model.CommunicationCollaborationSuggestions;

                // Section 4: Compensation & Benefits
                existing.SatisfiedWithSalaryAndBenefits = model.SatisfiedWithSalaryAndBenefits;
                existing.CompensationMarketCompetitiveness = model.CompensationMarketCompetitiveness;

                // Section 5: Work Environment & Culture
                existing.FeltValuedAndRecognized = model.FeltValuedAndRecognized;
                existing.MostLikedAboutCompany = model.MostLikedAboutCompany;
                existing.CultureImprovementSuggestions = model.CultureImprovementSuggestions;

                // Section 6: Suggestions for Improvement
                existing.EmployeeRetentionRecommendations = model.EmployeeRetentionRecommendations;
                existing.ResignationPreventionSuggestions = model.ResignationPreventionSuggestions;

                // Section 7: Future Engagement
                existing.WouldReturnToCompany = model.WouldReturnToCompany;
                existing.WouldRecommendCompany = model.WouldRecommendCompany;

                // Section 8: Work-Life Balance
                existing.WorkLifeBalanceComments = model.WorkLifeBalanceComments;

                // Section 9: Additional Comments
                existing.AdditionalComments = model.AdditionalComments;

                // Sign-off
                existing.EmployeeSignature = model.EmployeeSignature;
                existing.HRRepresentativeSignature = model.HRRepresentativeSignature;
                existing.SignOffDate = model.SignOffDate;

                // Audit (CreatedDate/IsDeleted preserved from existing entity)
                existing.ModifiedDate = DateTime.Now;

                await _context.SaveChangesAsync();

                TempData["Success"] = "Exit Interview updated successfully.";

                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        // GET: ExitInterview/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var interview = await _context.ExitInterviews
                .FirstOrDefaultAsync(m => m.Id == id);

            if (interview == null)
                return NotFound();

            return View(interview);
        }

        // POST: ExitInterview/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var interview = await _context.ExitInterviews.FindAsync(id);

            if (interview != null)
            {
                _context.ExitInterviews.Remove(interview);
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "Exit Interview deleted successfully.";

            return RedirectToAction(nameof(Index));
        }

        private bool ExitInterviewExists(int id)
        {
            return _context.ExitInterviews.Any(e => e.Id == id);
        }
    }
}
