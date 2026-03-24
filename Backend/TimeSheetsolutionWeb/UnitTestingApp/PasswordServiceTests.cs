using System;
using TimeSheetAppWeb.Services;
using Xunit;

namespace TimeSheetAppWeb.Tests.Services
{
    public class PasswordServiceTests
    {
        private readonly PasswordService _service;

        public PasswordServiceTests()
        {
            _service = new PasswordService();
        }

        [Fact]
        public void HashPassword_Should_Return_NonEmpty_Hash()
        {
            // Arrange
            var password = "MyStrongP@ssword123";

            // Act
            var hash = _service.HashPassword(password);

            // Assert
            Assert.False(string.IsNullOrEmpty(hash));
            Assert.NotEqual(password, hash); // hash should not equal plain text
        }

        [Fact]
        public void VerifyPassword_Should_Return_True_For_Correct_Password()
        {
            // Arrange
            var password = "MyStrongP@ssword123";
            var hash = _service.HashPassword(password);

            // Act
            var result = _service.VerifyPassword(password, hash);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void VerifyPassword_Should_Return_False_For_Wrong_Password()
        {
            // Arrange
            var password = "MyStrongP@ssword123";
            var wrongPassword = "WrongPassword";
            var hash = _service.HashPassword(password);

            // Act
            var result = _service.VerifyPassword(wrongPassword, hash);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void VerifyPassword_Should_Return_False_For_Invalid_Hash()
        {
            // Act
            var result = _service.VerifyPassword("password", "NotAValidBase64Hash");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void HashPassword_Should_Throw_On_Empty_Password()
        {
            Assert.Throws<ArgumentException>(() => _service.HashPassword(""));
            Assert.Throws<ArgumentException>(() => _service.HashPassword("  "));
            Assert.Throws<ArgumentException>(() => _service.HashPassword(null!));
        }
    }
}