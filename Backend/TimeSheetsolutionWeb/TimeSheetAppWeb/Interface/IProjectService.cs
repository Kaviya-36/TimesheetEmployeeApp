using TimeSheetAppWeb.Model.DTOs;

namespace TimeSheetAppWeb.Interface
{
    public interface IProjectService
    {
       
        Task<ApiResponse<ProjectResponse>> CreateProjectAsync(ProjectCreateRequest request);
        Task<ApiResponse<ProjectResponse>> UpdateProjectAsync(int projectId, ProjectUpdateRequest request);
        Task<ApiResponse<bool>> DeleteProjectAsync(int projectId);

        Task<ApiResponse<IEnumerable<ProjectResponse>>> GetAllProjectsAsync(
            int pageNumber = 1,
            int pageSize = 10,
            int? managerId = null,
            DateTime? startFrom = null,
            DateTime? startTo = null,
            DateTime? endFrom = null,
            DateTime? endTo = null);

        Task<ApiResponse<ProjectAssignmentResponse>> AssignUserToProjectAsync(ProjectAssignRequest request);
        Task<ApiResponse<bool>> RemoveUserFromProjectAsync(int assignmentId);

        Task<ApiResponse<IEnumerable<ProjectAssignmentResponse>>> GetProjectAssignmentsAsync(
            int projectId,
            int pageNumber = 1,
            int pageSize = 10);

        Task<ApiResponse<IEnumerable<ProjectAssignmentResponse>>> GetUserProjectAssignmentsAsync(
            int userId,
            int pageNumber = 1,
            int pageSize = 10);
    }
}