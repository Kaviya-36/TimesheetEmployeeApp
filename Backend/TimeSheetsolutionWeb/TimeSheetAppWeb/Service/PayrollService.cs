using Microsoft.Extensions.Logging;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.DTOs;

namespace TimeSheetAppWeb.Services
{
    public class PayrollService : IPayrollService
    {
        private readonly IRepository<int, Payroll> _payrollRepository;
        private readonly IRepository<int, User> _userRepository;
        private readonly IRepository<int, Timesheet> _timesheetRepository;
        private readonly ILogger<PayrollService> _logger;

        public PayrollService(
            IRepository<int, Payroll> payrollRepository,
            IRepository<int, User> userRepository,
            IRepository<int, Timesheet> timesheetRepository,
            ILogger<PayrollService> logger)
        {
            _payrollRepository = payrollRepository;
            _userRepository = userRepository;
            _timesheetRepository = timesheetRepository;
            _logger = logger;
        }

        // ---------------- CREATE PAYROLL ----------------
        public async Task<ApiResponse<PayrollResponse>> CreatePayrollAsync(PayrollCreateRequest request)
        {
            try
            {
                _logger.LogInformation("Creating payroll for UserId={UserId}, SalaryMonth={SalaryMonth}", request.UserId, request.SalaryMonth);

                var user = await _userRepository.GetByIdAsync(request.UserId);
                if (user == null)
                {
                    _logger.LogWarning("User with ID {UserId} not found", request.UserId);
                    return new ApiResponse<PayrollResponse> { Success = false, Message = "User not found" };
                }

                // ── Only allow current month ───────────────────────────────────────
                var now = DateTime.Now;
                if (request.SalaryMonth.Year != now.Year || request.SalaryMonth.Month != now.Month)
                    return new ApiResponse<PayrollResponse>
                    {
                        Success = false,
                        Message = $"Payroll can only be generated for the current month ({now:MMMM yyyy})."
                    };

                // ── Prevent duplicate payroll for same user + month ────────────────
                var existing = await _payrollRepository.GetAllAsync() ?? Enumerable.Empty<Payroll>();
                var duplicate = existing.Any(p =>
                    p.UserId == request.UserId &&
                    p.SalaryMonth.Year  == request.SalaryMonth.Year &&
                    p.SalaryMonth.Month == request.SalaryMonth.Month);

                if (duplicate)
                    return new ApiResponse<PayrollResponse>
                    {
                        Success = false,
                        Message = $"Payroll for {user.Name} has already been generated for {request.SalaryMonth:MMMM yyyy}."
                    };

                // ── Calculate daily rate and weekend bonus ─────────────────────────
                // Count working weekdays in the salary month
                var salaryYear  = request.SalaryMonth.Year;
                var salaryMonthNum = request.SalaryMonth.Month;
                var daysInMonth = DateTime.DaysInMonth(salaryYear, salaryMonthNum);
                int weekdayCount = Enumerable.Range(1, daysInMonth)
                    .Count(d => { var day = new DateTime(salaryYear, salaryMonthNum, d).DayOfWeek;
                                  return day != DayOfWeek.Saturday && day != DayOfWeek.Sunday; });

                decimal dailyRate = weekdayCount > 0 ? Math.Round(request.BasicSalary / weekdayCount, 2) : 0;

                // Find approved timesheets for this user in the salary month that fall on weekends
                var allTimesheets = await _timesheetRepository.GetAllAsync() ?? Enumerable.Empty<Timesheet>();
                var weekendDaysWorked = allTimesheets
                    .Where(t => t.UserId == request.UserId
                             && t.Status == TimesheetStatus.Approved
                             && t.WorkDate.Year == salaryYear
                             && t.WorkDate.Month == salaryMonthNum
                             && (t.WorkDate.DayOfWeek == DayOfWeek.Saturday || t.WorkDate.DayOfWeek == DayOfWeek.Sunday))
                    .Select(t => t.WorkDate.Date)
                    .Distinct()
                    .Count();

                // Weekend bonus = 1x daily rate extra per weekend day (total pay = 2x normal)
                decimal weekendBonus = weekendDaysWorked * dailyRate;

                _logger.LogInformation(
                    "Payroll calc for UserId={UserId}: DailyRate={DailyRate}, WeekendDaysWorked={WeekendDays}, WeekendBonus={WeekendBonus}",
                    request.UserId, dailyRate, weekendDaysWorked, weekendBonus);

                decimal netSalary = request.BasicSalary + request.OvertimeAmount + weekendBonus - request.Deductions;

                var payroll = new Payroll
                {
                    UserId = request.UserId,
                    BasicSalary = request.BasicSalary,
                    OvertimeAmount = request.OvertimeAmount,
                    Deductions = request.Deductions,
                    DailyRate = dailyRate,
                    WeekendBonus = weekendBonus,
                    NetSalary = netSalary,
                    SalaryMonth = request.SalaryMonth,
                    GeneratedDate = DateTime.Now
                };

                var addedPayroll = await _payrollRepository.AddAsync(payroll);
                _logger.LogInformation("Payroll created successfully with ID {PayrollId} for UserId={UserId}", addedPayroll!.Id, request.UserId);

                return new ApiResponse<PayrollResponse>
                {
                    Success = true,
                    Message = "Payroll created successfully",
                    Data = MapToDto(addedPayroll, user)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating payroll for UserId={UserId}", request.UserId);
                return new ApiResponse<PayrollResponse>
                {
                    Success = false,
                    Message = $"An error occurred while creating payroll: {ex.Message}"
                };
            }
        }

        // ---------------- GET PAYROLL BY ID ----------------
        public async Task<ApiResponse<PayrollResponse>> GetPayrollByIdAsync(int payrollId)
        {
            try
            {
                _logger.LogInformation("Fetching payroll by ID {PayrollId}", payrollId);

                var payroll = await _payrollRepository.GetByIdAsync(payrollId);
                if (payroll == null)
                {
                    _logger.LogWarning("Payroll with ID {PayrollId} not found", payrollId);
                    return new ApiResponse<PayrollResponse> { Success = false, Message = "Payroll not found" };
                }

                var user = await _userRepository.GetByIdAsync(payroll.UserId);
                if (user == null)
                {
                    _logger.LogWarning("User with ID {UserId} not found for payroll ID {PayrollId}", payroll.UserId, payrollId);
                    return new ApiResponse<PayrollResponse> { Success = false, Message = "User not found for this payroll" };
                }

                _logger.LogInformation("Payroll ID {PayrollId} fetched successfully", payrollId);

                return new ApiResponse<PayrollResponse> { Success = true, Data = MapToDto(payroll, user) };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching payroll ID {PayrollId}", payrollId);
                return new ApiResponse<PayrollResponse>
                {
                    Success = false,
                    Message = $"An error occurred while fetching payroll: {ex.Message}"
                };
            }
        }

        // ---------------- GET USER PAYROLLS ----------------
        public async Task<ApiResponse<IEnumerable<PayrollResponse>>> GetUserPayrollsAsync(
            int userId,
            int pageNumber = 1,
            int pageSize = 10,
            DateTime? fromMonth = null,
            DateTime? toMonth = null,
            decimal? minSalary = null,
            decimal? maxSalary = null)
        {
            try
            {
                _logger.LogInformation("Fetching payrolls for UserId={UserId} with filters: FromMonth={FromMonth}, ToMonth={ToMonth}, MinSalary={MinSalary}, MaxSalary={MaxSalary}, Page={Page}, PageSize={PageSize}",
                    userId, fromMonth, toMonth, minSalary, maxSalary, pageNumber, pageSize);

                var payrolls = await _payrollRepository.GetAllAsync() ?? Enumerable.Empty<Payroll>();
                var userPayrolls = payrolls.Where(p => p.UserId == userId);

                if (fromMonth.HasValue) userPayrolls = userPayrolls.Where(p => p.SalaryMonth >= fromMonth.Value);
                if (toMonth.HasValue) userPayrolls = userPayrolls.Where(p => p.SalaryMonth <= toMonth.Value);
                if (minSalary.HasValue) userPayrolls = userPayrolls.Where(p => p.NetSalary >= minSalary.Value);
                if (maxSalary.HasValue) userPayrolls = userPayrolls.Where(p => p.NetSalary <= maxSalary.Value);

                userPayrolls = userPayrolls.OrderByDescending(p => p.SalaryMonth);

                if (!userPayrolls.Any())
                {
                    _logger.LogWarning("No payroll records found for UserId={UserId} with specified filters", userId);
                    return new ApiResponse<IEnumerable<PayrollResponse>> { Success = false, Message = "No payroll records found for this user" };
                }

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User with ID {UserId} not found", userId);
                    return new ApiResponse<IEnumerable<PayrollResponse>> { Success = false, Message = "User not found" };
                }

                var pagedPayrolls = userPayrolls.Skip((pageNumber - 1) * pageSize).Take(pageSize);
                var response = pagedPayrolls.Select(p => MapToDto(p, user));

                _logger.LogInformation("Fetched {Count} payroll records for UserId={UserId}", response.Count(), userId);

                return new ApiResponse<IEnumerable<PayrollResponse>> { Success = true, Data = response };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching payrolls for UserId={UserId}", userId);
                return new ApiResponse<IEnumerable<PayrollResponse>>
                {
                    Success = false,
                    Message = $"An error occurred while fetching user payrolls: {ex.Message}"
                };
            }
        }

        // ---------------- GET ALL PAYROLLS ----------------
        public async Task<ApiResponse<IEnumerable<PayrollResponse>>> GetAllPayrollsAsync(
            int pageNumber = 1,
            int pageSize = 10,
            int? userId = null,
            DateTime? fromMonth = null,
            DateTime? toMonth = null,
            decimal? minSalary = null,
            decimal? maxSalary = null)
        {
            try
            {
                _logger.LogInformation("Fetching all payrolls with filters: UserId={UserId}, FromMonth={FromMonth}, ToMonth={ToMonth}, MinSalary={MinSalary}, MaxSalary={MaxSalary}, Page={Page}, PageSize={PageSize}",
                    userId, fromMonth, toMonth, minSalary, maxSalary, pageNumber, pageSize);

                var payrolls = await _payrollRepository.GetAllAsync() ?? Enumerable.Empty<Payroll>();

                if (userId.HasValue) payrolls = payrolls.Where(p => p.UserId == userId.Value);
                if (fromMonth.HasValue) payrolls = payrolls.Where(p => p.SalaryMonth >= fromMonth.Value);
                if (toMonth.HasValue) payrolls = payrolls.Where(p => p.SalaryMonth <= toMonth.Value);
                if (minSalary.HasValue) payrolls = payrolls.Where(p => p.NetSalary >= minSalary.Value);
                if (maxSalary.HasValue) payrolls = payrolls.Where(p => p.NetSalary <= maxSalary.Value);

                payrolls = payrolls.OrderByDescending(p => p.SalaryMonth);

                if (!payrolls.Any())
                {
                    _logger.LogWarning("No payroll records found with the specified filters");
                    return new ApiResponse<IEnumerable<PayrollResponse>> { Success = false, Message = "No payroll records found" };
                }

                var pagedPayrolls = payrolls.Skip((pageNumber - 1) * pageSize).Take(pageSize);

                var result = new List<PayrollResponse>();
                foreach (var payroll in pagedPayrolls)
                {
                    var user = await _userRepository.GetByIdAsync(payroll.UserId);
                    if (user != null)
                        result.Add(MapToDto(payroll, user));
                }

                _logger.LogInformation("Fetched {Count} payroll records", result.Count);

                return new ApiResponse<IEnumerable<PayrollResponse>> { Success = true, Data = result };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all payrolls");
                return new ApiResponse<IEnumerable<PayrollResponse>>
                {
                    Success = false,
                    Message = $"An error occurred while fetching all payrolls: {ex.Message}"
                };
            }
        }

        // ---------------- HELPER: MAP TO DTO ----------------
        private PayrollResponse MapToDto(Payroll payroll, User user)
        {
            return new PayrollResponse
            {
                PayrollId = payroll.Id,
                EmployeeName = user.Name,
                EmployeeId = user.EmployeeId,
                BasicSalary = payroll.BasicSalary,
                OvertimeAmount = payroll.OvertimeAmount,
                Deductions = payroll.Deductions,
                DailyRate = payroll.DailyRate,
                WeekendBonus = payroll.WeekendBonus,
                NetSalary = payroll.NetSalary,
                SalaryMonth = payroll.SalaryMonth,
                GeneratedDate = payroll.GeneratedDate
            };
        }
    }
}