using IT_Service_Management_System.Helpers;
using IT_Service_Management_System.Models.Itsm;
using Xunit;

namespace IT_Service_Management_System.Tests
{
    public class MajorIncidentWorkflowTests
    {
        private static readonly DateTime Now = new(2026, 7, 23, 12, 0, 0);

        [Theory]
        [InlineData(MajorIncidentStatus.Declared, MajorIncidentStatus.Investigating)]
        [InlineData(MajorIncidentStatus.Investigating, MajorIncidentStatus.Identified)]
        [InlineData(MajorIncidentStatus.Investigating, MajorIncidentStatus.Recovering)]
        [InlineData(MajorIncidentStatus.Identified, MajorIncidentStatus.Recovering)]
        [InlineData(MajorIncidentStatus.Recovering, MajorIncidentStatus.Resolved)]
        [InlineData(MajorIncidentStatus.Resolved, MajorIncidentStatus.Review)]
        [InlineData(MajorIncidentStatus.Review, MajorIncidentStatus.Closed)]
        public void Legal_forward_transitions_are_allowed(MajorIncidentStatus from, MajorIncidentStatus to)
        {
            Assert.True(MajorIncidentWorkflow.CanTransition(from, to));
        }

        [Theory]
        [InlineData(MajorIncidentStatus.Declared, MajorIncidentStatus.Resolved)]   // cannot skip stages
        [InlineData(MajorIncidentStatus.Declared, MajorIncidentStatus.Recovering)]
        [InlineData(MajorIncidentStatus.Resolved, MajorIncidentStatus.Investigating)] // no going backwards
        [InlineData(MajorIncidentStatus.Recovering, MajorIncidentStatus.Declared)]
        public void Illegal_transitions_are_rejected(MajorIncidentStatus from, MajorIncidentStatus to)
        {
            Assert.False(MajorIncidentWorkflow.CanTransition(from, to));
        }

        [Fact]
        public void Any_open_incident_can_be_closed_directly()
        {
            Assert.True(MajorIncidentWorkflow.CanTransition(MajorIncidentStatus.Declared, MajorIncidentStatus.Closed));
            Assert.True(MajorIncidentWorkflow.CanTransition(MajorIncidentStatus.Investigating, MajorIncidentStatus.Closed));
            Assert.True(MajorIncidentWorkflow.CanTransition(MajorIncidentStatus.Recovering, MajorIncidentStatus.Closed));
        }

        [Fact]
        public void Closed_is_terminal()
        {
            Assert.False(MajorIncidentWorkflow.CanTransition(MajorIncidentStatus.Closed, MajorIncidentStatus.Review));
            Assert.False(MajorIncidentWorkflow.CanTransition(MajorIncidentStatus.Closed, MajorIncidentStatus.Declared));
            Assert.Empty(MajorIncidentWorkflow.NextStages(MajorIncidentStatus.Closed));
        }

        [Fact]
        public void Same_status_is_not_a_transition()
        {
            Assert.False(MajorIncidentWorkflow.CanTransition(MajorIncidentStatus.Investigating, MajorIncidentStatus.Investigating));
        }

        [Fact]
        public void NextStages_excludes_direct_close()
        {
            var stages = MajorIncidentWorkflow.NextStages(MajorIncidentStatus.Investigating);
            Assert.Contains(MajorIncidentStatus.Identified, stages);
            Assert.Contains(MajorIncidentStatus.Recovering, stages);
            Assert.DoesNotContain(MajorIncidentStatus.Closed, stages);
        }

        [Theory]
        [InlineData(MajorIncidentStatus.Resolved, true)]
        [InlineData(MajorIncidentStatus.Review, true)]
        [InlineData(MajorIncidentStatus.Closed, true)]
        [InlineData(MajorIncidentStatus.Declared, false)]
        [InlineData(MajorIncidentStatus.Recovering, false)]
        public void IsResolvedState_reflects_service_restoration(MajorIncidentStatus status, bool expected)
        {
            Assert.Equal(expected, MajorIncidentWorkflow.IsResolvedState(status));
        }

        [Fact]
        public void MeanTimeToResolve_averages_only_resolved_incidents()
        {
            var incidents = new[]
            {
                new MajorIncident { DeclaredAt = Now, ResolvedAt = Now.AddMinutes(60) },   // 60
                new MajorIncident { DeclaredAt = Now, ResolvedAt = Now.AddMinutes(120) },  // 120
                new MajorIncident { DeclaredAt = Now, ResolvedAt = null },                 // ignored (unresolved)
            };

            var mttr = MajorIncidentWorkflow.MeanTimeToResolveMinutes(incidents);

            Assert.Equal(90, mttr);
        }

        [Fact]
        public void MeanTimeToResolve_is_null_when_nothing_resolved()
        {
            var incidents = new[] { new MajorIncident { DeclaredAt = Now, ResolvedAt = null } };
            Assert.Null(MajorIncidentWorkflow.MeanTimeToResolveMinutes(incidents));
        }

        [Fact]
        public void TimeToResolveMinutes_is_null_until_resolved_then_computed()
        {
            var open = new MajorIncident { DeclaredAt = Now };
            Assert.Null(open.TimeToResolveMinutes);

            var resolved = new MajorIncident { DeclaredAt = Now, ResolvedAt = Now.AddMinutes(45) };
            Assert.Equal(45, resolved.TimeToResolveMinutes);
        }
    }
}
