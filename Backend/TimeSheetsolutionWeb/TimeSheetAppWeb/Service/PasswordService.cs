using System;
using System.Security.Cryptography;
using System.Text;
using TimeSheetAppWeb.Interfaces;

namespace TimeSheetAppWeb.Services
{
    public class PasswordService : IPasswordService
    {
        private readonly byte[] _secretKey;

        public PasswordService()
        {
           
            _secretKey = Encoding.UTF8.GetBytes("YourVeryStrongSecretKey12345!");
        }

        public string HashPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password cannot be empty.", nameof(password));

            using var hmac = new HMACSHA256(_secretKey);
            var passwordBytes = Encoding.UTF8.GetBytes(password);
            var hashBytes = hmac.ComputeHash(passwordBytes);

            return Convert.ToBase64String(hashBytes);
        }

        public bool VerifyPassword(string password, string passwordHash)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
                return false;

            try
            {
                var computedHash = HashPassword(password);
                return CryptographicOperations.FixedTimeEquals(
                    Convert.FromBase64String(computedHash),
                    Convert.FromBase64String(passwordHash)
                );
            }
            catch (FormatException)
            {
                return false; 
            }
        }
    }
}