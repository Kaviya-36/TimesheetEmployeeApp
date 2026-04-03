using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using TimeSheetAppWeb.Model.DTOs;
using TimeSheetAppWeb.Models.DTOs;
using TimeSheetAppWeb.Services;
using Xunit;

namespace TimeSheetAppWeb.Tests.Services
{
    /// <summary>
    /// Unit tests for <see cref="TokenService"/>.
    /// Covers constructor validation, token creation, and claim embedding.
    /// </summary>
    public class TokenServiceTests
    {
        // ── Constants ──────────────────────────────────────────────────────────

        private const string ValidSecret    = "SuperSecretKey_AtLeast32Chars_XYZ!";
        private const string ShortSecret    = "short";
        private const double TokenLifeHours = 8.0;

        // ── Helpers ────────────────────────────────────────────────────────────

        private static IConfiguration MakeConfig(string secret) =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Keys:Jwt"] = secret })
                .Build();

        private static IConfiguration MakeEmptyConfig() =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>())
                .Build();

        private TokenService CreateService(string secret = ValidSecret) =>
            new(MakeConfig(secret));

        private static TokenPayloadDto MakePayload(
            int userId = 1,
            string username = "testuser",
            string role = "Employee") =>
            new TokenPayloadDto { UserId = userId, Username = username, Role = role };

        // ── Constructor ────────────────────────────────────────────────────────

        [Fact]
        public void Constructor_ShortSecret_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() =>
                new TokenService(MakeConfig(ShortSecret)));
        }

        [Fact]
        public void Constructor_NullSecret_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() =>
                new TokenService(MakeEmptyConfig()));
        }

        [Fact]
        public void Constructor_ValidSecret_DoesNotThrow()
        {
            var exception = Record.Exception(() => CreateService());
            Assert.Null(exception);
        }

        // ── CreateToken ────────────────────────────────────────────────────────

        [Fact]
        public void CreateToken_ReturnsNonEmptyString()
        {
            var svc   = CreateService();
            var token = svc.CreateToken(MakePayload());

            Assert.NotEmpty(token);
        }

        [Fact]
        public void CreateToken_ReturnsValidJwtFormat()
        {
            var svc     = CreateService();
            var token   = svc.CreateToken(MakePayload());
            var handler = new JwtSecurityTokenHandler();

            Assert.True(handler.CanReadToken(token));
        }

        [Fact]
        public void CreateToken_ContainsCorrectUserId()
        {
            const int expectedUserId = 42;
            var svc   = CreateService();
            var token = svc.CreateToken(MakePayload(userId: expectedUserId));

            var jwt     = new JwtSecurityTokenHandler().ReadJwtToken(token);
            var idClaim = jwt.Claims.FirstOrDefault(c =>
                c.Type == ClaimTypes.NameIdentifier || c.Type == "nameid");

            Assert.NotNull(idClaim);
            Assert.Equal(expectedUserId.ToString(), idClaim!.Value);
        }

        [Fact]
        public void CreateToken_ContainsCorrectRole()
        {
            const string expectedRole = "HR";
            var svc   = CreateService();
            var token = svc.CreateToken(MakePayload(role: expectedRole));

            var jwt       = new JwtSecurityTokenHandler().ReadJwtToken(token);
            var roleClaim = jwt.Claims.FirstOrDefault(c =>
                c.Type == ClaimTypes.Role || c.Type == "role");

            Assert.NotNull(roleClaim);
            Assert.Equal(expectedRole, roleClaim!.Value);
        }

        [Fact]
        public void CreateToken_ContainsCorrectUsername()
        {
            const string expectedUsername = "kaviya";
            var svc   = CreateService();
            var token = svc.CreateToken(MakePayload(username: expectedUsername));

            var jwt       = new JwtSecurityTokenHandler().ReadJwtToken(token);
            var nameClaim = jwt.Claims.FirstOrDefault(c =>
                c.Type == ClaimTypes.Name || c.Type == "unique_name");

            Assert.NotNull(nameClaim);
            Assert.Equal(expectedUsername, nameClaim!.Value);
        }

        [Fact]
        public void CreateToken_ExpiresWithinExpectedWindow()
        {
            var svc    = CreateService();
            var before = DateTime.UtcNow.AddHours(TokenLifeHours - 0.1);
            var after  = DateTime.UtcNow.AddHours(TokenLifeHours + 0.1);

            var token = svc.CreateToken(MakePayload());
            var jwt   = new JwtSecurityTokenHandler().ReadJwtToken(token);

            Assert.InRange(jwt.ValidTo, before, after);
        }

        [Fact]
        public void CreateToken_DifferentPayloads_ProduceDifferentTokens()
        {
            var svc = CreateService();
            var t1  = svc.CreateToken(MakePayload(userId: 1, username: "user1", role: "Employee"));
            var t2  = svc.CreateToken(MakePayload(userId: 2, username: "user2", role: "Manager"));

            Assert.NotEqual(t1, t2);
        }
    }
}

    public class TokenServiceEdgeCaseTests
    {
        private const string ValidSecret = "SuperSecretKey_AtLeast32Chars_XYZ!";

        private static IConfiguration MakeConfig(string secret) =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Keys:Jwt"] = secret })
                .Build();

        private TokenService CreateService(string secret = ValidSecret) =>
            new(MakeConfig(secret));

        private static TokenPayloadDto MakePayload(
            int userId = 1, string username = "testuser", string role = "Employee") =>
            new TokenPayloadDto { UserId = userId, Username = username, Role = role };

        // ── CreateToken — boundary secret length (exactly 32 chars) ──────────

        [Fact]
        public void CreateToken_ExactlyMinLengthSecret_DoesNotThrow()
        {
            // 32-character secret (minimum valid length)
            var secret = new string('A', 32);
            var ex = Record.Exception(() => new TokenService(MakeConfig(secret)));
            Assert.Null(ex);
        }

        // ── CreateToken — 31-char secret (too short) ──────────────────────────

        [Fact]
        public void Constructor_31CharSecret_ThrowsInvalidOperationException()
        {
            var secret = new string('A', 31);
            Assert.Throws<InvalidOperationException>(() => new TokenService(MakeConfig(secret)));
        }

        // ── CreateToken — all roles produce valid tokens ───────────────────────

        [Theory]
        [InlineData("Employee")]
        [InlineData("Manager")]
        [InlineData("HR")]
        [InlineData("Admin")]
        [InlineData("Mentor")]
        [InlineData("Intern")]
        public void CreateToken_AllRoles_ProduceValidJwt(string role)
        {
            var svc   = CreateService();
            var token = svc.CreateToken(MakePayload(role: role));
            var jwt   = new JwtSecurityTokenHandler().ReadJwtToken(token);

            var roleClaim = jwt.Claims.FirstOrDefault(c =>
                c.Type == ClaimTypes.Role || c.Type == "role");
            Assert.Equal(role, roleClaim?.Value);
        }

        // ── CreateToken — large userId ────────────────────────────────────────

        [Fact]
        public void CreateToken_LargeUserId_EmbeddedCorrectly()
        {
            var svc   = CreateService();
            var token = svc.CreateToken(MakePayload(userId: int.MaxValue));
            var jwt   = new JwtSecurityTokenHandler().ReadJwtToken(token);

            var idClaim = jwt.Claims.FirstOrDefault(c =>
                c.Type == ClaimTypes.NameIdentifier || c.Type == "nameid");
            Assert.Equal(int.MaxValue.ToString(), idClaim?.Value);
        }

        // ── CreateToken — username with special characters ────────────────────

        [Fact]
        public void CreateToken_UsernameWithSpecialChars_EmbeddedCorrectly()
        {
            const string specialName = "user@domain.com";
            var svc   = CreateService();
            var token = svc.CreateToken(MakePayload(username: specialName));
            var jwt   = new JwtSecurityTokenHandler().ReadJwtToken(token);

            var nameClaim = jwt.Claims.FirstOrDefault(c =>
                c.Type == ClaimTypes.Name || c.Type == "unique_name");
            Assert.Equal(specialName, nameClaim?.Value);
        }

        // ── CreateToken — token is not expired immediately ────────────────────

        [Fact]
        public void CreateToken_IsNotExpiredImmediately()
        {
            var svc   = CreateService();
            var token = svc.CreateToken(MakePayload());
            var jwt   = new JwtSecurityTokenHandler().ReadJwtToken(token);

            Assert.True(jwt.ValidTo > DateTime.UtcNow);
        }

       
    }

    public class TokenServiceAdditionalTests
    {
        private const string ValidSecret = "SuperSecretKey_AtLeast32Chars_XYZ!";

        private static IConfiguration MakeConfig(string secret) =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Keys:Jwt"] = secret })
                .Build();

        private TokenService CreateService(string secret = ValidSecret) =>
            new(MakeConfig(secret));

        private static TokenPayloadDto MakePayload(
            int userId = 1, string username = "testuser", string role = "Employee") =>
            new TokenPayloadDto { UserId = userId, Username = username, Role = role };

        // ── Constructor: valid 32-char secret does not throw ──────────────────

        [Fact]
        public void Constructor_Exactly32CharSecret_DoesNotThrow()
        {
            var secret = new string('K', 32);
            var ex = Record.Exception(() => new TokenService(MakeConfig(secret)));
            Assert.Null(ex);
        }

        // ── Constructor: 31-char secret throws ───────────────────────────────

        [Fact]
        public void Constructor_31CharSecret_ThrowsInvalidOperationException()
        {
            var secret = new string('K', 31);
            Assert.Throws<InvalidOperationException>(() => new TokenService(MakeConfig(secret)));
        }

        // ── CreateToken: returns three-part JWT ───────────────────────────────

        [Fact]
        public void CreateToken_HasThreeParts_SeparatedByDots()
        {
            var svc   = CreateService();
            var token = svc.CreateToken(MakePayload());
            var parts = token.Split('.');

            Assert.Equal(3, parts.Length);
        }

        // ── CreateToken: userId claim embedded correctly ───────────────────────

        [Fact]
        public void CreateToken_UserId99_ClaimIs99()
        {
            var svc   = CreateService();
            var token = svc.CreateToken(MakePayload(userId: 99));
            var jwt   = new JwtSecurityTokenHandler().ReadJwtToken(token);

            var idClaim = jwt.Claims.FirstOrDefault(c =>
                c.Type == ClaimTypes.NameIdentifier || c.Type == "nameid");
            Assert.Equal("99", idClaim?.Value);
        }

        // ── CreateToken: role claim embedded correctly ────────────────────────

        [Fact]
        public void CreateToken_ManagerRole_RoleClaimIsManager()
        {
            var svc   = CreateService();
            var token = svc.CreateToken(MakePayload(role: "Manager"));
            var jwt   = new JwtSecurityTokenHandler().ReadJwtToken(token);

            var roleClaim = jwt.Claims.FirstOrDefault(c =>
                c.Type == ClaimTypes.Role || c.Type == "role");
            Assert.Equal("Manager", roleClaim?.Value);
        }

        // ── CreateToken: username claim embedded correctly ────────────────────

        [Fact]
        public void CreateToken_Username_NameClaimMatches()
        {
            var svc   = CreateService();
            var token = svc.CreateToken(MakePayload(username: "alice"));
            var jwt   = new JwtSecurityTokenHandler().ReadJwtToken(token);

            var nameClaim = jwt.Claims.FirstOrDefault(c =>
                c.Type == ClaimTypes.Name || c.Type == "unique_name");
            Assert.Equal("alice", nameClaim?.Value);
        }

        // ── CreateToken: token is not expired immediately ─────────────────────

        [Fact]
        public void CreateToken_ValidTo_IsInFuture()
        {
            var svc   = CreateService();
            var token = svc.CreateToken(MakePayload());
            var jwt   = new JwtSecurityTokenHandler().ReadJwtToken(token);

            Assert.True(jwt.ValidTo > DateTime.UtcNow);
        }

        // ── CreateToken: different users produce different tokens ─────────────

        [Fact]
        public void CreateToken_DifferentUserIds_ProduceDifferentTokens()
        {
            var svc = CreateService();
            var t1  = svc.CreateToken(MakePayload(userId: 1));
            var t2  = svc.CreateToken(MakePayload(userId: 2));

            Assert.NotEqual(t1, t2);
        }

        // ── CreateToken: all roles produce readable JWT ───────────────────────

        [Theory]
        [InlineData("Employee")]
        [InlineData("Manager")]
        [InlineData("HR")]
        [InlineData("Admin")]
        [InlineData("Mentor")]
        [InlineData("Intern")]
        public void CreateToken_EachRole_ProducesReadableJwt(string role)
        {
            var svc     = CreateService();
            var token   = svc.CreateToken(MakePayload(role: role));
            var handler = new JwtSecurityTokenHandler();

            Assert.True(handler.CanReadToken(token));
        }

        // ── CreateToken: issued-at is not in the future ───────────────────────

        [Fact]
        public void CreateToken_IssuedAt_IsNotInFuture()
        {
            var svc   = CreateService();
            var token = svc.CreateToken(MakePayload());
            var jwt   = new JwtSecurityTokenHandler().ReadJwtToken(token);

            Assert.True(jwt.IssuedAt <= DateTime.UtcNow.AddSeconds(5));
        }

        // ── CreateToken: expiry is approximately 8 hours from now ─────────────

        [Fact]
        public void CreateToken_Expiry_IsApproximately8HoursFromNow()
        {
            var svc    = CreateService();
            var before = DateTime.UtcNow.AddHours(7.9);
            var after  = DateTime.UtcNow.AddHours(8.1);
            var token  = svc.CreateToken(MakePayload());
            var jwt    = new JwtSecurityTokenHandler().ReadJwtToken(token);

            Assert.InRange(jwt.ValidTo, before, after);
        }
    }
