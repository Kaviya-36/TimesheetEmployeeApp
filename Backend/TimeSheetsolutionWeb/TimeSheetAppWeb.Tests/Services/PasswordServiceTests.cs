using TimeSheetAppWeb.Services;
using Xunit;

namespace TimeSheetAppWeb.Tests.Services
{
    /// <summary>
    /// Unit tests for <see cref="PasswordService"/>.
    /// Covers hashing, verification, edge cases, and security properties.
    /// </summary>
    public class PasswordServiceTests
    {
        private readonly PasswordService _svc = new();

        // ── HashPassword ───────────────────────────────────────────────────────

        [Fact]
        public void HashPassword_ReturnsNonEmptyString()
        {
            var hash = _svc.HashPassword("mypassword");
            Assert.NotEmpty(hash);
        }

        [Fact]
        public void HashPassword_SameInput_ProducesSameHash()
        {
            var h1 = _svc.HashPassword("password123");
            var h2 = _svc.HashPassword("password123");
            Assert.Equal(h1, h2);
        }

        [Fact]
        public void HashPassword_DifferentInputs_ProduceDifferentHashes()
        {
            var h1 = _svc.HashPassword("password1");
            var h2 = _svc.HashPassword("password2");
            Assert.NotEqual(h1, h2);
        }

        [Fact]
        public void HashPassword_EmptyString_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _svc.HashPassword(string.Empty));
        }

        [Fact]
        public void HashPassword_WhitespaceOnly_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _svc.HashPassword("   "));
        }

        [Fact]
        public void HashPassword_ReturnsValidBase64()
        {
            var hash  = _svc.HashPassword("test");
            var bytes = Convert.FromBase64String(hash);
            Assert.NotEmpty(bytes);
        }

        // ── VerifyPassword ─────────────────────────────────────────────────────

        [Fact]
        public void VerifyPassword_CorrectPassword_ReturnsTrue()
        {
            var hash = _svc.HashPassword("correct");
            Assert.True(_svc.VerifyPassword("correct", hash));
        }

        [Fact]
        public void VerifyPassword_WrongPassword_ReturnsFalse()
        {
            var hash = _svc.HashPassword("correct");
            Assert.False(_svc.VerifyPassword("wrong", hash));
        }

        [Fact]
        public void VerifyPassword_EmptyPassword_ReturnsFalse()
        {
            var hash = _svc.HashPassword("correct");
            Assert.False(_svc.VerifyPassword(string.Empty, hash));
        }

        [Fact]
        public void VerifyPassword_EmptyHash_ReturnsFalse()
        {
            Assert.False(_svc.VerifyPassword("password", string.Empty));
        }

        [Fact]
        public void VerifyPassword_InvalidBase64Hash_ReturnsFalse()
        {
            Assert.False(_svc.VerifyPassword("password", "not-valid-base64!!!"));
        }

        [Fact]
        public void VerifyPassword_IsCaseSensitive()
        {
            var hash = _svc.HashPassword("Password");
            Assert.False(_svc.VerifyPassword("password", hash));
        }

        [Fact]
        public void VerifyPassword_NullHash_ReturnsFalse()
        {
            Assert.False(_svc.VerifyPassword("password", null!));
        }
    }
}
