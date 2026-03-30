using Moq;
using TimeSheetAppWeb.Interface;

namespace TimeSheetAppWeb.Tests.Helpers
{
    public static class MockRepo
    {
        public static Mock<IRepository<int, T>> Create<T>() where T : class
            => new Mock<IRepository<int, T>>();
    }
}
