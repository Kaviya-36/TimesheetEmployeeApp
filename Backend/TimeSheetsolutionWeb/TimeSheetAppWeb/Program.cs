using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using TimeSheetAppWeb.Contexts;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Interfaces;
using TimeSheetAppWeb.Model;
using TimeSheetAppWeb.Repositories;
using TimeSheetAppWeb.Services;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

// ================= DATABASE =================
builder.Services.AddDbContext<TimeSheetContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

// ================= GENERIC REPOSITORIES =================
builder.Services.AddScoped<IRepository<int, User>, Repository<int, User>>();
builder.Services.AddScoped<IRepository<int, Department>, Repository<int, Department>>();
builder.Services.AddScoped<IRepository<int, Project>, Repository<int, Project>>();
builder.Services.AddScoped<IRepository<int, ProjectAssignment>, Repository<int, ProjectAssignment>>();
builder.Services.AddScoped<IRepository<int, Timesheet>, Repository<int, Timesheet>>();
builder.Services.AddScoped<IRepository<int, LeaveType>, Repository<int, LeaveType>>();
builder.Services.AddScoped<IRepository<int, LeaveRequest>, Repository<int, LeaveRequest>>();
builder.Services.AddScoped<IRepository<int, Attendance>, Repository<int, Attendance>>();
builder.Services.AddScoped<IRepository<int, Payroll>, Repository<int, Payroll>>();
builder.Services.AddScoped<IRepository<int, InternTask>, Repository<int, InternTask>>();
builder.Services.AddScoped<IRepository<int, InternDetails>, Repository<int, InternDetails>>();

// ================= SERVICES =================
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITimesheetService, TimesheetService>();
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ILeaveService, LeaveService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<IInternTaskService, InternTaskService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IPayrollService, PayrollService>();
builder.Services.AddScoped<IInternDetailsService, InternDetailsService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();

// ================= SIGNALR =================
builder.Services.AddSignalR();

// ================= CORS (IMPORTANT FOR ANGULAR + SIGNALR) =================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // REQUIRED for SignalR
    });
});

// ================= CONTROLLERS =================
builder.Services.AddControllers();

// ================= JWT AUTH =================
var secretKey = builder.Configuration["Keys:Jwt"];
if (string.IsNullOrEmpty(secretKey))
    throw new InvalidOperationException("JWT secret not configured properly.");

var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            IssuerSigningKey = key
        };

        // 🔥 IMPORTANT FOR SIGNALR AUTH (optional but recommended)
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];

                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/notificationHub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// ================= SWAGGER =================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter: Bearer YOUR_TOKEN"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ================= MIDDLEWARE =================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// 🔥 ORDER MATTERS
app.UseCors("AllowAngular");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ================= SIGNALR HUB =================
app.MapHub<NotificationHub>("/notificationHub");

app.Run();


// ================= HUB CLASS =================
public class NotificationHub : Hub
{
}