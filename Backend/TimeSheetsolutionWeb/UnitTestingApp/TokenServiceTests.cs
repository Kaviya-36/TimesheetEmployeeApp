using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using TimeSheetAppWeb.Model.DTOs;
using TimeSheetAppWeb.Models.DTOs;
using TimeSheetAppWeb.Services;
using Xunit;

namespace TimeSheetAppWeb.Tests.Services
{
    public class TokenServiceTests
    {
        private IConfiguration GetConfiguration(string secret)
        {
            var inMemorySettings = new System.Collections.Generic.Dictionary<string, string>
            {
                {"Keys:Jwt", secret}
            };
            return new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();
        }

        [Fact]
        public void CreateToken_Should_Contain_Correct_Claims()
        {
            // Arrange
            var secret = "ThisIsASecretKeyThatIsLongerThan32Characters!!";
            var config = GetConfiguration(secret);
            var service = new TokenService(config);

            var payload = new TokenPayloadDto
            {
                UserId = 5,
                Username = "alice",
                Role = "Manager"
            };

            // Act
            var tokenString = service.CreateToken(payload);
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(tokenString);

            // Assert claims
            Assert.Contains(token.Claims, c =>
                c.Type == System.Security.Claims.ClaimTypes.NameIdentifier && c.Value == "5");

            Assert.Contains(token.Claims, c =>
                c.Type == System.Security.Claims.ClaimTypes.Name && c.Value == "alice");

            Assert.Contains(token.Claims, c =>
                c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == "Manager");
        }

        [Fact]
        public void CreateToken_Should_Have_Valid_Expiration()
        {
            // Arrange
            var secret = "ThisIsASecretKeyThatIsLongerThan32Characters!!";
            var config = GetConfiguration(secret);
            var service = new TokenService(config);

            var payload = new TokenPayloadDto
            {
                UserId = 1,
                Username = "bob",
                Role = "Employee"
            };

            // Act
            var tokenString = service.CreateToken(payload);
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(tokenString);

            // Assert expiration is roughly 8 hours
            var expectedExpiration = DateTime.UtcNow.AddHours(8);
            Assert.True((token.ValidTo - DateTime.UtcNow).TotalHours <= 8.01); // small buffer
            Assert.True(token.ValidTo > DateTime.UtcNow);
        }

        [Fact]
        public void CreateToken_Should_Be_Valid_With_SigningKey()
        {
            // Arrange
            var secret = "ThisIsASecretKeyThatIsLongerThan32Characters!!";
            var config = GetConfiguration(secret);
            var service = new TokenService(config);

            var payload = new TokenPayloadDto
            {
                UserId = 42,
                Username = "charlie",
                Role = "Admin"
            };

            var tokenString = service.CreateToken(payload);

            // Validate signature
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(secret);

            var validationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key)
            };

            // Act & Assert
            tokenHandler.ValidateToken(tokenString, validationParameters, out var validatedToken);
            Assert.NotNull(validatedToken);
        }
    }
}