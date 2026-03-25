public class AnalyticsRequest
{
    public int? ProjectId { get; set; }
    public int? UserId { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public string? SortBy { get; set; } // "hours", "days"

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}