using Microsoft.AspNetCore.Authorization;
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
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddSingleton<INotificationService, NotificationService>();

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
    public override async Task OnConnectedAsync()
    {
        // Parse JWT directly from query string — Context.User may be null without [Authorize]
        var token = Context.GetHttpContext()?.Request.Query["access_token"].ToString();
        string? userId = null;
        string? role   = null;

        if (!string.IsNullOrEmpty(token))
        {
            try
            {
                var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                var jwt     = handler.ReadJwtToken(token);
                userId = jwt.Claims.FirstOrDefault(c =>
                    c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier" ||
                    c.Type == "nameid" || c.Type == "sub")?.Value;
                role   = jwt.Claims.FirstOrDefault(c =>
                    c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role" ||
                    c.Type == "role")?.Value;
            }
            catch { }
        }

        // Fallback to Context.User
        userId ??= Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        role   ??= Context.User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

        if (!string.IsNullOrEmpty(userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
        if (!string.IsNullOrEmpty(role))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"role_{role}");

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var token = Context.GetHttpContext()?.Request.Query["access_token"].ToString();
        string? userId = null;
        string? role   = null;

        if (!string.IsNullOrEmpty(token))
        {
            try
            {
                var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                var jwt     = handler.ReadJwtToken(token);
                userId = jwt.Claims.FirstOrDefault(c =>
                    c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier" ||
                    c.Type == "nameid" || c.Type == "sub")?.Value;
                role   = jwt.Claims.FirstOrDefault(c =>
                    c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role" ||
                    c.Type == "role")?.Value;
            }
            catch { }
        }

        userId ??= Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        role   ??= Context.User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

        if (!string.IsNullOrEmpty(userId))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
        if (!string.IsNullOrEmpty(role))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"role_{role}");

        await base.OnDisconnectedAsync(exception);
    }
}