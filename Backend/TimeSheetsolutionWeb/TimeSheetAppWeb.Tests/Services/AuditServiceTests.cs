using Moq;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Services;
using Xunit;

namespace TimeSheetAppWeb.Tests.Services
{
    public class AuditServiceTests
    {
        private readonly Mock<IRepository<int, AuditLog>> _repo = new();

        private AuditService CreateService() => new(_repo.Object);

        private List<AuditLog> MakeLogs() => new()
        {
            new AuditLog { Id = 1, TableName = "Users",      Action = "Added",    UserId = 1, ChangedAt = DateTime.UtcNow.AddMinutes(-10), KeyValues = "id=1" },
            new AuditLog { Id = 2, TableName = "Timesheets", Action = "Modified", UserId = 2, ChangedAt = DateTime.UtcNow.AddMinutes(-5),  KeyValues = "id=2" },
            new AuditLog { Id = 3, TableName = "Users",      Action = "Deleted",  UserId = 1, ChangedAt = DateTime.UtcNow,                 KeyValues = "id=3" },
        };

        // ── GetAllAsync ────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAll_ReturnsOrderedByDateDesc()
        {
            _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(MakeLogs());
            var svc = CreateService();

            var result = (await svc.GetAllAsync()).ToList();

            Assert.Equal(3, result.Count);
            Assert.True(result[0].ChangedAt >= result[1].ChangedAt);
        }

        [Fact]
        public async Task GetAll_NullRepo_ReturnsEmpty()
        {
            _repo.Setup(r => r.GetAllAsync()).ReturnsAsync((IEnumerable<AuditLog>?)null);
            var svc = CreateService();

            var result = await svc.GetAllAsync();

            Assert.Empty(result);
        }

        // ── GetByIdAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task GetById_Found_ReturnsLog()
        {
            var log = MakeLogs()[0];
            _repo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(log);
            var svc = CreateService();

            var result = await svc.GetByIdAsync(1);

            Assert.NotNull(result);
            Assert.Equal(1, result!.Id);
        }

        [Fact]
        public async Task GetById_NotFound_ReturnsNull()
        {
            _repo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((AuditLog?)null);
            var svc = CreateService();

            var result = await svc.GetByIdAsync(99);

            Assert.Null(result);
        }

        // ── GetByTableAsync ────────────────────────────────────────────────────

        [Fact]
        public async Task GetByTable_ReturnsMatchingLogs()
        {
            _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(MakeLogs());
            var svc = CreateService();

            var result = (await svc.GetByTableAsync("users")).ToList();

            Assert.Equal(2, result.Count);
            Assert.All(result, l => Assert.Equal("Users", l.TableName));
        }

        [Fact]
        public async Task GetByTable_CaseInsensitive()
        {
            _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(MakeLogs());
            var svc = CreateService();

            var result = await svc.GetByTableAsync("USERS");

            Assert.Equal(2, result.Count());
        }

        [Fact]
        public async Task GetByTable_NoMatch_ReturnsEmpty()
        {
            _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(MakeLogs());
            var svc = CreateService();

            var result = await svc.GetByTableAsync("NonExistent");

            Assert.Empty(result);
        }

        // ── GetByActionAsync ───────────────────────────────────────────────────

        [Fact]
        public async Task GetByAction_ReturnsMatchingLogs()
        {
            _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(MakeLogs());
            var svc = CreateService();

            var result = (await svc.GetByActionAsync("Added")).ToList();

            Assert.Single(result);
            Assert.Equal("Added", result[0].Action);
        }

        [Fact]
        public async Task GetByAction_CaseInsensitive()
        {
            _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(MakeLogs());
            var svc = CreateService();

            var result = await svc.GetByActionAsync("MODIFIED");

            Assert.Single(result);
        }

        // ── GetByUserAsync ─────────────────────────────────────────────────────

        [Fact]
        public async Task GetByUser_ReturnsOnlyThatUser()
        {
            _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(MakeLogs());
            var svc = CreateService();

            var result = (await svc.GetByUserAsync(1)).ToList();

            Assert.Equal(2, result.Count);
            Assert.All(result, l => Assert.Equal(1, l.UserId));
        }

        [Fact]
        public async Task GetByUser_NoLogs_ReturnsEmpty()
        {
            _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(MakeLogs());
            var svc = CreateService();

            var result = await svc.GetByUserAsync(99);

            Assert.Empty(result);
        }

        // ── GetPagedAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task GetPaged_ReturnsCorrectPage()
        {
            _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(MakeLogs());
            var svc = CreateService();

            var result = await svc.GetPagedAsync(1, 2);

            Assert.Equal(3, result.TotalRecords);
            Assert.Equal(2, result.Data.Count());
        }

        [Fact]
        public async Task GetPaged_SearchFilter_ReturnsMatching()
        {
            _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(MakeLogs());
            var svc = CreateService();

            var result = await svc.GetPagedAsync(1, 10, search: "users");

            Assert.Equal(2, result.TotalRecords);
        }

        [Fact]
        public async Task GetPaged_ActionFilter_ReturnsMatching()
        {
            _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(MakeLogs());
            var svc = CreateService();

            var result = await svc.GetPagedAsync(1, 10, action: "Added");

            Assert.Equal(1, result.TotalRecords);
        }

        [Fact]
        public async Task GetPaged_TableFilter_ReturnsMatching()
        {
            _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(MakeLogs());
            var svc = CreateService();

            var result = await svc.GetPagedAsync(1, 10, table: "timesheets");

            Assert.Equal(1, result.TotalRecords);
        }

        [Fact]
        public async Task GetPaged_SortAsc_OrdersCorrectly()
        {
            _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(MakeLogs());
            var svc = CreateService();

            var result = await svc.GetPagedAsync(1, 10, sortDir: "asc");
            var list = result.Data.ToList();

            Assert.True(list[0].ChangedAt <= list[1].ChangedAt);
        }

        [Fact]
        public async Task GetPaged_Page2_ReturnsCorrectItems()
        {
            _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(MakeLogs());
            var svc = CreateService();

            var result = await svc.GetPagedAsync(2, 2);

            Assert.Equal(1, result.Data.Count());
            Assert.Equal(2, result.TotalPages);
        }
    }
}

    public class AuditServiceEdgeCaseTests
    {
        private readonly Mock<IRepository<int, AuditLog>> _repo = new();

        private AuditService CreateService() => new(_repo.Object);

        private List<AuditLog> MakeLogs() => new()
        {
            new AuditLog { Id = 1, TableName = "Users",      Action = "Added",    UserId = 1, ChangedAt = DateTime.UtcNow.AddMinutes(-10), KeyValues = "id=1" },
            new AuditLog { Id = 2, TableName = "Timesheets", Action = "Modified", UserId = 2, ChangedAt = DateTime.UtcNow.AddMinutes(-5),  KeyValues = "id=2" },
            new AuditLog { Id = 3, TableName = "Users",      Action = "Deleted",  UserId = 1, ChangedAt = DateTime.UtcNow,                 KeyValues = "id=3" },
        };

        // ── GetAll — empty repository ─────────────────────────────────────────

        [Fact]
        public async Task GetAll_EmptyRepo_ReturnsEmpty()
        {
            _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<AuditLog>());
            var svc = CreateService();

            var result = await svc.GetAllAsync();

            Assert.Empty(result);
        }


        // ── GetByAction — no match ────────────────────────────────────────────

        [Fact]
        public async Task GetByAction_NoMatch_ReturnsEmpty()
        {
            _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(MakeLogs());
            var svc = CreateService();

            var result = await svc.GetByActionAsync("Restored");

            Assert.Empty(result);
        }

        

        // ── GetPaged — combined filters ───────────────────────────────────────

        [Fact]
        public async Task GetPaged_TableAndActionFilter_ReturnsIntersection()
        {
            _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(MakeLogs());
            var svc = CreateService();

            var result = await svc.GetPagedAsync(1, 10, table: "users", action: "Added");

            Assert.Equal(1, result.TotalRecords);
        }

        // ── GetPaged — page beyond total ──────────────────────────────────────

        [Fact]
        public async Task GetPaged_PageBeyondTotal_ReturnsEmpty()
        {
            _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(MakeLogs());
            var svc = CreateService();

            var result = await svc.GetPagedAsync(10, 10);

            Assert.Empty(result.Data);
            Assert.Equal(3, result.TotalRecords);
        }

        // ── GetByUser — user with no logs ─────────────────────────────────────

        [Fact]
        public async Task GetByUser_UserWithNoLogs_ReturnsEmpty()
        {
            _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(MakeLogs());
            var svc = CreateService();

            var result = await svc.GetByUserAsync(999);

            Assert.Empty(result);
        }

        // ── GetAll — single log ───────────────────────────────────────────────

        [Fact]
        public async Task GetAll_SingleLog_ReturnsSingleItem()
        {
            var single = new List<AuditLog>
            {
                new AuditLog { Id = 1, TableName = "Projects", Action = "Added", UserId = 5, ChangedAt = DateTime.UtcNow }
            };
            _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(single);
            var svc = CreateService();

            var result = await svc.GetAllAsync();

            Assert.Single(result);
        }

        // ── GetPaged — sort desc (default) ───────────────────────────────────

        [Fact]
        public async Task GetPaged_DefaultSort_IsDescending()
        {
            _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(MakeLogs());
            var svc = CreateService();

            var result = await svc.GetPagedAsync(1, 10);
            var list = result.Data.ToList();

            Assert.True(list[0].ChangedAt >= list[list.Count - 1].ChangedAt);
        }
    }

    public class AuditServiceAdditionalTests
    {
        private readonly Mock<IRepository<int, AuditLog>> _repo = new();

        private AuditService CreateService() => new(_repo.Object);

        private List<AuditLog> MakeLogs() => new()
        {
            new AuditLog { Id = 1, TableName = "Users",      Action = "Added",    UserId = 1, ChangedAt = DateTime.UtcNow.AddMinutes(-10), KeyValues = "id=1" },
            new AuditLog { Id = 2, TableName = "Timesheets", Action = "Modified", UserId = 2, ChangedAt = DateTime.UtcNow.AddMinutes(-5),  KeyValues = "id=2" },
            new AuditLog { Id = 3, TableName = "Users",      Action = "Deleted",  UserId = 1, ChangedAt = DateTime.UtcNow,                 KeyValues = "id=3" },
        };

        // ── GetAll: returns all logs ordered by date desc ─────────────────────

        [Fact]
        public async Task GetAll_MultipleItems_OrderedDescending()
        {
            _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(MakeLogs());

            var result = (await CreateService().GetAllAsync()).ToList();

            Assert.Equal(3, result.Count);
            Assert.True(result[0].ChangedAt >= result[1].ChangedAt);
            Assert.True(result[1].ChangedAt >= result[2].ChangedAt);
        }

        // ── GetByTable: case-insensitive match ────────────────────────────────

        [Fact]
        public async Task GetByTable_LowerCase_ReturnsMatchingLogs()
        {
            _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(MakeLogs());

            var result = await CreateService().GetByTableAsync("timesheets");

            Assert.Single(result);
        }

        // ── GetByAction: returns only matching action ─────────────────────────

        [Fact]
        public async Task GetByAction_Deleted_ReturnsOnlyDeletedLogs()
        {
            _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(MakeLogs());

            var result = (await CreateService().GetByActionAsync("Deleted")).ToList();

            Assert.Single(result);
            Assert.Equal("Deleted", result[0].Action);
        }

        // ── GetByUser: returns logs for specific user ─────────────────────────

        [Fact]
        public async Task GetByUser_UserId1_ReturnsTwoLogs()
        {
            _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(MakeLogs());

            var result = (await CreateService().GetByUserAsync(1)).ToList();

            Assert.Equal(2, result.Count);
            Assert.All(result, l => Assert.Equal(1, l.UserId));
        }

       

        // ── GetPaged: combined table+action filter ────────────────────────────

        [Fact]
        public async Task GetPaged_TableAndAction_ReturnsIntersection()
        {
            _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(MakeLogs());

            var result = await CreateService().GetPagedAsync(1, 10, table: "users", action: "Deleted");

            Assert.Equal(1, result.TotalRecords);
        }

        // ── GetPaged: page beyond total returns empty data ────────────────────

        [Fact]
        public async Task GetPaged_PageBeyondTotal_DataIsEmpty()
        {
            _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(MakeLogs());

            var result = await CreateService().GetPagedAsync(100, 10);

            Assert.Empty(result.Data);
            Assert.Equal(3, result.TotalRecords);
        }

        // ── GetById: returns correct log ──────────────────────────────────────

        [Fact]
        public async Task GetById_ExistingId_ReturnsCorrectLog()
        {
            var log = MakeLogs()[1];
            _repo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(log);

            var result = await CreateService().GetByIdAsync(2);

            Assert.NotNull(result);
            Assert.Equal("Timesheets", result!.TableName);
        }

        // ── GetAll: empty repo returns empty ──────────────────────────────────

        [Fact]
        public async Task GetAll_EmptyRepo_ReturnsEmptyCollection()
        {
            _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<AuditLog>());

            var result = await CreateService().GetAllAsync();

            Assert.Empty(result);
        }

        // ── GetPaged: search filter matches table name ────────────────────────

        [Fact]
        public async Task GetPaged_SearchFilter_MatchesTableName()
        {
            _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(MakeLogs());

            var result = await CreateService().GetPagedAsync(1, 10, search: "timesheet");

            Assert.Equal(1, result.TotalRecords);
        }

        // ── GetPaged: sort ascending orders correctly ─────────────────────────

        [Fact]
        public async Task GetPaged_SortAscending_FirstItemIsOldest()
        {
            _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(MakeLogs());

            var result = await CreateService().GetPagedAsync(1, 10, sortDir: "asc");
            var list = result.Data.ToList();

            Assert.True(list[0].ChangedAt <= list[list.Count - 1].ChangedAt);
        }
    }
