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

        // ── Constructor null guards ────────────────────────────────────────────

        [Fact]
        public void Constructor_NullInternRepo_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new InternDetailsService(null!, _userRepo.Object, _logger.Object));
        }

        [Fact]
        public void Constructor_NullUserRepo_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new InternDetailsService(_internRepo.Object, null!, _logger.Object));
        }

        [Fact]
        public void Constructor_NullLogger_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new InternDetailsService(_internRepo.Object, _userRepo.Object, null!));
        }

        // ── GetAll with mentorName filter ──────────────────────────────────────

        [Fact]
        public async Task GetAll_WithMentorNameFilter_ReturnsFiltered()
        {
            var intern = MakeIntern();
            var mentor = new User { Id = 2, Name = "Mentor Bob", Email = "m@t.com", EmployeeId = "M001", Role = UserRole.Manager, PasswordHash = "h", IsActive = true };
            var details = new InternDetails
            {
                Id = 1, UserId = 1, MentorId = 2,
                TrainingStart = DateTime.Today, TrainingEnd = DateTime.Today.AddMonths(3),
                Mentor = mentor
            };
            _internRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<InternDetails> { details });
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(intern);
            _userRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(mentor);

            var svc = CreateService();
            var result = await svc.GetAllAsync(1, 10, mentorName: "Bob");

            Assert.True(result.Success);
            Assert.Equal(1, result.Data?.TotalRecords);
        }

        [Fact]
        public async Task GetAll_WithUserNameFilter_ReturnsFiltered()
        {
            var intern = MakeIntern();
            intern.Name = "Alice";
            var details = new InternDetails
            {
                Id = 1, UserId = 1, MentorId = 2,
                TrainingStart = DateTime.Today, TrainingEnd = DateTime.Today.AddMonths(3),
                User = intern
            };
            _internRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<InternDetails> { details });
            _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync(intern);

            var svc = CreateService();
            var result = await svc.GetAllAsync(1, 10, userName: "Alice");

            Assert.True(result.Success);
            Assert.Equal(1, result.Data?.TotalRecords);
        }

        // ── MapToDtoAsync — mentor lookup branch ───────────────────────────────

        [Fact]
        public async Task GetById_WithMentorId_LooksUpMentorName()
        {
            var intern = MakeIntern();
            var mentor = new User { Id = 2, Name = "Mentor Bob", Email = "m@t.com", EmployeeId = "M001", Role = UserRole.Manager, PasswordHash = "h", IsActive = true };
            var details = new InternDetails
            {
                Id = 1, UserId = 1, MentorId = 2,
                TrainingStart = DateTime.Today, TrainingEnd = DateTime.Today.AddMonths(3)
            };
            _internRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(details);
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(intern);
            _userRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(mentor);

            var svc = CreateService();
            var result = await svc.GetByIdAsync(1);

            Assert.True(result.Success);
            Assert.Equal("Mentor Bob", result.Data?.MentorName);
        }

        // ── Create — user is null (combined null+role check) ──────────────────

        [Fact]
        public async Task Create_UserIsNull_ReturnsFail()
        {
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((User?)null);
            var svc = CreateService();

            var result = await svc.CreateAsync(new InternDetailsCreateDto
            {
                UserId = 1, TrainingStart = DateTime.Today, TrainingEnd = DateTime.Today.AddMonths(3)
            });

            Assert.False(result.Success);
            Assert.Contains("intern", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ── Update — user is null ──────────────────────────────────────────────

        [Fact]
        public async Task Update_UserIsNull_ReturnsFail()
        {
            var details = MakeDetails();
            _internRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(details);
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((User?)null);
            var svc = CreateService();

            var result = await svc.UpdateAsync(1, new InternDetailsCreateDto
            {
                UserId = 1, TrainingStart = DateTime.Today, TrainingEnd = DateTime.Today.AddMonths(3)
            });

            Assert.False(result.Success);
        }
    }
}

    public class InternDetailsServiceAdditionalTests
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

        // ── GetByIdAsync: not found returns failure ────────────────────────────

        [Fact]
        public async Task GetById_NotFound_MessageContainsNotFound()
        {
            _internRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((InternDetails?)null);

            var result = await CreateService().GetByIdAsync(99);

            Assert.False(result.Success);
            Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ── CreateAsync: user not found returns failure ───────────────────────

        [Fact]
        public async Task Create_UserNotFound_ReturnsFail()
        {
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((User?)null);

            var result = await CreateService().CreateAsync(new InternDetailsCreateDto
            {
                UserId = 1, TrainingStart = DateTime.Today, TrainingEnd = DateTime.Today.AddMonths(3)
            });

            Assert.False(result.Success);
        }

        // ── CreateAsync: user not intern role returns failure ─────────────────

        [Fact]
        public async Task Create_UserNotInternRole_ReturnsFail()
        {
            var employee = new User
            {
                Id = 1, Name = "Emp", Email = "e@t.com", EmployeeId = "E001",
                Role = UserRole.Employee, PasswordHash = "h", IsActive = true
            };
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(employee);

            var result = await CreateService().CreateAsync(new InternDetailsCreateDto
            {
                UserId = 1, TrainingStart = DateTime.Today, TrainingEnd = DateTime.Today.AddMonths(3)
            });

            Assert.False(result.Success);
            Assert.Contains("intern", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ── UpdateAsync: not found returns failure ────────────────────────────

        [Fact]
        public async Task Update_NotFound_MessageContainsNotFound()
        {
            _internRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((InternDetails?)null);

            var result = await CreateService().UpdateAsync(99, new InternDetailsCreateDto
            {
                UserId = 1, TrainingStart = DateTime.Today, TrainingEnd = DateTime.Today.AddMonths(3)
            });

            Assert.False(result.Success);
        }

        // ── DeleteAsync: not found returns failure ────────────────────────────

        [Fact]
        public async Task Delete_NotFound_ReturnsFalseData()
        {
            _internRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((InternDetails?)null);

            var result = await CreateService().DeleteAsync(99);

            Assert.False(result.Success);
            Assert.False(result.Data);
        }

        // ── GetAllAsync: returns paged results ────────────────────────────────

        [Fact]
        public async Task GetAll_MultipleRecords_TotalRecordsCorrect()
        {
            var intern = MakeIntern();
            var details = Enumerable.Range(1, 5).Select(i => new InternDetails
            {
                Id = i, UserId = 1, MentorId = 2,
                TrainingStart = DateTime.Today, TrainingEnd = DateTime.Today.AddMonths(3)
            }).ToList();
            _internRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(details);
            _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync(intern);

            var result = await CreateService().GetAllAsync(1, 3);

            Assert.True(result.Success);
            Assert.Equal(5, result.Data?.TotalRecords);
            Assert.Equal(3, result.Data?.Data.Count());
        }

        // ── GetAllAsync: empty returns success with zero records ──────────────

        [Fact]
        public async Task GetAll_EmptyRepo_ReturnsZeroRecords()
        {
            _internRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<InternDetails>());

            var result = await CreateService().GetAllAsync(1, 10);

            Assert.True(result.Success);
            Assert.Equal(0, result.Data?.TotalRecords);
        }

        // ── CreateAsync: valid intern creates successfully ────────────────────

        [Fact]
        public async Task Create_ValidIntern_ReturnsSuccess()
        {
            var intern  = MakeIntern();
            var details = MakeDetails();
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(intern);
            _internRepo.Setup(r => r.AddAsync(It.IsAny<InternDetails>())).ReturnsAsync(details);

            var result = await CreateService().CreateAsync(new InternDetailsCreateDto
            {
                UserId = 1, TrainingStart = DateTime.Today, TrainingEnd = DateTime.Today.AddMonths(6)
            });

            Assert.True(result.Success);
        }

        // ── UpdateAsync: valid update returns success ─────────────────────────

        [Fact]
        public async Task Update_ValidData_ReturnsSuccess()
        {
            var intern  = MakeIntern();
            var details = MakeDetails();
            _internRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(details);
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(intern);
            _internRepo.Setup(r => r.UpdateAsync(1, It.IsAny<InternDetails>())).ReturnsAsync(details);

            var result = await CreateService().UpdateAsync(1, new InternDetailsCreateDto
            {
                UserId = 1, TrainingStart = DateTime.Today, TrainingEnd = DateTime.Today.AddMonths(12)
            });

            Assert.True(result.Success);
        }

        // ── DeleteAsync: valid delete returns true ────────────────────────────

        [Fact]
        public async Task Delete_ValidId_ReturnsTrueData()
        {
            var details = MakeDetails();
            _internRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(details);
            _internRepo.Setup(r => r.DeleteAsync(1)).ReturnsAsync(details);

            var result = await CreateService().DeleteAsync(1);

            Assert.True(result.Success);
            Assert.True(result.Data);
        }
    }
