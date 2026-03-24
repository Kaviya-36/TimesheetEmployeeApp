namespace TimeSheetAppWeb.Exceptions
{
    public class UnAuthorizedException : Exception
    {
        public UnAuthorizedException() : base("Unauthorized access.")
        {
        }
        public UnAuthorizedException(string message) : base(message)
        {
        }
        public UnAuthorizedException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
