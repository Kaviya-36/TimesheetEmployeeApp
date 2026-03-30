using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.common;
using TimeSheetAppWeb.Model.DTOs;
namespace TimeSheetAppWeb.Services
{
    public class TimesheetService : ITimesheetService
    {
        private readonly IRepository<int, Timesheet> _timesheetRepository;
        private readonly IRepository<int, User> _userRepository;
        private readonly IRepository<int, Project> _projectRepository;
        private readonly IProjectService _projectService;
        private readonly IAttendanceService _attendanceService;
        private readonly INotificationService _notifService;
        private readonly ILogger<TimesheetService> _logger;

        public TimesheetService(
            IRepository<int, Timesheet> timesheetRepository,
            IRepository<int, User> userRepository,
            IRepository<int, Project> projectRepository,
            IProjectService projectService,
            IAttendanceService attendanceService,
            INotificationService notifService,
            ILogger<TimesheetService> logger)
        {
            _timesheetRepository = timesheetRepository;
            _userRepository = userRepository;
            _projectRepository = projectRepository;
            _projectService = projectService;
            _attendanceService = attendanceService;
            _notifService = notifService;
            _logger = logger;
        }

        // ---------------- CREATE FROM GRID (hours only) ----------------
        public async Task<ApiResponse<TimesheetResponse>> CreateFromGridAsync(int userId, TimesheetGridRequest request)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null) return new ApiResponse<TimesheetResponse> { Success = false, Message = "User not found" };

                Project? project = null;
                try { project = await _projectRepository.GetByIdAsync(request.ProjectId); } catch { }
                if (project == null)
                {
                    // Fallback: find by name
                    var allProjects = await _projectRepository.GetAllAsync() ?? Enumerable.Empty<Project>();
                    project = allProjects.FirstOrDefault(p => p.ProjectName == request.ProjectName);
                }
                if (project == null) return new ApiResponse<TimesheetResponse> { Success = false, Message = $"Invalid project (id={request.ProjectId}, name={request.ProjectName})" };

                if (request.Hours <= 0 || request.Hours > 24)
                    return new ApiResponse<TimesheetResponse> { Success = false, Message = "Hours must be between 0 and 24" };

                if (!DateTime.TryParse(request.WorkDate, out var workDate))
                    return new ApiResponse<TimesheetResponse> { Success = false, Message = $"Invalid date: {request.WorkDate}" };

                // Check for existing timesheet on same date+project
                var all = await _timesheetRepository.GetAllAsync() ?? Enumerable.Empty<Timesheet>();
                var existing = all.FirstOrDefault(t =>
                    t.UserId == userId &&
                    t.ProjectId == project.Id &&
                    t.WorkDate.Date == workDate.Date);

                var startTime = new TimeSpan(9, 0, 0);
                var totalMinutes = (int)Math.Round(request.Hours * 60);
                var endTime = startTime.Add(TimeSpan.FromMinutes(totalMinutes));
                if (endTime.TotalHours > 23) endTime = new TimeSpan(23, 59, 0);

                if (existing != null)
                {
                    // Update existing pending timesheet
                    if (existing.Status == TimesheetStatus.Approved)
                        return new ApiResponse<TimesheetResponse> { Success = false, Message = "Approved timesheets cannot be updated" };

                    existing.StartTime  = startTime;
                    existing.EndTime    = endTime;
                    existing.TotalHours = request.Hours;
                    if (!string.IsNullOrEmpty(request.TaskDescription))
                        existing.TaskDescription = request.TaskDescription;
                    await _timesheetRepository.UpdateAsync(existing.Id, existing);

                    return new ApiResponse<TimesheetResponse>
                    {
                        Success = true, Message = "Timesheet updated",
                        Data = MapToDto(existing, user, project)
                    };
                }

                var timesheet = new Timesheet
                {
                    UserId      = userId,
                    ProjectId   = project.Id,
                    ProjectName = project.ProjectName,
                    WorkDate    = workDate,
                    StartTime   = startTime,
                    EndTime     = endTime,
                    BreakTime   = TimeSpan.Zero,
                    TotalHours  = request.Hours,
                    TaskDescription = request.TaskDescription ?? "",
                    Status      = TimesheetStatus.Pending
                };

                await _timesheetRepository.AddAsync(timesheet);

                // Notify managers/HR
                await _notifService.SendToRoleAsync("Manager", "Timesheet", $"{user.Name} submitted a timesheet for {project.ProjectName} on {workDate:dd MMM yyyy}.");
                await _notifService.SendToRoleAsync("HR",      "Timesheet", $"{user.Name} submitted a timesheet for {project.ProjectName} on {workDate:dd MMM yyyy}.");

                return new ApiResponse<TimesheetResponse>
                {
                    Success = true, Message = "Timesheet created",
                    Data = MapToDto(timesheet, user, project)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating grid timesheet for UserId={UserId}", userId);
                return new ApiResponse<TimesheetResponse> { Success = false, Message = $"Error: {ex.Message} | Inner: {ex.InnerException?.Message ?? "none"}" };
            }
        }

        // ---------------- SUBMIT WEEKLY TIMESHEET (batch) ----------------
        public async Task<ApiResponse<TimesheetWeeklyResponse>> SubmitWeeklyAsync(int userId, TimesheetWeeklyRequest request)
        {
            var result = new TimesheetWeeklyResponse();
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null) return new ApiResponse<TimesheetWeeklyResponse> { Success = false, Message = "User not found" };

                var allProjects = (await _projectRepository.GetAllAsync() ?? Enumerable.Empty<Project>()).ToList();
                var allTs       = (await _timesheetRepository.GetAllAsync() ?? Enumerable.Empty<Timesheet>()).ToList();

                foreach (var entry in request.Entries)
                {
                    if (entry.Hours <= 0) { result.Skipped++; continue; }

                    if (!DateTime.TryParse(entry.WorkDate, out var workDate))
                    { result.Errors.Add($"Invalid date: {entry.WorkDate}"); result.Skipped++; continue; }

                    // Resolve project
                    Project? project = null;
                    try { project = await _projectRepository.GetByIdAsync(entry.ProjectId); } catch { }
                    if (project == null)
                        project = allProjects.FirstOrDefault(p => p.ProjectName == entry.ProjectName);
                    if (project == null)
                    { result.Errors.Add($"Project not found: {entry.ProjectName}"); result.Skipped++; continue; }

                    var startTime = new TimeSpan(9, 0, 0);
                    var totalMinutes = (int)Math.Round(entry.Hours * 60);
                    var endTime = startTime.Add(TimeSpan.FromMinutes(totalMinutes));
                    if (endTime.TotalHours > 23) endTime = new TimeSpan(23, 59, 0);

                    var existing = allTs.FirstOrDefault(t =>
                        t.UserId == userId && t.ProjectId == project.Id && t.WorkDate.Date == workDate.Date);

                    if (existing != null)
                    {
                        if (existing.Status == TimesheetStatus.Approved)
                        { result.AlreadyApproved++; continue; }

                        existing.StartTime  = startTime;
                        existing.EndTime    = endTime;
                        existing.TotalHours = entry.Hours;
                        if (!string.IsNullOrEmpty(entry.TaskDescription))
                            existing.TaskDescription = entry.TaskDescription;
                        if (request.Submit)
                            existing.Status = TimesheetStatus.Pending;

                        await _timesheetRepository.UpdateAsync(existing.Id, existing);
                        result.Updated++;
                    }
                    else
                    {
                        var ts = new Timesheet
                        {
                            UserId          = userId,
                            ProjectId       = project.Id,
                            ProjectName     = project.ProjectName,
                            WorkDate        = workDate,
                            StartTime       = startTime,
                            EndTime         = endTime,
                            BreakTime       = TimeSpan.Zero,
                            TotalHours      = entry.Hours,
                            TaskDescription = entry.TaskDescription ?? "",
                            Status          = TimesheetStatus.Pending
                        };
                        await _timesheetRepository.AddAsync(ts);
                        result.Saved++;
                    }
                }

                if (request.Submit && (result.Saved + result.Updated) > 0)
                {
                    await _notifService.SendToRoleAsync("Manager", "Timesheet",
                        $"{user.Name} submitted a weekly timesheet ({result.Saved + result.Updated} entries).");
                    await _notifService.SendToRoleAsync("HR", "Timesheet",
                        $"{user.Name} submitted a weekly timesheet ({result.Saved + result.Updated} entries).");
                }

                return new ApiResponse<TimesheetWeeklyResponse>
                {
                    Success = true,
                    Message = request.Submit
                        ? $"Weekly timesheet submitted: {result.Saved} new, {result.Updated} updated" +
                          (result.AlreadyApproved > 0 ? $", {result.AlreadyApproved} already approved (unchanged)" : "") +
                          (result.Skipped > 0 ? $", {result.Skipped} skipped" : "") + "."
                        : $"Saved: {result.Saved} new, {result.Updated} updated" +
                          (result.AlreadyApproved > 0 ? $", {result.AlreadyApproved} already approved (unchanged)" : "") +
                          (result.Skipped > 0 ? $", {result.Skipped} skipped" : "") + ".",
                    Data = result
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting weekly timesheet for UserId={UserId}", userId);
                return new ApiResponse<TimesheetWeeklyResponse> { Success = false, Message = $"Error: {ex.Message}" };
            }
        }

        // ---------------- CREATE MANUAL TIMESHEET ----------------
        public async Task<ApiResponse<TimesheetResponse>> CreateManualTimesheetAsync(int userId, TimesheetCreateRequest request)
        {            try
            {
                _logger.LogInformation("Manual timesheet creation for UserId {UserId}", userId);

                // ✅ Check user
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                    return new ApiResponse<TimesheetResponse> { Success = false, Message = "User not found" };


                // ✅ Check project
                var project = await _projectRepository.GetByIdAsync(request.ProjectId);
                if (project == null)
                {
                    return new ApiResponse<TimesheetResponse>
                    {
                        Success = false,
                        Message = "Invalid project"
                    };
                }

                // 🔥 NEW: Check project assignment
                var assignments = await _projectService.GetUserProjectAssignmentsAsync(userId);

                var isAssigned = assignments.Data
                    ?.Any(a => a.ProjectId == request.ProjectId) ?? false;

                if (!isAssigned)
                {
                    return new ApiResponse<TimesheetResponse>
                    {
                        Success = false,
                        Message = "User is not assigned to this project"
                    };
                }

                // ❌ Prevent duplicate
                var exists = (await _timesheetRepository.GetAllAsync())
                    ?.Any(t => t.UserId == userId && t.WorkDate.Date == request.WorkDate.Date);

                if (exists == true)
                {
                    return new ApiResponse<TimesheetResponse>
                    {
                        Success = false,
                        Message = "Timesheet already exists for this date"
                    };
                }

                // ❌ Validate time
                if (request.EndTime <= request.StartTime)
                {
                    return new ApiResponse<TimesheetResponse>
                    {
                        Success = false,
                        Message = "End time must be greater than start time"
                    };
                }

                var totalHours = (request.EndTime - request.StartTime - request.BreakTime).TotalHours;

                if (totalHours <= 0)
                {
                    return new ApiResponse<TimesheetResponse>
                    {
                        Success = false,
                        Message = "Invalid working hours"
                    };
                }

                // ✅ Create timesheet
                var timesheet = new Timesheet
                {
                    UserId = userId,
                    ProjectId = request.ProjectId,
                    ProjectName = project.ProjectName,
                    WorkDate = request.WorkDate,
                    StartTime = request.StartTime,
                    EndTime = request.EndTime,
                    BreakTime = request.BreakTime,
                    TaskDescription = request.TaskDescription ?? "",
                    TotalHours = totalHours,
                    Status = TimesheetStatus.Pending
                };

                await _timesheetRepository.AddAsync(timesheet);

                // Notify managers and HR that a new timesheet needs review
                await _notifService.SendToRoleAsync("Manager", "Timesheet", $"{user.Name} submitted a timesheet for {project.ProjectName} on {request.WorkDate:dd MMM yyyy}.");
                await _notifService.SendToRoleAsync("HR",      "Timesheet", $"{user.Name} submitted a timesheet for {project.ProjectName} on {request.WorkDate:dd MMM yyyy}.");

                return new ApiResponse<TimesheetResponse>
                {
                    Success = true,
                    Message = "Timesheet created successfully",
                    Data = MapToDto(timesheet, user, project)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating manual timesheet");
                return new ApiResponse<TimesheetResponse>
                {
                    Success = false,
                    Message = "Error creating timesheet"
                };
            }
        }

        // ---------------- CREATE TIMESHEET ----------------
        public async Task<ApiResponse<TimesheetResponse>> CreateTimesheetFromAttendanceAsync(int userId, Attendance attendance)
        {
            try
            {
                _logger.LogInformation("Creating timesheet for UserId {UserId} on Date {Date}", userId, attendance.Date);

                if (!attendance.CheckIn.HasValue || !attendance.CheckOut.HasValue)
                {
                    _logger.LogWarning("Attendance incomplete for UserId {UserId} on Date {Date}", userId, attendance.Date);
                    return new ApiResponse<TimesheetResponse>
                    {
                        Success = false,
                        Message = "Attendance incomplete"
                    };
                }

                var existing = (await _timesheetRepository.GetAllAsync())
                    ?.FirstOrDefault(t => t.UserId == userId && t.WorkDate.Date == attendance.Date);

                if (existing != null)
                {
                    _logger.LogWarning("Timesheet already exists for UserId {UserId} on Date {Date}", userId, attendance.Date);
                    return new ApiResponse<TimesheetResponse>
                    {
                        Success = false,
                        Message = "Timesheet already exists for today"
                    };
                }

                var assignments = await _projectService.GetUserProjectAssignmentsAsync(userId);
                var projectAssignment = assignments.Data.FirstOrDefault();

                if (projectAssignment == null)
                {
                    _logger.LogWarning("No project assigned for UserId {UserId}, cannot create timesheet", userId);
                    return new ApiResponse<TimesheetResponse>
                    {
                        Success = false,
                        Message = "No project assigned"
                    };
                }

                var project = await _projectRepository.GetByIdAsync(projectAssignment.ProjectId);
                var user = await _userRepository.GetByIdAsync(userId);

                var totalHours = Math.Max(0, (attendance.CheckOut.Value - attendance.CheckIn.Value).TotalHours);

                var timesheet = new Timesheet
                {
                    UserId = userId,
                    ProjectId = project!.Id,
                    ProjectName = project.ProjectName,
                    WorkDate = attendance.Date,
                    StartTime = attendance.CheckIn.Value,
                    EndTime = attendance.CheckOut.Value,
                    BreakTime = TimeSpan.FromMinutes(30),
                    TaskDescription = "Auto generated from attendance",
                    TotalHours = totalHours,
                    Status = TimesheetStatus.Pending
                };

                await _timesheetRepository.AddAsync(timesheet);
                _logger.LogInformation("Timesheet created successfully for UserId {UserId} on Date {Date}", userId, attendance.Date);

                return new ApiResponse<TimesheetResponse>
                {
                    Success = true,
                    Message = "Timesheet auto-created",
                    Data = MapToDto(timesheet, user!, project)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating timesheet automatically for UserId {UserId}", userId);
                return new ApiResponse<TimesheetResponse>
                {
                    Success = false,
                    Message = "Error generating timesheet"
                };
            }
        }



        // ---------------- UPDATE TIMESHEET ----------------
        public async Task<ApiResponse<TimesheetResponse>> UpdateTimesheetAsync(int timesheetId, TimesheetUpdateRequest request)
        {
            try
            {
                _logger.LogInformation("Updating timesheet with ID {TimesheetId}", timesheetId);

                var timesheet = await _timesheetRepository.GetByIdAsync(timesheetId);
                if (timesheet == null)
                {
                    _logger.LogWarning("Timesheet with ID {TimesheetId} not found", timesheetId);
                    return new ApiResponse<TimesheetResponse> { Success = false, Message = "Timesheet not found" };
                }

                if (timesheet.Status == TimesheetStatus.Approved)
                {
                    _logger.LogWarning("Attempt to update approved timesheet ID {TimesheetId}", timesheetId);
                    return new ApiResponse<TimesheetResponse> { Success = false, Message = "Approved timesheets cannot be updated" };
                }

                if (request.WorkDate.HasValue) timesheet.WorkDate = request.WorkDate.Value;
                if (request.StartTime.HasValue) timesheet.StartTime = request.StartTime.Value;
                if (request.EndTime.HasValue) timesheet.EndTime = request.EndTime.Value;
                if (request.BreakTime.HasValue) timesheet.BreakTime = request.BreakTime.Value;
                if (request.TaskDescription != null) timesheet.TaskDescription = request.TaskDescription;

                timesheet.TotalHours = Math.Max(0, (timesheet.EndTime - timesheet.StartTime - timesheet.BreakTime).TotalHours);

                await _timesheetRepository.UpdateAsync(timesheetId, timesheet);

                var user = await _userRepository.GetByIdAsync(timesheet.UserId);
                var project = await _projectRepository.GetByIdAsync(timesheet.ProjectId);

                _logger.LogInformation("Timesheet with ID {TimesheetId} updated successfully", timesheetId);

                return new ApiResponse<TimesheetResponse>
                {
                    Success = true,
                    Message = "Timesheet updated successfully",
                    Data = MapToDto(timesheet, user!, project!)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating timesheet {TimesheetId}", timesheetId);
                return new ApiResponse<TimesheetResponse> { Success = false, Message = "Error updating timesheet" };
            }
        }

        // ---------------- APPROVE / REJECT ----------------
        public async Task<ApiResponse<bool>> ApproveOrRejectTimesheetAsync(TimesheetApprovalRequest request)
        {
            try
            {
                _logger.LogInformation("Approving/Rejecting timesheet ID {TimesheetId} by ManagerId {ManagerId}", request.TimesheetId, request.ApprovedById);

                var timesheet = await _timesheetRepository.GetByIdAsync(request.TimesheetId);
                if (timesheet == null)
                {
                    _logger.LogWarning("Timesheet with ID {TimesheetId} not found", request.TimesheetId);
                    return new ApiResponse<bool> { Success = false, Message = "Timesheet not found", Data = false };
                }

                timesheet.Status = request.IsApproved ? TimesheetStatus.Approved : TimesheetStatus.Rejected;
                timesheet.ApprovedById = request.ApprovedById;
                timesheet.ManagerComment = request.ManagerComment;

                await _timesheetRepository.UpdateAsync(timesheet.Id, timesheet);

                // Notify the employee who submitted the timesheet
                var status = request.IsApproved ? "approved" : "rejected";
                await _notifService.SendToUserAsync(timesheet.UserId, "Timesheet",
                    $"Your timesheet has been {status}.{(string.IsNullOrEmpty(request.ManagerComment) ? "" : $" Comment: {request.ManagerComment}")}");

                _logger.LogInformation("Timesheet ID {TimesheetId} status updated to {Status}", timesheet.Id, timesheet.Status);
                return new ApiResponse<bool> { Success = true, Message = "Timesheet status updated", Data = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving/rejecting timesheet {TimesheetId}", request.TimesheetId);
                return new ApiResponse<bool> { Success = false, Message = "Error updating status", Data = false };
            }
        }

        // ---------------- GET ALL TIMESHEETS ----------------
        public async Task<ApiResponse<PagedResponse<TimesheetResponse>>> GetAllTimesheetsAsync(PaginationParams p)
        {
            try
            {
                var query = (await _timesheetRepository.GetAllAsync() ?? Enumerable.Empty<Timesheet>()).AsQueryable();

                // Filter
                if (!string.IsNullOrWhiteSpace(p.Search))
                {
                    var q = p.Search.ToLower();
                    var matchingUserIds = (await _userRepository.GetAllAsync() ?? Enumerable.Empty<User>())
                        .Where(u => u.Name.ToLower().Contains(q) || u.EmployeeId.ToLower().Contains(q))
                        .Select(u => u.Id).ToHashSet();
                    query = query.Where(t => matchingUserIds.Contains(t.UserId) || t.ProjectName.ToLower().Contains(q));
                }
                if (!string.IsNullOrWhiteSpace(p.Status) && Enum.TryParse<TimesheetStatus>(p.Status, true, out var st))
                    query = query.Where(t => t.Status == st);

                // Sort
                query = (p.SortBy?.ToLower(), p.SortDir?.ToLower()) switch
                {
                    ("hours", "asc")  => query.OrderBy(t => t.TotalHours),
                    ("hours", _)      => query.OrderByDescending(t => t.TotalHours),
                    ("employee", "asc") => query.OrderBy(t => t.UserId),
                    ("employee", _)   => query.OrderByDescending(t => t.UserId),
                    (_, "asc")        => query.OrderBy(t => t.WorkDate),
                    _                 => query.OrderByDescending(t => t.WorkDate)
                };

                var total = query.Count();
                var data  = query.Skip((p.PageNumber - 1) * p.PageSize).Take(p.PageSize).ToList();

                var responses = new List<TimesheetResponse>();
                foreach (var t in data)
                {
                    var user    = await _userRepository.GetByIdAsync(t.UserId);
                    var project = await _projectRepository.GetByIdAsync(t.ProjectId);
                    if (user != null && project != null) responses.Add(MapToDto(t, user, project));
                }

                return new ApiResponse<PagedResponse<TimesheetResponse>>
                {
                    Success = true,
                    Data = new PagedResponse<TimesheetResponse>(responses, total, p.PageNumber, p.PageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all timesheets");
                return new ApiResponse<PagedResponse<TimesheetResponse>> { Success = false, Message = "Error fetching timesheets" };
            }
        }

        // ---------------- GET USER TIMESHEETS ----------------
        public async Task<ApiResponse<PagedResponse<TimesheetResponse>>> GetUserTimesheetsAsync(int userId, PaginationParams p)
        {
            try
            {
                var query = (await _timesheetRepository.GetAllAsync() ?? Enumerable.Empty<Timesheet>())
                    .Where(t => t.UserId == userId).AsQueryable();

                if (!string.IsNullOrWhiteSpace(p.Search))
                {
                    var q = p.Search.ToLower();
                    query = query.Where(t => t.ProjectName.ToLower().Contains(q));
                }
                if (!string.IsNullOrWhiteSpace(p.Status) && Enum.TryParse<TimesheetStatus>(p.Status, true, out var st))
                    query = query.Where(t => t.Status == st);

                query = (p.SortBy?.ToLower(), p.SortDir?.ToLower()) switch
                {
                    ("hours", "asc")  => query.OrderBy(t => t.TotalHours),
                    ("hours", _)      => query.OrderByDescending(t => t.TotalHours),
                    (_, "asc")        => query.OrderBy(t => t.WorkDate),
                    _                 => query.OrderByDescending(t => t.WorkDate)
                };

                var total = query.Count();
                var data  = query.Skip((p.PageNumber - 1) * p.PageSize).Take(p.PageSize).ToList();

                var user = await _userRepository.GetByIdAsync(userId);
                var responses = new List<TimesheetResponse>();
                foreach (var t in data)
                {
                    var project = await _projectRepository.GetByIdAsync(t.ProjectId);
                    if (project != null) responses.Add(MapToDto(t, user!, project));
                }

                return new ApiResponse<PagedResponse<TimesheetResponse>>
                {
                    Success = true,
                    Data = new PagedResponse<TimesheetResponse>(responses, total, p.PageNumber, p.PageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching timesheets for UserId {UserId}", userId);
                return new ApiResponse<PagedResponse<TimesheetResponse>> { Success = false, Message = "Error fetching timesheets" };
            }
        }

        // ---------------- DELETE TIMESHEET ----------------
        public async Task<ApiResponse<bool>> DeleteTimesheetAsync(int timesheetId)
        {
            try
            {
                _logger.LogInformation("Deleting timesheet with ID {TimesheetId}", timesheetId);

                var timesheet = await _timesheetRepository.GetByIdAsync(timesheetId);
                if (timesheet == null)
                {
                    _logger.LogWarning("Timesheet with ID {TimesheetId} not found", timesheetId);
                    return new ApiResponse<bool> { Success = false, Message = "Timesheet not found", Data = false };
                }

                if (timesheet.Status == TimesheetStatus.Approved)
                {
                    _logger.LogWarning("Attempt to delete approved timesheet ID {TimesheetId}", timesheetId);
                    return new ApiResponse<bool> { Success = false, Message = "Approved timesheets cannot be deleted", Data = false };
                }

                await _timesheetRepository.DeleteAsync(timesheetId);
                _logger.LogInformation("Timesheet ID {TimesheetId} deleted successfully", timesheetId);

                return new ApiResponse<bool>
                {
                    Success = true,
                    Message = "Timesheet deleted successfully",
                    Data = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting timesheet {TimesheetId}", timesheetId);
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = "An error occurred while deleting the timesheet.",
                    Data = false
                };
            }
        }

        // ---------------- HELPER: MAP DTO ----------------
        private TimesheetResponse MapToDto(Timesheet t, User user, Project project)
        {
            return new TimesheetResponse
            {
                Id = t.Id,
                EmployeeName = user.Name,
                EmployeeId = user.EmployeeId,
                ProjectName = project.ProjectName,
                Date = t.WorkDate,
                StartTime = t.StartTime,
                EndTime = t.EndTime,
                BreakTime = t.BreakTime,
                HoursWorked = TimeSpan.FromHours(t.TotalHours).ToString(@"hh\:mm"),
                Description = t.TaskDescription,
                Status = t.Status,
                ManagerComment = t.ManagerComment
            };
        }
    }
}