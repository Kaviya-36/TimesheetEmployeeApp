using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TimeSheetAppWeb.Interfaces;
using TimeSheetAppWeb.Model.DTOs;
using TimeSheetAppWeb.Models.DTOs;

namespace TimeSheetAppWeb.Services
{
    public class TokenService : ITokenService
    {
        private readonly string _jwtSecret;

        public TokenService(IConfiguration configuration)
        {
            _jwtSecret = configuration["Keys:Jwt"]!;
            if (string.IsNullOrEmpty(_jwtSecret) || _jwtSecret.Length < 32)
            {
                throw new InvalidOperationException(
                    "JWT secret must be set and at least 32 characters long.");
            }
        }

        public string CreateToken(TokenPayloadDto payloadDto)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, payloadDto.UserId.ToString()), 
                new Claim(ClaimTypes.Name, payloadDto.Username),
                new Claim(ClaimTypes.Role, payloadDto.Role)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: null,
                audience: null,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}