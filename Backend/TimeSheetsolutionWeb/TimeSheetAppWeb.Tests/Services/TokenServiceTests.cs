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
