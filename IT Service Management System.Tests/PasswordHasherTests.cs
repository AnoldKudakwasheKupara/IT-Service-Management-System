using IT_Service_Management_System.Helpers;
using Xunit;

namespace IT_Service_Management_System.Tests
{
    public class PasswordHasherTests
    {
        [Fact]
        public void Hash_then_verify_round_trips()
        {
            var hash = PasswordHasher.HashPassword("Str0ng!Pass");
            Assert.True(PasswordHasher.VerifyPassword("Str0ng!Pass", hash));
        }

        [Fact]
        public void Wrong_password_fails()
        {
            var hash = PasswordHasher.HashPassword("Str0ng!Pass");
            Assert.False(PasswordHasher.VerifyPassword("wrong", hash));
        }

        [Fact]
        public void Hash_is_salted_so_two_hashes_differ()
        {
            Assert.NotEqual(PasswordHasher.HashPassword("same"), PasswordHasher.HashPassword("same"));
        }

        [Fact]
        public void IsHashed_distinguishes_hashed_from_plaintext()
        {
            Assert.True(PasswordHasher.IsHashed(PasswordHasher.HashPassword("x")));
            Assert.False(PasswordHasher.IsHashed("plaintext"));
            Assert.False(PasswordHasher.IsHashed(null));
            Assert.False(PasswordHasher.IsHashed(""));
        }

        [Fact]
        public void Verify_fails_closed_on_null_or_garbage_stored_value()
        {
            Assert.False(PasswordHasher.VerifyPassword("x", null));
            Assert.False(PasswordHasher.VerifyPassword("x", "not-a-real-hash"));
        }
    }
}
