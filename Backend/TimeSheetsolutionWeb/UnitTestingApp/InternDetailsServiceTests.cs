using Xunit;
using Microsoft.EntityFrameworkCore;
using TimeSheetAppWeb.Contexts;
using TimeSheetAppWeb.Services;
using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Model.DTOs;
using System;
using System.Threading.Tasks;
using System.Linq;

public class InternDetailsServiceTests
{
    private TimeSheetContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<TimeSheetContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TimeSheetContext(options);
    }

    // ---------------- CREATE SUCCESS ----------------
    [Fact]
    public async Task CreateAsync_Should_Succeed_When_User_Is_Intern()
    {
        var context = GetDbContext();

        var user = new User { Id = 1, Name = "InternUser", Role = UserRole.Intern };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = new InternDetailsService(context);

        var dto = new InternDetailsCreateDto
        {
            UserId = 1,
            TrainingStart = DateTime.Today,
            TrainingEnd = DateTime.Today.AddMonths(1)
        };

        var result = await service.CreateAsync(dto);

        Assert.True(result.Success);
        Assert.Equal("Intern created successfully", result.Message);
        Assert.Equal("InternUser", result.Data.UserName);
    }

    // ---------------- CREATE FAIL - USER NOT FOUND ----------------
    [Fact]
    public async Task CreateAsync_Should_Fail_When_User_Not_Found()
    {
        var context = GetDbContext();
        var service = new InternDetailsService(context);

        var dto = new InternDetailsCreateDto { UserId = 99 };

        var result = await service.CreateAsync(dto);

        Assert.False(result.Success);
        Assert.Equal("User not found", result.Message);
    }

    // ---------------- CREATE FAIL - WRONG ROLE ----------------
    [Fact]
    public async Task CreateAsync_Should_Fail_When_User_Not_Intern()
    {
        var context = GetDbContext();

        context.Users.Add(new User
        {
            Id = 2,
            Name = "ManagerUser",
            Role = UserRole.Manager
        });

        await context.SaveChangesAsync();

        var service = new InternDetailsService(context);

        var dto = new InternDetailsCreateDto { UserId = 2 };

        var result = await service.CreateAsync(dto);

        Assert.False(result.Success);
        Assert.Equal("Only users with role 'Intern' can be added", result.Message);
    }

    // ---------------- GET BY ID SUCCESS ----------------
    [Fact]
    public async Task GetByIdAsync_Should_Return_Intern_When_Exists()
    {
        var context = GetDbContext();

        var user = new User { Id = 3, Name = "TestIntern", Role = UserRole.Intern };
        context.Users.Add(user);

        context.InternDetails.Add(new InternDetails
        {
            Id = 1,
            UserId = 3,
            TrainingStart = DateTime.Today,
            TrainingEnd = DateTime.Today.AddMonths(1)
        });

        await context.SaveChangesAsync();

        var service = new InternDetailsService(context);

        var result = await service.GetByIdAsync(1);

        Assert.True(result.Success);
        Assert.Equal("TestIntern", result.Data.UserName);
    }

    // ---------------- GET BY ID FAIL ----------------
    [Fact]
    public async Task GetByIdAsync_Should_Return_NotFound_When_Not_Exists()
    {
        var context = GetDbContext();
        var service = new InternDetailsService(context);

        var result = await service.GetByIdAsync(100);

        Assert.False(result.Success);
        Assert.Equal("Intern not found", result.Message);
    }

    // ---------------- GET ALL ----------------
    [Fact]
    public async Task GetAllAsync_Should_Return_All_Interns()
    {
        var context = GetDbContext();

        var user = new User { Id = 4, Name = "InternA", Role = UserRole.Intern };
        context.Users.Add(user);

        context.InternDetails.Add(new InternDetails
        {
            Id = 10,
            UserId = 4,
            TrainingStart = DateTime.Today,
            TrainingEnd = DateTime.Today.AddMonths(2)
        });

        await context.SaveChangesAsync();

        var service = new InternDetailsService(context);

        var result = await service.GetAllAsync();

        Assert.True(result.Success);
        Assert.Single(result.Data);
    }

    // ---------------- UPDATE SUCCESS ----------------
    [Fact]
    public async Task UpdateAsync_Should_Update_Intern()
    {
        var context = GetDbContext();

        var user = new User { Id = 5, Name = "InternB", Role = UserRole.Intern };
        context.Users.Add(user);

        context.InternDetails.Add(new InternDetails
        {
            Id = 20,
            UserId = 5,
            TrainingStart = DateTime.Today,
            TrainingEnd = DateTime.Today.AddMonths(1)
        });

        await context.SaveChangesAsync();

        var service = new InternDetailsService(context);

        var dto = new InternDetailsCreateDto
        {
            UserId = 5,
            TrainingStart = DateTime.Today,
            TrainingEnd = DateTime.Today.AddMonths(3)
        };

        var result = await service.UpdateAsync(20, dto);

        Assert.True(result.Success);
        Assert.Equal("Intern updated successfully", result.Message);
    }

    // ---------------- DELETE SUCCESS ----------------
    [Fact]
    public async Task DeleteAsync_Should_Delete_Intern()
    {
        var context = GetDbContext();

        context.InternDetails.Add(new InternDetails
        {
            Id = 30,
            UserId = 1,
            TrainingStart = DateTime.Today,
            TrainingEnd = DateTime.Today.AddMonths(1)
        });

        await context.SaveChangesAsync();

        var service = new InternDetailsService(context);

        var result = await service.DeleteAsync(30);

        Assert.True(result.Success);
        Assert.True(result.Data);
    }

    // ---------------- DELETE FAIL ----------------
    [Fact]
    public async Task DeleteAsync_Should_Return_NotFound()
    {
        var context = GetDbContext();
        var service = new InternDetailsService(context);

        var result = await service.DeleteAsync(500);

        Assert.False(result.Success);
        Assert.False(result.Data);
    }
}