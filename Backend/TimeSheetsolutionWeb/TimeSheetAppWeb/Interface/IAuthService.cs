using TimeSheetAppWeb.Model;

namespace TimeSheetAppWeb.Interface
{
    public interface IAuthService
    {
        Task<string> LoginAsync(string email, string password);
        Task<User> RegisterAsync(User user, string password);
    }
}