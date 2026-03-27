namespace TimeSheetAppWeb.Interface
{
    public interface INotificationService
    {
        Task SendToUserAsync(int userId, string type, string message);
        Task SendToRoleAsync(string role, string type, string message);
    }
}
