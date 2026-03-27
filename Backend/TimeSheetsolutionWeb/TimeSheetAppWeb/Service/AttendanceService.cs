using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.common;
using TimeSheetAppWeb.Model.DTOs;

namespace TimeSheetAppWeb.Services
{
    public class AttendanceService : IAttendanceService
    {
        private readonly IRepository<int, Attendance> _attendanceRepository;
        private readonly IRepository<int, User> _userRepository;

        public AttendanceService(IRepository<int, Attendance> attendanceRepository,
                                 IRepository<int, User> userRepository)
        {
            _attendanceRepository = attendanceRepository;
            _userRepository = userRepository;
        }

        // ---------------- CHECK IN ----------------
        public async Task<ApiResponse<AttendanceResponse>> CheckInAsync(int userId)
        {
            try
            {
                var today = DateTime.Today;

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                    return new ApiResponse<AttendanceResponse> { Success = false, Message = "Invalid user. User does not exist." };

                var allAttendances = await _attendanceRepository.GetAllAsync() ?? Enumerable.Empty<Attendance>();

                // ── Auto-checkout yesterday if missed ──────────────────────────────
                var yesterday = today.AddDays(-1);
                var prevRecord = allAttendances.FirstOrDefault(a => a.UserId == userId && a.Date.Date == yesterday);
                if (prevRecord != null && prevRecord.CheckIn.HasValue && !prevRecord.CheckOut.HasValue)
                {
                    var autoCheckout = CalcAutoCheckout(prevRecord.CheckIn.Value);
                    var tracked = await _attendanceRepository.GetByIdAsync(prevRecord.Id);
                    if (tracked != null)
                    {
                        tracked.CheckOut   = autoCheckout.checkOut;
                        tracked.TotalHours = autoCheckout.totalHours;
                        await _attendanceRepository.UpdateAsync(tracked.Id, tracked);
                    }
                }

                var attendance = allAttendances.FirstOrDefault(a => a.UserId == userId && a.Date.Date == today);

                if (attendance != null && attendance.CheckIn.HasValue)
                {
                    // Already checked in — return the existing record as success so frontend stays in sync
                    return new ApiResponse<AttendanceResponse>
                    {
                        Success = true,
                        Message = "Already checked in today.",
                        Data = await MapToDtoAsync(attendance)
                    };
                }

                var currentTime = DateTime.Now.TimeOfDay;

                if (attendance == null)
                {
                    attendance = new Attendance
                    {
                        UserId = userId,
                        Date = today,
                        CheckIn = currentTime,
                        IsLate = currentTime > new TimeSpan(9, 0, 0)
                    };
                    await _attendanceRepository.AddAsync(attendance);
                }
                else
                {
                    attendance.CheckIn = currentTime;
                    attendance.IsLate = currentTime > new TimeSpan(9, 0, 0);
                    await _attendanceRepository.UpdateAsync(attendance.Id, attendance);
                }

                return new ApiResponse<AttendanceResponse>
                {
                    Success = true,
                    Message = "Check-in successful",
                    Data = await MapToDtoAsync(attendance)
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<AttendanceResponse> { Success = false, Message = $"An error occurred during check-in: {ex.Message} | Inner: {ex.InnerException?.Message ?? "none"}" };
            }
        }

        // ---------------- CHECK OUT ----------------
        public async Task<ApiResponse<AttendanceResponse>> CheckOutAsync(int userId)
        {
            try
            {
                var today = DateTime.Today;

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                    return new ApiResponse<AttendanceResponse>
                    {
                        Success = false,
                        Message = "Invalid user. User does not exist."
                    };

                var allAttendances = await _attendanceRepository.GetAllAsync() ?? Enumerable.Empty<Attendance>();
                var attendance = allAttendances.FirstOrDefault(a => a.UserId == userId && a.Date.Date == today);

                if (attendance == null || !attendance.CheckIn.HasValue)
                {
                    return new ApiResponse<AttendanceResponse>
                    {
                        Success = false,
                        Message = "User has not checked in today."
                    };
                }

                if (attendance.CheckOut.HasValue)
                {
                    return new ApiResponse<AttendanceResponse>
                    {
                        Success = true,
                        Message = "Already checked out today.",
                        Data = await MapToDtoAsync(attendance)
                    };
                }

                var currentTime = DateTime.Now.TimeOfDay;
                attendance.CheckOut = currentTime;
                attendance.TotalHours = attendance.CheckOut.Value - attendance.CheckIn.Value;

                await _attendanceRepository.UpdateAsync(attendance.Id, attendance);

                return new ApiResponse<AttendanceResponse>
                {
                    Success = true,
                    Message = "Check-out successful",
                    Data = await MapToDtoAsync(attendance)
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<AttendanceResponse>
                {
                    Success = false,
                    Message = $"An error occurred during check-out: {ex.Message}"
                };
            }
        }
        public async Task<ApiResponse<AttendanceResponse>> GetTodayAsync(int userId)
        {
            try
            {
                var today = DateTime.Today;
                var yesterday = today.AddDays(-1);
                var allAttendances = await _attendanceRepository.GetAllAsync() ?? Enumerable.Empty<Attendance>();

                // ── Auto-checkout yesterday if missed, then flag it ────────────────
                var prevRecord = allAttendances.FirstOrDefault(a => a.UserId == userId && a.Date.Date == yesterday);
                bool missedCheckout = false;
                if (prevRecord != null && prevRecord.CheckIn.HasValue && !prevRecord.CheckOut.HasValue)
                {
                    missedCheckout = true;
                    var autoCheckout = CalcAutoCheckout(prevRecord.CheckIn.Value);
                    var tracked = await _attendanceRepository.GetByIdAsync(prevRecord.Id);
                    if (tracked != null)
                    {
                        tracked.CheckOut   = autoCheckout.checkOut;
                        tracked.TotalHours = autoCheckout.totalHours;
                        await _attendanceRepository.UpdateAsync(tracked.Id, tracked);
                    }
                }

                var attendance = allAttendances.FirstOrDefault(a => a.UserId == userId && a.Date.Date == today);

                if (attendance == null)
                    return new ApiResponse<AttendanceResponse>
                    {
                        Success = true,
                        Message = missedCheckout ? "missed_checkout" : "No attendance for today",
                        Data = missedCheckout ? new AttendanceResponse { MissedCheckout = true } : null
                    };

                // ── Auto check-out after 12 hours (same-day safety net) ────────────
                if (attendance.CheckIn.HasValue && !attendance.CheckOut.HasValue)
                {
                    var autoCheckoutTime = attendance.Date.Add(attendance.CheckIn.Value).AddHours(12);
                    if (DateTime.Now >= autoCheckoutTime)
                    {
                        var proposed = attendance.CheckIn.Value.Add(TimeSpan.FromHours(12));
                        var maxTime  = new TimeSpan(23, 59, 59);
                        attendance.CheckOut   = proposed > maxTime ? maxTime : proposed;
                        attendance.TotalHours = attendance.CheckOut.Value - attendance.CheckIn.Value;
                        await _attendanceRepository.UpdateAsync(attendance.Id, attendance);
                    }
                }

                var dto = await MapToDtoAsync(attendance);
                dto.MissedCheckout = missedCheckout;

                return new ApiResponse<AttendanceResponse> { Success = true, Data = dto };
            }
            catch (Exception ex)
            {
                return new ApiResponse<AttendanceResponse> { Success = false, Message = ex.Message };
            }
        }

        // ---------------- GET USER ATTENDANCE ----------------
        public async Task<ApiResponse<PagedResponse<AttendanceResponse>>> GetUserAttendanceAsync(int userId, int pageNumber, int pageSize)
        {
            try
            {
                var allAttendances = (await _attendanceRepository.GetAllAsync() ?? Enumerable.Empty<Attendance>())
                    .Where(a => a.UserId == userId)
                    .OrderByDescending(a => a.Date);

                var totalRecords = allAttendances.Count();

                var pagedData = allAttendances
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize);

                var result = new List<AttendanceResponse>();
                foreach (var a in pagedData)
                    result.Add(await MapToDtoAsync(a));
                return new ApiResponse<PagedResponse<AttendanceResponse>>
                {
                    Success = true,
                    Data = new PagedResponse<AttendanceResponse>(
                    result,
                    totalRecords,
                    pageNumber,
                    pageSize
                )
                };


            }
            catch (Exception ex)
            {
                return new ApiResponse<PagedResponse<AttendanceResponse>>
                {
                    Success = false,
                    Message = $"Error fetching user attendance: {ex.Message}"
                };
            }
        }

        public async Task<IEnumerable<Attendance>> GetUserAttendanceEntitiesAsync(int userId)
        {
            var allAttendances = await _attendanceRepository.GetAllAsync() ?? Enumerable.Empty<Attendance>();
            return allAttendances.Where(a => a.UserId == userId);
        }
        // ---------------- GET ALL ATTENDANCE ----------------
        public async Task<ApiResponse<PagedResponse<AttendanceResponse>>> GetAllAttendanceAsync(int pageNumber, int pageSize)
        {
            try
            {
                var allAttendances = (await _attendanceRepository.GetAllAsync() ?? Enumerable.Empty<Attendance>())
                    .OrderByDescending(a => a.Date);

                var totalRecords = allAttendances.Count();

                var pagedData = allAttendances
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize);

                var result = new List<AttendanceResponse>();
                foreach (var a in pagedData)
                    result.Add(await MapToDtoAsync(a));
                return new ApiResponse<PagedResponse<AttendanceResponse>>
                {
                    Success = true,
                    Data = new PagedResponse<AttendanceResponse>(
                        result,
                        totalRecords,
                        pageNumber,
                        pageSize
                    )
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<PagedResponse<AttendanceResponse>>
                {
                    Success = false,
                    Message = $"Error fetching attendance: {ex.Message}"
                };
            }
        }


    private static (TimeSpan checkOut, TimeSpan totalHours) CalcAutoCheckout(TimeSpan checkIn)
    {
        var proposed = checkIn.Add(TimeSpan.FromHours(8));
        // SQL time column max is 23:59:59 — cap if overflow
        var maxTime = new TimeSpan(23, 59, 59);
        var checkOut   = proposed > maxTime ? maxTime : proposed;
        var totalHours = checkOut - checkIn;
        return (checkOut, totalHours);
    }
        private async Task<AttendanceResponse> MapToDtoAsync(Attendance attendance)
        {
            string employeeName = "Unknown";
            try
            {
                if (attendance.User == null)
                {
                    var user = await _userRepository.GetByIdAsync(attendance.UserId);
                    if (user != null) employeeName = user.Name;
                }
                else
                {
                    employeeName = attendance.User.Name;
                }
            }
            catch
            {
                // Ignore mapping errors, keep employeeName as "Unknown"
            }

            return new AttendanceResponse
            {
                Id = attendance.Id,
                UserId = attendance.UserId,
                EmployeeName = employeeName,
                Date = attendance.Date,
                CheckIn = attendance.CheckIn?.ToString(@"hh\:mm"),
                CheckOut = attendance.CheckOut?.ToString(@"hh\:mm"),
                IsLate = attendance.IsLate,
                TotalHours = attendance.TotalHours.ToString(@"hh\:mm") ?? "00:00"
            };
        }
    }
}