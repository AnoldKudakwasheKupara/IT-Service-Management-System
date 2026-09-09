using System.Text.Json;
using IT_Service_Management_System.Models;
using IT_Service_Management_System.Services.Auditing;
using IT_Service_Management_System.ViewModels;
using Xunit;

namespace IT_Service_Management_System.Tests
{
    /// <summary>
    /// Covers the parts of the audit trail that carry its guarantees: the hash chain that makes
    /// tampering detectable, and the redaction rules that keep secrets out of the trail in the
    /// first place.
    /// </summary>
    public class AuditTrailTests
    {
        private static AuditLog Entry(string action = "Updated", string details = "Ticket edited") => new()
        {
            Id = 1,
            UserId = "7",
            UserName = "Jane Moyo",
            UserRole = "Admin",
            Action = action,
            Entity = "Ticket",
            EntityId = 42,
            EntityKey = "42",
            Details = details,
            Timestamp = new DateTime(2026, 3, 4, 9, 30, 0, DateTimeKind.Unspecified),
            IpAddress = "10.0.0.5",
            Device = "Chrome on Windows",
            CorrelationId = "req-1",
            Source = AuditSource.Automatic
        };

        // ── Hash chain ───────────────────────────────────────────────────────

        [Fact]
        public void Hash_is_stable_for_identical_content()
        {
            Assert.Equal(AuditWriter.ComputeHash(Entry()), AuditWriter.ComputeHash(Entry()));
        }

        [Fact]
        public void Editing_any_captured_field_changes_the_hash()
        {
            var original = AuditWriter.ComputeHash(Entry());

            Assert.NotEqual(original, AuditWriter.ComputeHash(Entry(details: "Ticket deleted")));
            Assert.NotEqual(original, AuditWriter.ComputeHash(Entry(action: "Deleted")));

            var reassigned = Entry();
            reassigned.UserName = "Someone Else";
            Assert.NotEqual(original, AuditWriter.ComputeHash(reassigned));

            var backdated = Entry();
            backdated.Timestamp = backdated.Timestamp.AddMinutes(-5);
            Assert.NotEqual(original, AuditWriter.ComputeHash(backdated));
        }

        [Fact]
        public void Changing_the_diff_changes_the_hash()
        {
            var entry = Entry();
            entry.Changes = JsonSerializer.Serialize(new[]
            {
                new AuditFieldChange { Field = "Priority", Old = "Low", New = "Critical" }
            });
            var withChanges = AuditWriter.ComputeHash(entry);

            entry.Changes = JsonSerializer.Serialize(new[]
            {
                new AuditFieldChange { Field = "Priority", Old = "Low", New = "Low" }
            });

            Assert.NotEqual(withChanges, AuditWriter.ComputeHash(entry));
        }

        [Fact]
        public void Chain_link_is_part_of_the_hash_so_removing_a_predecessor_is_detectable()
        {
            var first = Entry();
            first.PreviousHash = "AAAA";
            var second = Entry();
            second.PreviousHash = "BBBB";

            Assert.NotEqual(AuditWriter.ComputeHash(first), AuditWriter.ComputeHash(second));
        }

        [Fact]
        public void Hash_survives_the_database_round_trip()
        {
            // Entries are written with DateTime.Now (Kind=Local) and read back from datetime2 as
            // Kind=Unspecified. A Kind-sensitive hash format makes every entry fail its own
            // verification the moment it is re-read.
            var written = Entry();
            written.Timestamp = DateTime.SpecifyKind(written.Timestamp, DateTimeKind.Local);

            var readBack = Entry();
            readBack.Timestamp = DateTime.SpecifyKind(readBack.Timestamp, DateTimeKind.Unspecified);

            Assert.Equal(AuditWriter.ComputeHash(written), AuditWriter.ComputeHash(readBack));
        }

        [Fact]
        public void Presentational_fields_do_not_affect_the_hash()
        {
            // Location is resolved after the fact on the background queue, so it must not be part
            // of what the hash asserts — otherwise every entry would fail its own verification.
            var entry = Entry();
            var before = AuditWriter.ComputeHash(entry);
            entry.Location = "Harare, Zimbabwe";

            Assert.Equal(before, AuditWriter.ComputeHash(entry));
        }

        // ── Redaction ────────────────────────────────────────────────────────

        [Theory]
        [InlineData("PasswordHash")]
        [InlineData("PasswordSalt")]
        [InlineData("MfaSecret")]
        [InlineData("ResetPasswordToken")]      // matched by token, not by the explicit list
        [InlineData("OtpCode")]
        [InlineData("ApiKeyValue")]
        [InlineData("clientsecret")]            // matching is case-insensitive
        public void Sensitive_properties_are_redacted(string property)
        {
            Assert.True(new AuditOptions().IsRedacted(property));
        }

        [Theory]
        [InlineData("Title")]
        [InlineData("Status")]
        [InlineData("AssignedToId")]
        [InlineData("Email")]
        public void Ordinary_properties_are_not_redacted(string property)
        {
            Assert.False(new AuditOptions().IsRedacted(property));
        }

        [Fact]
        public void The_audit_table_itself_is_never_captured()
        {
            // Auditing AuditLog would recurse: every audit write would produce another.
            Assert.True(new AuditOptions().IsExcluded(nameof(AuditLog)));
        }

        [Fact]
        public void Concurrency_tokens_are_ignored_rather_than_recorded_as_changes()
        {
            Assert.True(new AuditOptions().IsIgnored("RowVersion"));
        }

        // ── Entry payload ────────────────────────────────────────────────────

        [Fact]
        public void ChangeList_round_trips_the_stored_diff()
        {
            var entry = Entry();
            entry.Changes = JsonSerializer.Serialize(new[]
            {
                new AuditFieldChange { Field = "Status", Old = "Open", New = "Resolved" },
                new AuditFieldChange { Field = "ClosedAt", Old = null, New = "2026-03-04 09:30:00" }
            });

            var changes = entry.ChangeList();

            Assert.Equal(2, changes.Count);
            Assert.Equal("Status", changes[0].Field);
            Assert.Equal("Open", changes[0].Old);
            Assert.Null(changes[1].Old);
        }

        [Fact]
        public void ChangeList_is_empty_rather_than_throwing_on_missing_or_bad_json()
        {
            Assert.Empty(Entry().ChangeList());

            var corrupt = Entry();
            corrupt.Changes = "{not json";
            Assert.Empty(corrupt.ChangeList());
        }

        // ── Filter ───────────────────────────────────────────────────────────

        [Fact]
        public void Empty_filter_is_inactive_and_carries_no_route_values()
        {
            var filter = new AuditFilter();

            Assert.False(filter.IsActive);
            Assert.Empty(filter.RouteValues());
            Assert.Equal("no filter", filter.Describe());
        }

        [Fact]
        public void Filter_round_trips_into_route_values_for_the_export_link()
        {
            var filter = new AuditFilter
            {
                User = "Jane Moyo",
                Action = "Deleted",
                Source = AuditSource.Automatic,
                From = new DateTime(2026, 3, 1),
                To = new DateTime(2026, 3, 31)
            };

            var values = filter.RouteValues();

            Assert.True(filter.IsActive);
            Assert.Equal("Jane Moyo", values["user"]);
            Assert.Equal("Deleted", values["action"]);
            Assert.Equal("Automatic", values["source"]);
            Assert.Equal("2026-03-01", values["from"]);
            Assert.Equal("2026-03-31", values["to"]);
            Assert.False(values.ContainsKey("entity"));
        }
    }
}
