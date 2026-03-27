namespace TimeSheetAppWeb.Model.common
{
    public class PaginationParams
    {
        private const int MaxPageSize = 100;

        public int PageNumber { get; set; } = 1;

        private int _pageSize = 10;
        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = value > MaxPageSize ? MaxPageSize : value;
        }

        public string? Search   { get; set; }
        public string? SortBy   { get; set; } = "date";
        public string? SortDir  { get; set; } = "desc";
        public string? Status   { get; set; }   // pending | approved | rejected
        public string? Role     { get; set; }   // for users
        public string? Action   { get; set; }   // for audit logs: INSERT | UPDATE | DELETE
        public string? Table    { get; set; }   // for audit logs
    }
}
