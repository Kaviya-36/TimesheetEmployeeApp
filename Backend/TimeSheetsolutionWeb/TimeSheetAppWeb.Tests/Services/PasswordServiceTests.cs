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

    public class PasswordServiceEdgeCaseTests
    {
        private readonly PasswordService _svc = new();

        // ── HashPassword — long password ──────────────────────────────────────

        [Fact]
        public void HashPassword_LongPassword_ReturnsHash()
        {
            var longPassword = new string('x', 1000);
            var hash = _svc.HashPassword(longPassword);
            Assert.NotEmpty(hash);
        }

        // ── HashPassword — special characters ────────────────────────────────

        [Fact]
        public void HashPassword_SpecialCharacters_ReturnsHash()
        {
            var hash = _svc.HashPassword("P@$$w0rd!#%^&*()");
            Assert.NotEmpty(hash);
        }

        // ── HashPassword — unicode characters ────────────────────────────────

        [Fact]
        public void HashPassword_UnicodeCharacters_ReturnsHash()
        {
            var hash = _svc.HashPassword("пароль123");
            Assert.NotEmpty(hash);
        }

        // ── VerifyPassword — long password round-trip ─────────────────────────

        [Fact]
        public void VerifyPassword_LongPassword_RoundTrip()
        {
            var longPassword = new string('y', 500);
            var hash = _svc.HashPassword(longPassword);
            Assert.True(_svc.VerifyPassword(longPassword, hash));
        }

        // ── VerifyPassword — whitespace-only password ─────────────────────────

        [Fact]
        public void VerifyPassword_WhitespacePassword_ReturnsFalse()
        {
            var hash = _svc.HashPassword("correct");
            Assert.False(_svc.VerifyPassword("   ", hash));
        }

        // ── VerifyPassword — null password ────────────────────────────────────

        [Fact]
        public void VerifyPassword_NullPassword_ReturnsFalse()
        {
            var hash = _svc.HashPassword("correct");
            Assert.False(_svc.VerifyPassword(null!, hash));
        }

        // ── HashPassword — single character ──────────────────────────────────

        [Fact]
        public void HashPassword_SingleChar_ReturnsHash()
        {
            var hash = _svc.HashPassword("a");
            Assert.NotEmpty(hash);
            Assert.True(_svc.VerifyPassword("a", hash));
        }

        // ── VerifyPassword — tampered hash ───────────────────────────────────

        [Fact]
        public void VerifyPassword_TamperedHash_ReturnsFalse()
        {
            var hash = _svc.HashPassword("password");
            // Flip the last character
            var tampered = hash[..^1] + (hash[^1] == 'A' ? 'B' : 'A');
            Assert.False(_svc.VerifyPassword("password", tampered));
        }

        // ── HashPassword — deterministic (same input → same hash) ────────────

        [Fact]
        public void HashPassword_Deterministic_SameInputAlwaysSameHash()
        {
            const string password = "deterministic_test_123";
            var hashes = Enumerable.Range(0, 5).Select(_ => _svc.HashPassword(password)).ToList();
            Assert.All(hashes, h => Assert.Equal(hashes[0], h));
        }

        // ── VerifyPassword — extra whitespace around password ─────────────────

        [Fact]
        public void VerifyPassword_ExtraWhitespace_ReturnsFalse()
        {
            var hash = _svc.HashPassword("password");
            Assert.False(_svc.VerifyPassword(" password ", hash));
        }
    }

    public class PasswordServiceAdditionalTests
    {
        private readonly PasswordService _svc = new();

        // ── HashPassword: numeric-only password ───────────────────────────────

        [Fact]
        public void HashPassword_NumericOnly_ReturnsHash()
        {
            var hash = _svc.HashPassword("123456789");
            Assert.NotEmpty(hash);
        }

        // ── HashPassword: mixed case produces consistent hash ─────────────────

        [Fact]
        public void HashPassword_MixedCase_ConsistentHash()
        {
            var h1 = _svc.HashPassword("MyPassword");
            var h2 = _svc.HashPassword("MyPassword");
            Assert.Equal(h1, h2);
        }

        // ── VerifyPassword: correct password returns true ─────────────────────

        [Fact]
        public void VerifyPassword_CorrectPassword_ReturnsTrue()
        {
            var hash = _svc.HashPassword("securePass99");
            Assert.True(_svc.VerifyPassword("securePass99", hash));
        }

        // ── VerifyPassword: wrong password returns false ──────────────────────

        [Fact]
        public void VerifyPassword_WrongPassword_ReturnsFalse()
        {
            var hash = _svc.HashPassword("securePass99");
            Assert.False(_svc.VerifyPassword("wrongPass", hash));
        }

        // ── VerifyPassword: case sensitivity ─────────────────────────────────

        [Fact]
        public void VerifyPassword_DifferentCase_ReturnsFalse()
        {
            var hash = _svc.HashPassword("CaseSensitive");
            Assert.False(_svc.VerifyPassword("casesensitive", hash));
        }

        // ── HashPassword: two different passwords produce different hashes ─────

        [Fact]
        public void HashPassword_TwoDifferentPasswords_ProduceDifferentHashes()
        {
            var h1 = _svc.HashPassword("alpha");
            var h2 = _svc.HashPassword("beta");
            Assert.NotEqual(h1, h2);
        }

        // ── VerifyPassword: empty hash returns false ──────────────────────────

        [Fact]
        public void VerifyPassword_EmptyHash_ReturnsFalse()
        {
            Assert.False(_svc.VerifyPassword("password", ""));
        }

        // ── HashPassword: null input throws ──────────────────────────────────

        [Fact]
        public void HashPassword_NullInput_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _svc.HashPassword(null!));
        }

        // ── VerifyPassword: round-trip with special chars ─────────────────────

        [Fact]
        public void VerifyPassword_SpecialCharsRoundTrip_ReturnsTrue()
        {
            const string pw = "P@$$w0rd!#%";
            var hash = _svc.HashPassword(pw);
            Assert.True(_svc.VerifyPassword(pw, hash));
        }

        // ── HashPassword: output is valid base64 ──────────────────────────────

        [Fact]
        public void HashPassword_Output_IsValidBase64()
        {
            var hash = _svc.HashPassword("testpassword");
            var ex = Record.Exception(() => Convert.FromBase64String(hash));
            Assert.Null(ex);
        }

        // ── VerifyPassword: unicode round-trip ────────────────────────────────

        [Fact]
        public void VerifyPassword_UnicodePassword_RoundTrip()
        {
            const string pw = "пароль123";
            var hash = _svc.HashPassword(pw);
            Assert.True(_svc.VerifyPassword(pw, hash));
        }
    }
