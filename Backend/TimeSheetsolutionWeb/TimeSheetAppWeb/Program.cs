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

// ================= GENERIC REPOSITORY (BEST PRACTICE 🔥) =================
builder.Services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));

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
builder.Services.AddScoped<IAuditService, AuditService>(); // ✅ FIXED

// ================= SIGNALR =================
builder.Services.AddSignalR();

// ================= CORS =================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ================= CONTROLLERS =================
builder.Services.AddControllers();

// ================= JWT AUTH =================
var secretKey = configuration["Keys:Jwt"];
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
            IssuerSigningKey = key,

            ClockSkew = TimeSpan.Zero // 🔥 important (no extra expiry time)
        };

        // ✅ SignalR Token Support
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