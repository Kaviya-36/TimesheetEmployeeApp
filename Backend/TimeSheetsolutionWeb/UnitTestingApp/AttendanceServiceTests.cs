using Moq;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.DTOs;
using TimeSheetAppWeb.Services;
using Xunit;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;

public class AttendanceServiceTests
{
    private readonly Mock<IRepository<int, Attendance>> _mockAttendanceRepo;
    private readonly Mock<IRepository<int, TimeSheetAppWeb.Model.User>> _mockUserRepo;
    private readonly AttendanceService _attendanceService;

    public AttendanceServiceTests()
    {
        _mockAttendanceRepo = new Mock<IRepository<int, Attendance>>();
        _mockUserRepo = new Mock<IRepository<int, TimeSheetAppWeb.Model.User>>();

        _attendanceService = new AttendanceService(
            _mockAttendanceRepo.Object,
            _mockUserRepo.Object
        );
    }

    // ---------------- CHECK IN SUCCESS ----------------
    [Fact]
    public async Task CheckInAsync_ShouldReturnSuccess_WhenUserExists()
    {
        // Arrange
        var user = new TimeSheetAppWeb.Model.User { Id = 1, Name = "John" };

        _mockUserRepo.Setup(x => x.GetByIdAsync(1))
                     .ReturnsAsync(user);

        _mockAttendanceRepo.Setup(x => x.GetAllAsync())
                           .ReturnsAsync(new List<Attendance>());

        _mockAttendanceRepo.Setup(x => x.AddAsync(It.IsAny<Attendance>()))
                           .ReturnsAsync((Attendance a) => a);

        // Act
        var result = await _attendanceService.CheckInAsync(1);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Check-in successful", result.Message);
    }

    // ---------------- CHECK IN FAIL (User Not Found) ----------------
    [Fact]
    public async Task CheckInAsync_ShouldFail_WhenUserDoesNotExist()
    {
        _mockUserRepo.Setup(x => x.GetByIdAsync(99))
                     .ReturnsAsync((TimeSheetAppWeb.Model.User?)null);

        var result = await _attendanceService.CheckInAsync(99);

        Assert.False(result.Success);
        Assert.Equal("Invalid user. User does not exist.", result.Message);
    }

    // ---------------- CHECK OUT FAIL (No CheckIn) ----------------
    [Fact]
    public async Task CheckOutAsync_ShouldFail_WhenNoCheckIn()
    {
        var user = new TimeSheetAppWeb.Model.User { Id = 1, Name = "John" };

        _mockUserRepo.Setup(x => x.GetByIdAsync(1))
                     .ReturnsAsync(user);

        _mockAttendanceRepo.Setup(x => x.GetAllAsync())
                           .ReturnsAsync(new List<Attendance>());

        var result = await _attendanceService.CheckOutAsync(1);

        Assert.False(result.Success);
        Assert.Equal("User has not checked in today.", result.Message);
    }

    // ---------------- GET ALL ATTENDANCE ----------------
    [Fact]
    public async Task GetAllAttendanceAsync_ShouldReturnData()
    {
        var attendanceList = new List<Attendance>
        {
            new Attendance
            {
                Id = 1,
                UserId = 1,
                Date = DateTime.Today,
                CheckIn = new TimeSpan(9,0,0),
                CheckOut = new TimeSpan(18,0,0),
                TotalHours = new TimeSpan(9,0,0)
            }
        };

        var user = new TimeSheetAppWeb.Model.User { Id = 1, Name = "John" };

        _mockAttendanceRepo.Setup(x => x.GetAllAsync())
                           .ReturnsAsync(attendanceList);

        _mockUserRepo.Setup(x => x.GetByIdAsync(1))
                     .ReturnsAsync(user);

        var result = await _attendanceService.GetAllAttendanceAsync();

        Assert.True(result.Success);
        Assert.Single(result.Data);
    }
}