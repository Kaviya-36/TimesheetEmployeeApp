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
        private readonly ILogger<LeaveService> _logger;

        public LeaveService(
            IRepository<int, LeaveRequest> leaveRepo,
            IRepository<int, User> userRepo,
            IRepository<int, LeaveType> leaveTypeRepo,
            ILogger<LeaveService> logger)
        {
            _leaveRepo = leaveRepo;
            _userRepo = userRepo;
            _leaveTypeRepo = leaveTypeRepo;
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

                // 🔹 Check max allowed days
                if (totalDays > leaveType.MaxDaysPerYear)
                    return Fail<LeaveResponse>($"Max {leaveType.MaxDaysPerYear} days allowed");

                // 🔹 Check overlapping leaves
                var allLeaves = await _leaveRepo.GetAllAsync() ?? Enumerable.Empty<LeaveRequest>();

                var overlap = allLeaves.Any(l =>
                    l.UserId == userId &&
                    l.Status != LeaveStatus.Rejected &&
                    l.FromDate <= request.ToDate &&
                    l.ToDate >= request.FromDate);

                if (overlap)
                    return Fail<LeaveResponse>("Leave already exists in selected range");

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

                return Success("Leave applied successfully",
                    MapToDto(leave, user, leaveType));
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

                return Success($"Leave {leave.Status}", true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ApproveLeave error");
                return Fail<bool>(ex.Message);
            }
        }

        // ================= GET MY LEAVES =================
        public async Task<ApiResponse<PagedResponse<LeaveResponse>>> GetUserLeavesAsync(int userId, int pageNumber, int pageSize)
        {
            try
            {
                var user = await _userRepo.GetByIdAsync(userId);
                if (user == null)
                    return Fail<PagedResponse<LeaveResponse>>("User not found");

                var leaves = await _leaveRepo.GetAllAsync() ?? Enumerable.Empty<LeaveRequest>();
                var leaveTypes = await _leaveTypeRepo.GetAllAsync() ?? Enumerable.Empty<LeaveType>();

                var filtered = leaves.Where(l => l.UserId == userId);

                var total = filtered.Count();

                var data = filtered
                    .OrderByDescending(l => l.FromDate)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(l =>
                    {
                        var type = leaveTypes.First(t => t.Id == l.LeaveTypeId);
                        return MapToDto(l, user, type);
                    })
                    .ToList();

                return Success("Fetched", new PagedResponse<LeaveResponse>(data, total, pageNumber, pageSize));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetUserLeaves error");
                return Fail<PagedResponse<LeaveResponse>>(ex.Message);
            }
        }

        // ================= GET ALL LEAVES =================
        public async Task<ApiResponse<PagedResponse<LeaveResponse>>> GetAllLeavesAsync(int pageNumber, int pageSize)
        {
            try
            {
                var leaves = await _leaveRepo.GetAllAsync() ?? Enumerable.Empty<LeaveRequest>();
                var users = await _userRepo.GetAllAsync() ?? Enumerable.Empty<User>();
                var types = await _leaveTypeRepo.GetAllAsync() ?? Enumerable.Empty<LeaveType>();

                var total = leaves.Count();

                var data = leaves
                    .OrderByDescending(l => l.FromDate)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(l =>
                    {
                        var user = users.First(u => u.Id == l.UserId);
                        var type = types.First(t => t.Id == l.LeaveTypeId);
                        return MapToDto(l, user, type);
                    })
                    .ToList();

                return Success("Fetched", new PagedResponse<LeaveResponse>(data, total, pageNumber, pageSize));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetAllLeaves error");
                return Fail<PagedResponse<LeaveResponse>>(ex.Message);
            }
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
        private LeaveResponse MapToDto(LeaveRequest leave, User user, LeaveType type)
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
                ManagerComment = leave.ManagerComment
            };
        }

        private ApiResponse<T> Success<T>(string msg, T data) =>
            new() { Success = true, Message = msg, Data = data };

        private ApiResponse<T> Fail<T>(string msg) =>
            new() { Success = false, Message = msg };
    }
}