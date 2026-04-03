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

                // ── Pre-validate: check daily 18-hour cap across the whole batch ──
                var batchByDate = request.Entries
                    .Where(e => e.Hours > 0 && DateTime.TryParse(e.WorkDate, out _))
                    .GroupBy(e => DateTime.Parse(e.WorkDate).Date);

                foreach (var dayGroup in batchByDate)
                {
                    var batchTotal = dayGroup.Sum(e => e.Hours);

                    // Hours from existing entries that will be REPLACED by this batch
                    // (same user + same day + same project name — these get updated, not added)
                    var replacedHours = 0.0;
                    foreach (var entry in dayGroup)
                    {
                        var existing = allTs.FirstOrDefault(t =>
                            t.UserId == userId &&
                            t.WorkDate.Date == dayGroup.Key &&
                            string.Equals(t.ProjectName, entry.ProjectName, StringComparison.OrdinalIgnoreCase) &&
                            t.Status != TimesheetStatus.Rejected);
                        if (existing != null) replacedHours += existing.TotalHours;
                    }

                    // Net new hours = batch total minus what's being replaced
                    var existingOtherHours = allTs
                        .Where(t => t.UserId == userId && t.WorkDate.Date == dayGroup.Key && t.Status != TimesheetStatus.Rejected)
                        .Sum(t => t.TotalHours) - replacedHours;

                    if (batchTotal + existingOtherHours > 12)
                    {
                        return new ApiResponse<TimesheetWeeklyResponse>
                        {
                            Success = false,
                            Message = $"Daily limit exceeded for {dayGroup.Key:dd MMM}: " +
                                      $"total would be {batchTotal + existingOtherHours:F1}h (max 18h per day)."
                        };
                    }
                }

                foreach (var entry in request.Entries)
                {
                    if (entry.Hours <= 0) { result.Skipped++; continue; }

                    if (!DateTime.TryParse(entry.WorkDate, out var workDate))
                    { result.Errors.Add($"Invalid date: {entry.WorkDate}"); result.Skipped++; continue; }

                    // Resolve project
                    Project? project = null;
                    if (entry.ProjectId > 0)
                    {
                        try { project = await _projectRepository.GetByIdAsync(entry.ProjectId); } catch { }
                    }
                    if (project == null)
                        project = allProjects.FirstOrDefault(p =>
                            string.Equals(p.ProjectName, entry.ProjectName, StringComparison.OrdinalIgnoreCase));
                    if (project == null && !string.IsNullOrWhiteSpace(entry.ProjectName))
                    {
                        // For interns without project assignment: use or create a single shared "Intern Tasks" project
                        const string internProjectName = "Intern Tasks";
                        project = allProjects.FirstOrDefault(p =>
                            string.Equals(p.ProjectName, internProjectName, StringComparison.OrdinalIgnoreCase));
                        if (project == null)
                        {
                            project = new Project
                            {
                                ProjectName = internProjectName,
                                Description = "Shared project for intern task timesheets",
                                StartDate   = DateTime.Today,
                                Status      = ProjectStatus.Active
                            };
                            await _projectRepository.AddAsync(project);
                            allProjects.Add(project);
                        }
                    }
                    if (project == null)
                    { result.Errors.Add($"Project not found: {entry.ProjectName}"); result.Skipped++; continue; }

                    var startTime = new TimeSpan(9, 0, 0);
                    var totalMinutes = (int)Math.Round(entry.Hours * 60);
                    var endTime = startTime.Add(TimeSpan.FromMinutes(totalMinutes));
                    if (endTime.TotalHours > 23) endTime = new TimeSpan(23, 59, 0);

                    var existing = allTs.FirstOrDefault(t =>
                        t.UserId == userId && t.ProjectId == project.Id && t.WorkDate.Date == workDate.Date);

                    // ── Daily 18-hour cap: sum existing hours for this day across all projects ──
                    var existingDayHours = allTs
                        .Where(t => t.UserId == userId && t.WorkDate.Date == workDate.Date && t.Status != TimesheetStatus.Rejected)
                        .Sum(t => t.TotalHours);
                    // Subtract existing entry for this project (it will be replaced)
                    if (existing != null) existingDayHours -= existing.TotalHours;
                    if (existingDayHours + entry.Hours > 12)
                    {
                        result.Errors.Add($"Daily limit exceeded for {workDate:dd MMM}: total would be {existingDayHours + entry.Hours:F1}h (max 12h).");
                        result.Skipped++;
                        continue;
                    }

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


                // ✅ Check project — for interns without assignment, use/create shared "Intern Tasks" project
                var project = await _projectRepository.GetByIdAsync(request.ProjectId);
                if (project == null && !string.IsNullOrWhiteSpace(request.ProjectName))
                {
                    var allProjects = (await _projectRepository.GetAllAsync() ?? Enumerable.Empty<Project>()).ToList();
                    project = allProjects.FirstOrDefault(p =>
                        string.Equals(p.ProjectName, request.ProjectName, StringComparison.OrdinalIgnoreCase));
                    if (project == null)
                    {
                        const string internProjectName = "Intern Tasks";
                        project = allProjects.FirstOrDefault(p =>
                            string.Equals(p.ProjectName, internProjectName, StringComparison.OrdinalIgnoreCase));
                        if (project == null)
                        {
                            project = new Project
                            {
                                ProjectName = internProjectName,
                                Description = "Shared project for intern task timesheets",
                                StartDate   = DateTime.Today,
                                Status      = ProjectStatus.Active
                            };
                            await _projectRepository.AddAsync(project);
                        }
                    }
                }
                if (project == null)
                    return new ApiResponse<TimesheetResponse> { Success = false, Message = "Invalid project" };

                // Check project assignment — skip for interns using the shared Intern Tasks project
                var assignments = await _projectService.GetUserProjectAssignmentsAsync(userId);
                var isAssigned = assignments.Data?.Any(a => a.ProjectId == project.Id) ?? false;
                if (!isAssigned && project.ProjectName != "Intern Tasks")
                    return new ApiResponse<TimesheetResponse> { Success = false, Message = "User is not assigned to this project" };

                // ❌ Prevent duplicate
                var allExisting = (await _timesheetRepository.GetAllAsync())?.ToList() ?? new List<Timesheet>();
                var exists = allExisting.Any(t => t.UserId == userId && t.WorkDate.Date == request.WorkDate.Date);

                if (exists)
                {
                    return new ApiResponse<TimesheetResponse>
                    {
                        Success = false,
                        Message = "Timesheet already exists for this date"
                    };
                }

                // ❌ Daily 18-hour cap
                var dayHours = allExisting
                    .Where(t => t.UserId == userId && t.WorkDate.Date == request.WorkDate.Date && t.Status != TimesheetStatus.Rejected)
                    .Sum(t => t.TotalHours);

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

                // ❌ Daily 18-hour cap check
                if (dayHours + totalHours > 12)
                {
                    return new ApiResponse<TimesheetResponse>
                    {
                        Success = false,
                        Message = $"Daily limit exceeded: adding {totalHours:F1}h would bring total to {dayHours + totalHours:F1}h (max 12h per day)."
                    };
                }

                // ✅ Create timesheet
                var timesheet = new Timesheet
                {
                    UserId = userId,
                    ProjectId = project.Id,
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

                // Prevent self-approval: a manager cannot approve their own timesheet
                if (timesheet.UserId == request.ApprovedById)
                {
                    _logger.LogWarning("Self-approval attempt: UserId {UserId} tried to approve their own timesheet {TimesheetId}", request.ApprovedById, request.TimesheetId);
                    return new ApiResponse<bool> { Success = false, Message = "You cannot approve your own timesheet.", Data = false };
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
                Id           = t.Id,
                UserId       = t.UserId,
                EmployeeName = user.Name,
                EmployeeId   = user.EmployeeId,
                ProjectName  = project.ProjectName,
                Date         = t.WorkDate,
                StartTime    = t.StartTime,
                EndTime      = t.EndTime,
                BreakTime    = t.BreakTime,
                HoursWorked  = TimeSpan.FromHours(t.TotalHours).ToString(@"hh\:mm"),
                Description  = t.TaskDescription,
                Status       = t.Status,
                ManagerComment = t.ManagerComment
            };
        }
    }
}