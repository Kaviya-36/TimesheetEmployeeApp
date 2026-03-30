using Microsoft.Extensions.Logging;
using Moq;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.DTOs;
using TimeSheetAppWeb.Services;
using Xunit;

namespace TimeSheetAppWeb.Tests.Services
{
    public class InternDetailsServiceTests
    {
        private readonly Mock<IRepository<int, InternDetails>> _internRepo = new();
        private readonly Mock<IRepository<int, User>>          _userRepo   = new();
        private readonly Mock<ILogger<InternDetailsService>>   _logger     = new();

        private InternDetailsService CreateService() =>
            new(_internRepo.Object, _userRepo.Object, _logger.Object);

        private User MakeIntern(int id = 1) => new User
        {
            Id = id, Name = "Intern One", Email = "i@t.com",
            EmployeeId = "I001", Role = UserRole.Intern,
            PasswordHash = "h", IsActive = true
        };

        private InternDetails MakeDetails(int id = 1) => new InternDetails
        {
            Id = id, UserId = 1, MentorId = 2,
            TrainingStart = DateTime.Today,
            TrainingEnd   = DateTime.Today.AddMonths(3)
        };

        // ── GetByIdAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task GetById_NotFound_ReturnsFail()
        {
            _internRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((InternDetails?)null);
            var svc = CreateService();

            var result = await svc.GetByIdAsync(99);

            Assert.False(result.Success);
            Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetById_Found_ReturnsSuccess()
        {
            var details = MakeDetails();
            var intern  = MakeIntern();
            _internRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(details);
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(intern);

            var svc = CreateService();
            var result = await svc.GetByIdAsync(1);

            Assert.True(result.Success);
            Assert.Equal(1, result.Data?.Id);
        }

        // ── CreateAsync ────────────────────────────────────────────────────────

        [Fact]
        public async Task Create_UserNotFound_ReturnsFail()
        {
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((User?)null);
            var svc = CreateService();

            var result = await svc.CreateAsync(new InternDetailsCreateDto
            {
                UserId = 1, TrainingStart = DateTime.Today, TrainingEnd = DateTime.Today.AddMonths(3)
            });

            Assert.False(result.Success);
        }

        [Fact]
        public async Task Create_UserNotIntern_ReturnsFail()
        {
            var manager = new User
            {
                Id = 1, Name = "Mgr", Email = "m@t.com",
                EmployeeId = "M001", Role = UserRole.Manager,
                PasswordHash = "h", IsActive = true
            };
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(manager);
            var svc = CreateService();

            var result = await svc.CreateAsync(new InternDetailsCreateDto
            {
                UserId = 1, TrainingStart = DateTime.Today, TrainingEnd = DateTime.Today.AddMonths(3)
            });

            Assert.False(result.Success);
            Assert.Contains("intern", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Create_Valid_ReturnsSuccess()
        {
            var intern  = MakeIntern();
            var details = MakeDetails();
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(intern);
            _internRepo.Setup(r => r.AddAsync(It.IsAny<InternDetails>())).ReturnsAsync(details);

            var svc = CreateService();
            var result = await svc.CreateAsync(new InternDetailsCreateDto
            {
                UserId = 1, TrainingStart = DateTime.Today, TrainingEnd = DateTime.Today.AddMonths(3)
            });

            Assert.True(result.Success);
        }

        // ── UpdateAsync ────────────────────────────────────────────────────────

        [Fact]
        public async Task Update_NotFound_ReturnsFail()
        {
            _internRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((InternDetails?)null);
            var svc = CreateService();

            var result = await svc.UpdateAsync(99, new InternDetailsCreateDto
            {
                UserId = 1, TrainingStart = DateTime.Today, TrainingEnd = DateTime.Today.AddMonths(3)
            });

            Assert.False(result.Success);
        }

        [Fact]
        public async Task Update_Valid_ReturnsSuccess()
        {
            var intern  = MakeIntern();
            var details = MakeDetails();
            _internRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(details);
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(intern);
            _internRepo.Setup(r => r.UpdateAsync(1, It.IsAny<InternDetails>())).ReturnsAsync(details);

            var svc = CreateService();
            var result = await svc.UpdateAsync(1, new InternDetailsCreateDto
            {
                UserId = 1, TrainingStart = DateTime.Today, TrainingEnd = DateTime.Today.AddMonths(6)
            });

            Assert.True(result.Success);
        }

        // ── DeleteAsync ────────────────────────────────────────────────────────

        [Fact]
        public async Task Delete_NotFound_ReturnsFail()
        {
            _internRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((InternDetails?)null);
            var svc = CreateService();

            var result = await svc.DeleteAsync(99);

            Assert.False(result.Success);
            Assert.False(result.Data);
        }

        [Fact]
        public async Task Delete_Valid_ReturnsSuccess()
        {
            var details = MakeDetails();
            _internRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(details);
            _internRepo.Setup(r => r.DeleteAsync(1)).ReturnsAsync(details);

            var svc = CreateService();
            var result = await svc.DeleteAsync(1);

            Assert.True(result.Success);
            Assert.True(result.Data);
        }

        // ── GetAllAsync ────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAll_ReturnsPagedResults()
        {
            var intern  = MakeIntern();
            var details = new List<InternDetails> { MakeDetails(1), MakeDetails(2) };
            _internRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(details);
            _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync(intern);

            var svc = CreateService();
            var result = await svc.GetAllAsync(1, 10);

            Assert.True(result.Success);
            Assert.Equal(2, result.Data?.TotalRecords);
        }
    }
}
