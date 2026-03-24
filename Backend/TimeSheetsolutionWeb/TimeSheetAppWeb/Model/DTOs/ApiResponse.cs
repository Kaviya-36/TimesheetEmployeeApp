namespace TimeSheetAppWeb.Model.DTOs
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; } = true;
        public string Message { get; set; } = "Request successful";
        public T? Data { get; set; }
        public IEnumerable<string>? Errors { get; set; }
    }
}