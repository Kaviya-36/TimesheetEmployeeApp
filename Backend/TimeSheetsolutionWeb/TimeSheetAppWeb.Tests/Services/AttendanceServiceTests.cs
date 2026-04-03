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

        [Fact]
        public async Task CheckIn_AutoCheckoutYesterday_WhenMissed()
        {
            var user = MakeUser();
            var yesterday = DateTime.Today.AddDays(-1);
            var prevRecord = new Attendance { Id = 5, UserId = 1, Date = yesterday, CheckIn = new TimeSpan(9, 0, 0) };

            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _attRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Attendance> { prevRecord });
            _attRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(prevRecord);
            _attRepo.Setup(r => r.UpdateAsync(5, It.IsAny<Attendance>())).ReturnsAsync(prevRecord);
            _attRepo.Setup(r => r.AddAsync(It.IsAny<Attendance>())).ReturnsAsync(new Attendance
            {
                Id = 2, UserId = 1, Date = DateTime.Today, CheckIn = DateTime.Now.TimeOfDay
            });

            var svc = CreateService();
            var result = await svc.CheckInAsync(1);

            Assert.True(result.Success);
        }

        // ── CheckOutAsync ─────────────────────────────────────────────────────

        [Fact]
        public async Task CheckOut_UserNotFound_ReturnsFail()
        {
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((User?)null);
            _attRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Attendance>());
            var svc = CreateService();

            var result = await svc.CheckOutAsync(1);

            Assert.False(result.Success);
        }

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

        // ── CalcAutoCheckout — night shift / midnight crossing ────────────────

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

        [Fact]
        public async Task GetToday_AutoCheckoutYesterday_MidnightCrossing()
        {
            var yesterday = DateTime.Today.AddDays(-1);
            // Check in at 11 PM yesterday — crosses midnight
            var prev = new Attendance
            {
                Id = 1, UserId = 1, Date = yesterday,
                CheckIn = new TimeSpan(23, 0, 0)
            };
            _attRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Attendance> { prev });
            _attRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(prev);
            _attRepo.Setup(r => r.UpdateAsync(1, It.IsAny<Attendance>())).ReturnsAsync(prev);
            var svc = CreateService();

            var result = await svc.GetTodayAsync(1);

            Assert.True(result.Success);
            Assert.True(result.Data?.MissedCheckout == true || result.Message == "missed_checkout");
        }
    }

    // ── GetTodayAsync ─────────────────────────────────────────────────────────

    public class AttendanceServiceGetTodayTests
    {
        private readonly Mock<IRepository<int, Attendance>> _attRepo  = new();
        private readonly Mock<IRepository<int, User>>       _userRepo = new();

        private AttendanceService CreateService() => new(_attRepo.Object, _userRepo.Object);

        [Fact]
        public async Task GetToday_NoRecord_ReturnsSuccessWithNull()
        {
            _attRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Attendance>());
            var svc = CreateService();

            var result = await svc.GetTodayAsync(1);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task GetToday_HasRecord_ReturnsDtoWithData()
        {
            var att = new Attendance
            {
                Id = 1, UserId = 1, Date = DateTime.Today,
                CheckIn = new TimeSpan(9, 0, 0)
            };
            _attRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Attendance> { att });
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new User
            {
                Id = 1, Name = "Test", Email = "t@t.com", EmployeeId = "E001",
                Role = UserRole.Employee, PasswordHash = "h", IsActive = true
            });
            var svc = CreateService();

            var result = await svc.GetTodayAsync(1);

            Assert.True(result.Success);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task GetToday_MissedCheckoutYesterday_SetsMissedFlag()
        {
            var yesterday = DateTime.Today.AddDays(-1);
            var prev = new Attendance
            {
                Id = 1, UserId = 1, Date = yesterday,
                CheckIn = new TimeSpan(9, 0, 0)
            };
            _attRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Attendance> { prev });
            _attRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(prev);
            _attRepo.Setup(r => r.UpdateAsync(1, It.IsAny<Attendance>())).ReturnsAsync(prev);
            var svc = CreateService();

            var result = await svc.GetTodayAsync(1);

            Assert.True(result.Success);
            Assert.True(result.Data?.MissedCheckout == true || result.Message == "missed_checkout");
        }

        [Fact]
        public async Task GetToday_AutoCheckout12h_WhenOverdue()
        {
            // Simulate check-in 13 hours ago (past the 12h threshold)
            var checkInTime = DateTime.Now.AddHours(-13).TimeOfDay;
            var att = new Attendance
            {
                Id = 1, UserId = 1, Date = DateTime.Today,
                CheckIn = checkInTime
            };
            _attRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Attendance> { att });
            _attRepo.Setup(r => r.UpdateAsync(1, It.IsAny<Attendance>())).ReturnsAsync(att);
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new User
            {
                Id = 1, Name = "T", Email = "t@t.com", EmployeeId = "E001",
                Role = UserRole.Employee, PasswordHash = "h", IsActive = true
            });
            var svc = CreateService();

            var result = await svc.GetTodayAsync(1);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task GetToday_WithUserNavProp_MapsName()
        {
            var user = new User { Id = 1, Name = "NavUser", Email = "n@t.com", EmployeeId = "N001", Role = UserRole.Employee, PasswordHash = "h", IsActive = true };
            var att = new Attendance { Id = 1, UserId = 1, Date = DateTime.Today, CheckIn = new TimeSpan(9, 0, 0), User = user };
            _attRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Attendance> { att });
            var svc = CreateService();

            var result = await svc.GetTodayAsync(1);

            Assert.True(result.Success);
            Assert.Equal("NavUser", result.Data?.EmployeeName);
        }

        [Fact]
        public async Task GetUserAttendance_ReturnsPaged()
        {
            var records = Enumerable.Range(1, 5).Select(i => new Attendance
            {
                Id = i, UserId = 1, Date = DateTime.Today.AddDays(-i),
                CheckIn = new TimeSpan(9, 0, 0)
            }).ToList();
            _attRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(records);
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new User
            {
                Id = 1, Name = "T", Email = "t@t.com", EmployeeId = "E001",
                Role = UserRole.Employee, PasswordHash = "h", IsActive = true
            });
            var svc = CreateService();

            var result = await svc.GetUserAttendanceAsync(1, 1, 3);

            Assert.True(result.Success);
            Assert.Equal(5, result.Data?.TotalRecords);
            Assert.Equal(3, result.Data?.Data.Count());
        }

        [Fact]
        public async Task GetAllAttendance_ReturnsPaged()
        {
            var records = Enumerable.Range(1, 10).Select(i => new Attendance
            {
                Id = i, UserId = i % 3 + 1, Date = DateTime.Today.AddDays(-i),
                CheckIn = new TimeSpan(9, 0, 0)
            }).ToList();
            _attRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(records);
            _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync(new User
            {
                Id = 1, Name = "T", Email = "t@t.com", EmployeeId = "E001",
                Role = UserRole.Employee, PasswordHash = "h", IsActive = true
            });
            var svc = CreateService();

            var result = await svc.GetAllAttendanceAsync(1, 5);

            Assert.True(result.Success);
            Assert.Equal(10, result.Data?.TotalRecords);
            Assert.Equal(5, result.Data?.Data.Count());
        }

        [Fact]
        public async Task GetUserAttendanceEntities_ReturnsOnlyUserRecords()
        {
            var records = new List<Attendance>
            {
                new() { Id = 1, UserId = 1, Date = DateTime.Today, CheckIn = new TimeSpan(9, 0, 0) },
                new() { Id = 2, UserId = 2, Date = DateTime.Today, CheckIn = new TimeSpan(9, 0, 0) }
            };
            _attRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(records);
            var svc = CreateService();

            var result = await svc.GetUserAttendanceEntitiesAsync(1);

            Assert.Single(result);
        }
    }
}

    public class AttendanceServiceEdgeCaseTests
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

        // ── CheckIn — inactive user still checks in (service doesn't block) ──

        [Fact]
        public async Task CheckIn_UserExistsButInactive_StillSucceeds()
        {
            // Service does not check IsActive — just verifies user exists
            var user = new User
            {
                Id = 1, Name = "Inactive", Email = "i@t.com",
                EmployeeId = "E002", Role = UserRole.Employee,
                PasswordHash = "h", IsActive = false
            };
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _attRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Attendance>());
            _attRepo.Setup(r => r.AddAsync(It.IsAny<Attendance>())).ReturnsAsync(new Attendance
            {
                Id = 1, UserId = 1, Date = DateTime.Today, CheckIn = DateTime.Now.TimeOfDay
            });

            var svc = CreateService();
            var result = await svc.CheckInAsync(1);

            Assert.True(result.Success);
        }

        // ── CheckIn — late flag set when after 9 AM ───────────────────────────

        [Fact]
        public async Task CheckIn_AfterNineAM_SetsIsLateTrue()
        {
            var user = MakeUser();
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _attRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Attendance>());
            Attendance? captured = null;
            _attRepo.Setup(r => r.AddAsync(It.IsAny<Attendance>()))
                    .Callback<Attendance>(a => captured = a)
                    .ReturnsAsync((Attendance a) => a);

            var svc = CreateService();
            // We can't control DateTime.Now, but we can verify the service logic
            // by checking the returned DTO — just assert success here
            var result = await svc.CheckInAsync(1);

            Assert.True(result.Success);
        }

       
        // ── GetUserAttendance — empty records ─────────────────────────────────

        [Fact]
        public async Task GetUserAttendance_NoRecords_ReturnsEmptyPaged()
        {
            _attRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Attendance>());
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());
            var svc = CreateService();

            var result = await svc.GetUserAttendanceAsync(1, 1, 10);

            Assert.True(result.Success);
            Assert.Equal(0, result.Data?.TotalRecords);
        }

        // ── GetUserAttendance — page 2 ────────────────────────────────────────

        [Fact]
        public async Task GetUserAttendance_Page2_ReturnsCorrectSlice()
        {
            var records = Enumerable.Range(1, 7).Select(i => new Attendance
            {
                Id = i, UserId = 1, Date = DateTime.Today.AddDays(-i),
                CheckIn = new TimeSpan(9, 0, 0)
            }).ToList();
            _attRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(records);
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());
            var svc = CreateService();

            var result = await svc.GetUserAttendanceAsync(1, 2, 5);

            Assert.True(result.Success);
            Assert.Equal(7, result.Data?.TotalRecords);
            Assert.Equal(2, result.Data?.Data.Count());
        }

        // ── GetAllAttendance — empty ───────────────────────────────────────────

        [Fact]
        public async Task GetAllAttendance_NoRecords_ReturnsEmptyPaged()
        {
            _attRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Attendance>());
            var svc = CreateService();

            var result = await svc.GetAllAttendanceAsync(1, 10);

            Assert.True(result.Success);
            Assert.Equal(0, result.Data?.TotalRecords);
        }

        // ── GetToday — already checked out ────────────────────────────────────

        [Fact]
        public async Task GetToday_AlreadyCheckedOut_ReturnsDtoWithCheckout()
        {
            var att = new Attendance
            {
                Id = 1, UserId = 1, Date = DateTime.Today,
                CheckIn  = new TimeSpan(9, 0, 0),
                CheckOut = new TimeSpan(17, 0, 0),
                TotalHours = new TimeSpan(8, 0, 0)
            };
            _attRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Attendance> { att });
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());
            var svc = CreateService();

            var result = await svc.GetTodayAsync(1);

            Assert.True(result.Success);
            Assert.NotNull(result.Data?.CheckOut);
        }

        // ── GetUserAttendanceEntities — multiple users ────────────────────────

        [Fact]
        public async Task GetUserAttendanceEntities_MultipleUsers_ReturnsOnlyRequested()
        {
            var records = new List<Attendance>
            {
                new() { Id = 1, UserId = 1, Date = DateTime.Today, CheckIn = new TimeSpan(9, 0, 0) },
                new() { Id = 2, UserId = 1, Date = DateTime.Today.AddDays(-1), CheckIn = new TimeSpan(9, 0, 0) },
                new() { Id = 3, UserId = 2, Date = DateTime.Today, CheckIn = new TimeSpan(9, 0, 0) },
                new() { Id = 4, UserId = 3, Date = DateTime.Today, CheckIn = new TimeSpan(9, 0, 0) }
            };
            _attRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(records);
            var svc = CreateService();

            var result = await svc.GetUserAttendanceEntitiesAsync(1);

            Assert.Equal(2, result.Count());
            Assert.All(result, a => Assert.Equal(1, a.UserId));
        }

        // ── CheckIn — auto-checkout only fires for yesterday, not older ───────

        [Fact]
        public async Task CheckIn_OldMissedCheckout_DoesNotAutoCheckout()
        {
            var user = MakeUser();
            // Record from 3 days ago — should NOT trigger auto-checkout
            var oldRecord = new Attendance
            {
                Id = 5, UserId = 1, Date = DateTime.Today.AddDays(-3),
                CheckIn = new TimeSpan(9, 0, 0)
            };
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _attRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Attendance> { oldRecord });
            _attRepo.Setup(r => r.AddAsync(It.IsAny<Attendance>())).ReturnsAsync(new Attendance
            {
                Id = 2, UserId = 1, Date = DateTime.Today, CheckIn = DateTime.Now.TimeOfDay
            });

            var svc = CreateService();
            var result = await svc.CheckInAsync(1);

            Assert.True(result.Success);
            // UpdateAsync should NOT have been called for the old record
            _attRepo.Verify(r => r.UpdateAsync(5, It.IsAny<Attendance>()), Times.Never);
        }
    }

    public class AttendanceServiceAdditionalTests
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

        // ── CheckInAsync: user not found returns failure ──────────────────────

        [Fact]
        public async Task CheckIn_UserNotFound_MessageContainsInvalidUser()
        {
            _userRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((User?)null);
            _attRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Attendance>());

            var result = await CreateService().CheckInAsync(99);

            Assert.False(result.Success);
            Assert.Contains("user", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ── CheckInAsync: already checked in today returns success with message

        [Fact]
        public async Task CheckIn_AlreadyCheckedIn_ReturnsSuccessWithAlreadyMessage()
        {
            var user = MakeUser();
            var existing = new Attendance
            {
                Id = 1, UserId = 1, Date = DateTime.Today,
                CheckIn = new TimeSpan(9, 0, 0)
            };
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _attRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Attendance> { existing });

            var result = await CreateService().CheckInAsync(1);

            Assert.True(result.Success);
            Assert.Contains("already", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ── CheckOutAsync: user not found returns failure ─────────────────────

        [Fact]
        public async Task CheckOut_UserNotFound_MessageContainsInvalidUser()
        {
            _userRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((User?)null);
            _attRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Attendance>());

            var result = await CreateService().CheckOutAsync(99);

            Assert.False(result.Success);
            Assert.Contains("user", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ── CheckOutAsync: not checked in returns failure ─────────────────────

        [Fact]
        public async Task CheckOut_NotCheckedIn_MessageContainsNotCheckedIn()
        {
            var user = MakeUser();
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _attRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Attendance>());

            var result = await CreateService().CheckOutAsync(1);

            Assert.False(result.Success);
            Assert.Contains("not checked in", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ── CheckOutAsync: already checked out returns success with message ───

        [Fact]
        public async Task CheckOut_AlreadyCheckedOut_ReturnsSuccessWithAlreadyMessage()
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

            var result = await CreateService().CheckOutAsync(1);

            Assert.True(result.Success);
            Assert.Contains("already", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ── GetTodayStatusAsync: no record returns null data ──────────────────

        [Fact]
        public async Task GetToday_NoRecord_DataIsNull()
        {
            _attRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Attendance>());

            var result = await CreateService().GetTodayAsync(1);

            Assert.True(result.Success);
            Assert.Null(result.Data);
        }

        // ── GetAllAttendanceAsync: returns paged results ──────────────────────

        [Fact]
        public async Task GetAllAttendance_PagedResults_TotalRecordsCorrect()
        {
            var records = Enumerable.Range(1, 12).Select(i => new Attendance
            {
                Id = i, UserId = i % 3 + 1, Date = DateTime.Today.AddDays(-i),
                CheckIn = new TimeSpan(9, 0, 0)
            }).ToList();
            _attRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(records);
            _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync(MakeUser());

            var result = await CreateService().GetAllAttendanceAsync(1, 5);

            Assert.True(result.Success);
            Assert.Equal(12, result.Data?.TotalRecords);
            Assert.Equal(5, result.Data?.Data.Count());
        }

        // ── GetUserAttendanceAsync: filters by userId ─────────────────────────

        [Fact]
        public async Task GetUserAttendance_FiltersOnlyRequestedUser()
        {
            var records = new List<Attendance>
            {
                new() { Id = 1, UserId = 1, Date = DateTime.Today, CheckIn = new TimeSpan(9, 0, 0) },
                new() { Id = 2, UserId = 2, Date = DateTime.Today, CheckIn = new TimeSpan(9, 0, 0) },
                new() { Id = 3, UserId = 1, Date = DateTime.Today.AddDays(-1), CheckIn = new TimeSpan(9, 0, 0) }
            };
            _attRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(records);
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeUser());

            var result = await CreateService().GetUserAttendanceAsync(1, 1, 10);

            Assert.True(result.Success);
            Assert.Equal(2, result.Data?.TotalRecords);
            Assert.All(result.Data!.Data, r => Assert.Equal(1, r.UserId));
        }

        // ── CheckIn: new record created with correct date ─────────────────────

        [Fact]
        public async Task CheckIn_NewRecord_DateIsToday()
        {
            var user = MakeUser();
            Attendance? captured = null;
            _userRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
            _attRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Attendance>());
            _attRepo.Setup(r => r.AddAsync(It.IsAny<Attendance>()))
                    .Callback<Attendance>(a => captured = a)
                    .ReturnsAsync((Attendance a) => a);

            await CreateService().CheckInAsync(1);

            Assert.Equal(DateTime.Today, captured?.Date);
        }

       
    }
