using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.common;
using TimeSheetAppWeb.Model.DTOs;

namespace TimeSheetAppWeb.Services
{
    public class InternDetailsService : IInternDetailsService
    {
        private readonly IRepository<int, InternDetails> _internRepository;
        private readonly IRepository<int, User> _userRepository;
        private readonly ILogger<InternDetailsService> _logger;

        public InternDetailsService(
            IRepository<int, InternDetails> internRepository,
            IRepository<int, User> userRepository,
            ILogger<InternDetailsService> logger)
        {
            _internRepository = internRepository ?? throw new ArgumentNullException(nameof(internRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // ---------------- GET ALL ----------------
        public async Task<ApiResponse<PagedResponse<InternDetailsDto>>> GetAllAsync(int pageNumber = 1, int pageSize = 10, string? userName = null, string? mentorName = null)
        {
            try
            {
                var allInterns = await _internRepository.GetAllAsync() ?? Enumerable.Empty<InternDetails>();

                var query = allInterns.AsQueryable();

                if (!string.IsNullOrEmpty(userName))
                    query = query.Where(i => i.User != null && i.User.Name.Contains(userName, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(mentorName))
                    query = query.Where(i => i.Mentor != null && i.Mentor.Name.Contains(mentorName, StringComparison.OrdinalIgnoreCase));

                var totalRecords = query.Count();

                var pagedData = query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var result = new List<InternDetailsDto>();
                foreach (var i in pagedData)
                {
                    result.Add(await MapToDtoAsync(i));
                }

                return new ApiResponse<PagedResponse<InternDetailsDto>>
                {
                    Success = true,
                    Message = "Intern records fetched successfully",
                    Data = new PagedResponse<InternDetailsDto>(result, totalRecords, pageNumber, pageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching intern records (Page {PageNumber}, Size {PageSize})", pageNumber, pageSize);
                return new ApiResponse<PagedResponse<InternDetailsDto>>
                {
                    Success = false,
                    Message = "Error fetching intern records"
                };
            }
        }

        // ---------------- GET BY ID ----------------
        public async Task<ApiResponse<InternDetailsDto>> GetByIdAsync(int id)
        {
            try
            {
                var intern = await _internRepository.GetByIdAsync(id);
                if (intern == null)
                {
                    return new ApiResponse<InternDetailsDto>
                    {
                        Success = false,
                        Message = "Intern not found"
                    };
                }

                return new ApiResponse<InternDetailsDto>
                {
                    Success = true,
                    Message = "Intern fetched successfully",
                    Data = await MapToDtoAsync(intern)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching intern with ID {Id}", id);
                return new ApiResponse<InternDetailsDto>
                {
                    Success = false,
                    Message = "Error fetching intern"
                };
            }
        }

        // ---------------- CREATE ----------------
        public async Task<ApiResponse<InternDetailsDto>> CreateAsync(InternDetailsCreateDto dto)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(dto.UserId);
                if (user == null || user.Role != UserRole.Intern)
                {
                    return new ApiResponse<InternDetailsDto>
                    {
                        Success = false,
                        Message = "Invalid user or user is not an intern"
                    };
                }

                var intern = new InternDetails
                {
                    UserId = dto.UserId,
                    MentorId = dto.MentorId,
                    TrainingStart = dto.TrainingStart,
                    TrainingEnd = dto.TrainingEnd
                };

                var addedIntern = await _internRepository.AddAsync(intern);
                _logger.LogInformation("Intern created with ID {InternId} for user {UserId}", addedIntern.Id, dto.UserId);

                return new ApiResponse<InternDetailsDto>
                {
                    Success = true,
                    Message = "Intern created successfully",
                    Data = await MapToDtoAsync(addedIntern)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating intern for user {UserId}", dto.UserId);
                return new ApiResponse<InternDetailsDto>
                {
                    Success = false,
                    Message = "Error creating intern"
                };
            }
        }

        // ---------------- UPDATE ----------------
        public async Task<ApiResponse<InternDetailsDto>> UpdateAsync(int id, InternDetailsCreateDto dto)
        {
            try
            {
                var intern = await _internRepository.GetByIdAsync(id);
                if (intern == null)
                {
                    return new ApiResponse<InternDetailsDto>
                    {
                        Success = false,
                        Message = "Intern not found"
                    };
                }

                var user = await _userRepository.GetByIdAsync(dto.UserId);
                if (user == null || user.Role != UserRole.Intern)
                {
                    return new ApiResponse<InternDetailsDto>
                    {
                        Success = false,
                        Message = "Invalid user or user is not an intern"
                    };
                }

                intern.UserId = dto.UserId;
                intern.MentorId = dto.MentorId;
                intern.TrainingStart = dto.TrainingStart;
                intern.TrainingEnd = dto.TrainingEnd;

                await _internRepository.UpdateAsync(id, intern);
                _logger.LogInformation("Intern with ID {InternId} updated", id);

                return new ApiResponse<InternDetailsDto>
                {
                    Success = true,
                    Message = "Intern updated successfully",
                    Data = await MapToDtoAsync(intern)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating intern with ID {InternId}", id);
                return new ApiResponse<InternDetailsDto>
                {
                    Success = false,
                    Message = "Error updating intern"
                };
            }
        }

        // ---------------- DELETE ----------------
        public async Task<ApiResponse<bool>> DeleteAsync(int id)
        {
            try
            {
                var intern = await _internRepository.GetByIdAsync(id);
                if (intern == null)
                {
                    return new ApiResponse<bool>
                    {
                        Success = false,
                        Message = "Intern not found",
                        Data = false
                    };
                }

                await _internRepository.DeleteAsync(id);
                _logger.LogInformation("Intern with ID {InternId} deleted", id);

                return new ApiResponse<bool>
                {
                    Success = true,
                    Message = "Intern deleted successfully",
                    Data = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting intern with ID {InternId}", id);
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = "Error deleting intern",
                    Data = false
                };
            }
        }

        // ---------------- HELPER: MAP TO DTO ----------------
        private async Task<InternDetailsDto> MapToDtoAsync(InternDetails i)
        {
            string userName = "Unknown";
            string mentorName = null;

            if (i.User != null)
                userName = i.User.Name;
            else
            {
                var user = await _userRepository.GetByIdAsync(i.UserId);
                if (user != null) userName = user.Name;
            }

            if (i.Mentor != null)
                mentorName = i.Mentor.Name;
            else if (i.MentorId.HasValue)
            {
                var mentor = await _userRepository.GetByIdAsync(i.MentorId.Value);
                if (mentor != null) mentorName = mentor.Name;
            }

            return new InternDetailsDto
            {
                Id = i.Id,
                UserId = i.UserId,
                UserName = userName,
                MentorId = i.MentorId,
                MentorName = mentorName,
                TrainingStart = i.TrainingStart,
                TrainingEnd = i.TrainingEnd
            };
        }
    }
}