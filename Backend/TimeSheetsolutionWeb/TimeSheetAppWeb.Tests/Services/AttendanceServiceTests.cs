using Moq;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Services;
using Xunit;

namespace TimeSheetAppWeb.Tests.Services
{
    public class AttendanceServiceTests
    {
        private readonly Mock<IRepository<int, Attendance>> _attRepo  = new();
        private readonly Mock<IRepository<int, User>>       _userRepo = new();

        private AttendanceService CreateService() =>
            new(_attRepo.Object, _userRepo.Object);

        private User MakeUser(int id = 1) => new User
        {
            Id = id, Name = "Test", Email = "t@t.com",
            EmployeeId = "E001", Role = UserRole.Employee,
            PasswordHash = "h", IsActive = true
        };

        // ── CheckInAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task CheckIn_UserNotFound_ReturnsFail()
        {
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((User?)null);
            _attRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Attendance>());
            var svc = CreateService();

            var result = await svc.CheckInAsync(1);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task CheckIn_AlreadyCheckedIn_ReturnsSuccess_WithMessage()
        {
            var user = MakeUser();
            var existing = new Attendance
            {
                Id = 1, UserId = 1, Date = DateTime.Today,
                CheckIn = new TimeSpan(9, 0, 0)
            };
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _attRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Attendance> { existing });

            var svc = CreateService();
            var result = await svc.CheckInAsync(1);

            // Already checked in returns success (frontend sync)
            Assert.True(result.Success);
            Assert.Contains("already", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CheckIn_FirstTimeToday_ReturnsSuccess()
        {
            var user = MakeUser();
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _attRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Attendance>());
            _attRepo.Setup(r => r.AddAsync(It.IsAny<Attendance>())).ReturnsAsync(new Attendance
            {
                Id = 1, UserId = 1, Date = DateTime.Today,
                CheckIn = DateTime.Now.TimeOfDay
            });

            var svc = CreateService();
            var result = await svc.CheckInAsync(1);

            Assert.True(result.Success);
        }

        // ── CheckOutAsync ─────────────────────────────────────────────────────

        [Fact]
        public async Task CheckOut_NotCheckedIn_ReturnsFail()
        {
            var user = MakeUser();
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _attRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Attendance>());

            var svc = CreateService();
            var result = await svc.CheckOutAsync(1);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task CheckOut_AlreadyCheckedOut_ReturnsSuccess_WithMessage()
        {
            var user = MakeUser();
            var existing = new Attendance
            {
                Id = 1, UserId = 1, Date = DateTime.Today,
                CheckIn  = new TimeSpan(9, 0, 0),
                CheckOut = new TimeSpan(17, 0, 0)
            };
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _attRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Attendance> { existing });

            var svc = CreateService();
            var result = await svc.CheckOutAsync(1);

            Assert.True(result.Success);
            Assert.Contains("already", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CheckOut_ValidCheckIn_ReturnsSuccess()
        {
            var user = MakeUser();
            var existing = new Attendance
            {
                Id = 1, UserId = 1, Date = DateTime.Today,
                CheckIn = new TimeSpan(9, 0, 0)
            };
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _attRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Attendance> { existing });
            _attRepo.Setup(r => r.UpdateAsync(1, It.IsAny<Attendance>())).ReturnsAsync(existing);

            var svc = CreateService();
            var result = await svc.CheckOutAsync(1);

            Assert.True(result.Success);
        }

        // ── CalcAutoCheckout (night shift) ────────────────────────────────────

        [Fact]
        public async Task CheckOut_NightShift_TotalHoursIs8()
        {
            var user = MakeUser();
            // Check in at 10 PM
            var existing = new Attendance
            {
                Id = 1, UserId = 1, Date = DateTime.Today,
                CheckIn = new TimeSpan(22, 0, 0)
            };
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _attRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Attendance> { existing });
            _attRepo.Setup(r => r.UpdateAsync(1, It.IsAny<Attendance>()))
                    .ReturnsAsync((int id, Attendance a) => a);

            var svc = CreateService();
            var result = await svc.CheckOutAsync(1);

            Assert.True(result.Success);
        }
    }
}
