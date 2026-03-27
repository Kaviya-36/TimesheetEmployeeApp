using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.common;
using TimeSheetAppWeb.Model.DTOs;

namespace TimeSheetAppWeb.Services
{
    public class LeaveService : ILeaveService
    {
        private readonly IRepository<int, LeaveRequest> _leaveRepo;
        private readonly IRepository<int, User> _userRepo;
        private readonly IRepository<int, LeaveType> _leaveTypeRepo;
        private readonly INotificationService _notifService;
        private readonly ILogger<LeaveService> _logger;

        public LeaveService(
            IRepository<int, LeaveRequest> leaveRepo,
            IRepository<int, User> userRepo,
            IRepository<int, LeaveType> leaveTypeRepo,
            INotificationService notifService,
            ILogger<LeaveService> logger)
        {
            _leaveRepo = leaveRepo;
            _userRepo = userRepo;
            _leaveTypeRepo = leaveTypeRepo;
            _notifService = notifService;
            _logger = logger;
        }

        // ================= APPLY LEAVE =================
        public async Task<ApiResponse<LeaveResponse>> ApplyLeaveAsync(int userId, LeaveCreateRequest request)
        {
            try
            {
                // 🔹 Validate dates
                if (request.FromDate > request.ToDate)
                    return Fail<LeaveResponse>("From date cannot be after To date");

                var user = await _userRepo.GetByIdAsync(userId);
                if (user == null)
                    return Fail<LeaveResponse>("User not found");

                var leaveType = await _leaveTypeRepo.GetByIdAsync(request.LeaveTypeId);
                if (leaveType == null || !leaveType.IsActive)
                    return Fail<LeaveResponse>("Invalid leave type");

                var totalDays = (request.ToDate - request.FromDate).Days + 1;

                // 🔹 Check max allowed per request
                if (totalDays > leaveType.MaxDaysPerYear)
                    return Fail<LeaveResponse>($"Max {leaveType.MaxDaysPerYear} days allowed");

                var allLeaves = await _leaveRepo.GetAllAsync() ?? Enumerable.Empty<LeaveRequest>();

                // 🔹 Overlap check
                var overlap = allLeaves.Any(l =>
                    l.UserId == userId &&
                    l.Status != LeaveStatus.Rejected &&
                    l.FromDate <= request.ToDate &&
                    l.ToDate >= request.FromDate);

                if (overlap)
                    return Fail<LeaveResponse>("Leave already exists in selected range");

                // ✅ CALCULATE USED LEAVES (APPROVED + PENDING optional)
                var usedLeaves = allLeaves
                    .Where(l => l.UserId == userId &&
                                l.LeaveTypeId == request.LeaveTypeId &&
                                l.Status != LeaveStatus.Rejected)
                    .Sum(l => (l.ToDate - l.FromDate).Days + 1);

                // ✅ CALCULATE REMAINING
                var remainingLeaves = leaveType.MaxDaysPerYear - usedLeaves - totalDays;

                if (remainingLeaves < 0)
                {
                    return Fail<LeaveResponse>(
                        $"Not enough leave balance. You only have {leaveType.MaxDaysPerYear - usedLeaves} days left"
                    );
                }

                var leave = new LeaveRequest
                {
                    UserId = userId,
                    LeaveTypeId = request.LeaveTypeId,
                    FromDate = request.FromDate,
                    ToDate = request.ToDate,
                    Reason = request.Reason,
                    Status = LeaveStatus.Pending
                };

                await _leaveRepo.AddAsync(leave);

                // Notify managers and HR about the new leave request
                await _notifService.SendToRoleAsync("Manager", "Leave", $"{user.Name} applied for {leaveType.Name} leave from {request.FromDate:dd MMM} to {request.ToDate:dd MMM yyyy}.");
                await _notifService.SendToRoleAsync("HR",      "Leave", $"{user.Name} applied for {leaveType.Name} leave from {request.FromDate:dd MMM} to {request.ToDate:dd MMM yyyy}.");

                return Success("Leave applied successfully", MapToDto(leave, user, leaveType, remainingLeaves));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ApplyLeave error");
                return Fail<LeaveResponse>(ex.Message);
            }
        }

        // ================= APPROVE / REJECT =================
        public async Task<ApiResponse<bool>> ApproveOrRejectLeaveAsync(LeaveApprovalRequest request)
        {
            try
            {
                var leave = await _leaveRepo.GetByIdAsync(request.LeaveId);
                if (leave == null)
                    return Fail<bool>("Leave not found");

                if (leave.UserId == request.ApprovedById)
                    return Fail<bool>("Cannot approve your own leave");

                if (leave.Status != LeaveStatus.Pending)
                    return Fail<bool>("Leave already processed");

                leave.Status = request.IsApproved ? LeaveStatus.Approved : LeaveStatus.Rejected;
                leave.ApprovedById = request.ApprovedById;
                leave.ApprovedDate = DateTime.Now;
                leave.ManagerComment = request.ManagerComment;

                await _leaveRepo.UpdateAsync(leave.Id, leave);

                // Notify the employee who applied the leave
                var statusText = request.IsApproved ? "approved" : "rejected";
                await _notifService.SendToUserAsync(leave.UserId, "Leave",
                    $"Your leave request has been {statusText}.{(string.IsNullOrEmpty(request.ManagerComment) ? "" : $" Comment: {request.ManagerComment}")}");

                return Success($"Leave {leave.Status}", true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ApproveLeave error");
                return Fail<bool>(ex.Message);
            }
        }

        // ================= GET MY LEAVES =================
        public async Task<ApiResponse<PagedResponse<LeaveResponse>>> GetUserLeavesAsync(int userId, int pageNumber, int pageSize, string? search = null, string? status = null, string? sortDir = "desc")
        {
            try
            {
                var user = await _userRepo.GetByIdAsync(userId);
                if (user == null) return Fail<PagedResponse<LeaveResponse>>("User not found");

                var leaves    = await _leaveRepo.GetAllAsync()     ?? Enumerable.Empty<LeaveRequest>();
                var leaveTypes = await _leaveTypeRepo.GetAllAsync() ?? Enumerable.Empty<LeaveType>();

                var filtered = leaves.Where(l => l.UserId == userId);
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var q = search.ToLower();
                    filtered = filtered.Where(l => leaveTypes.FirstOrDefault(t => t.Id == l.LeaveTypeId)?.Name.ToLower().Contains(q) == true
                                                || (l.Reason ?? "").ToLower().Contains(q));
                }
                if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<LeaveStatus>(status, true, out var st))
                    filtered = filtered.Where(l => l.Status == st);

                filtered = sortDir?.ToLower() == "asc"
                    ? filtered.OrderBy(l => l.FromDate)
                    : filtered.OrderByDescending(l => l.FromDate);

                var total = filtered.Count();
                var data  = filtered.Skip((pageNumber - 1) * pageSize).Take(pageSize)
                    .Select(l => { var type = leaveTypes.First(t => t.Id == l.LeaveTypeId); return MapToDto(l, user, type); }).ToList();

                return Success("Fetched", new PagedResponse<LeaveResponse>(data, total, pageNumber, pageSize));
            }
            catch (Exception ex) { _logger.LogError(ex, "GetUserLeaves error"); return Fail<PagedResponse<LeaveResponse>>(ex.Message); }
        }

        // ================= GET ALL LEAVES =================
        public async Task<ApiResponse<PagedResponse<LeaveResponse>>> GetAllLeavesAsync(int pageNumber, int pageSize, string? search = null, string? status = null, string? sortDir = "desc")
        {
            try
            {
                var leaves    = await _leaveRepo.GetAllAsync()     ?? Enumerable.Empty<LeaveRequest>();
                var users     = await _userRepo.GetAllAsync()      ?? Enumerable.Empty<User>();
                var types     = await _leaveTypeRepo.GetAllAsync() ?? Enumerable.Empty<LeaveType>();

                var filtered = leaves.AsEnumerable();
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var q = search.ToLower();
                    var matchingUserIds = users.Where(u => u.Name.ToLower().Contains(q)).Select(u => u.Id).ToHashSet();
                    filtered = filtered.Where(l => matchingUserIds.Contains(l.UserId)
                                                || types.FirstOrDefault(t => t.Id == l.LeaveTypeId)?.Name.ToLower().Contains(q) == true);
                }
                if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<LeaveStatus>(status, true, out var st))
                    filtered = filtered.Where(l => l.Status == st);

                filtered = sortDir?.ToLower() == "asc"
                    ? filtered.OrderBy(l => l.FromDate)
                    : filtered.OrderByDescending(l => l.FromDate);

                var total = filtered.Count();
                var data  = filtered.Skip((pageNumber - 1) * pageSize).Take(pageSize)
                    .Select(l => { var user = users.First(u => u.Id == l.UserId); var type = types.First(t => t.Id == l.LeaveTypeId); return MapToDto(l, user, type); }).ToList();

                return Success("Fetched", new PagedResponse<LeaveResponse>(data, total, pageNumber, pageSize));
            }
            catch (Exception ex) { _logger.LogError(ex, "GetAllLeaves error"); return Fail<PagedResponse<LeaveResponse>>(ex.Message); }
        }

        // ================= DELETE LEAVE =================
        public async Task<ApiResponse<bool>> DeleteLeaveAsync(int leaveId, int userId)
        {
            try
            {
                var leave = await _leaveRepo.GetByIdAsync(leaveId);
                if (leave == null) return Fail<bool>("Leave not found");
                if (leave.UserId != userId) return Fail<bool>("Not authorized");
                if (leave.Status != LeaveStatus.Pending) return Fail<bool>("Only pending leaves can be deleted");
                await _leaveRepo.DeleteAsync(leaveId);
                return Success("Leave deleted", true);
            }
            catch (Exception ex) { _logger.LogError(ex, "DeleteLeave error"); return Fail<bool>(ex.Message); }
        }

        // ================= GET LEAVE TYPES =================
        public async Task<ApiResponse<IEnumerable<LeaveType>>> GetLeaveTypesAsync()
        {
            var types = await _leaveTypeRepo.GetAllAsync();

            return new ApiResponse<IEnumerable<LeaveType>>
            {
                Success = true,
                Data = types?.Where(t => t.IsActive) ?? Enumerable.Empty<LeaveType>()
            };
        }

        // ================= HELPER =================
        private LeaveResponse MapToDto(LeaveRequest leave, User user, LeaveType type, int remainingLeaves = 0)
        {
            return new LeaveResponse
            {
                Id = leave.Id,
                EmployeeName = user.Name,
                LeaveType = type.Name,
                FromDate = leave.FromDate,
                ToDate = leave.ToDate,
                Reason = leave.Reason,
                Status = leave.Status,
                ApprovedById = leave.ApprovedById,
                ApprovedDate = leave.ApprovedDate,
                ManagerComment = leave.ManagerComment,
                RemainingLeaves = remainingLeaves
            };
        }

        private ApiResponse<T> Success<T>(string msg, T data) =>
            new() { Success = true, Message = msg, Data = data };

        private ApiResponse<T> Fail<T>(string msg) =>
            new() { Success = false, Message = msg };
    }
}