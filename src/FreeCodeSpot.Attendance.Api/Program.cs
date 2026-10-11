using FreeCodeSpot.Attendance.Api.Auth;
using FreeCodeSpot.Attendance.Api.Endpoints;
using FreeCodeSpot.Attendance.Application.Attendance;
using FreeCodeSpot.Attendance.Application.Employees;
using FreeCodeSpot.Attendance.Application.Employees.Import;
using FreeCodeSpot.Attendance.Application.Reports;
using FreeCodeSpot.Attendance.Application.Security;
using FreeCodeSpot.Attendance.Application.Timesheets;
using FreeCodeSpot.Attendance.Infrastructure;
using FreeCodeSpot.Attendance.Infrastructure.Identity;
using FreeCodeSpot.Attendance.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
var connectionString = builder.Configuration.GetConnectionString("AttendanceDb")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:AttendanceDb is not configured. Add it to appsettings.json.");

builder.Services.AddInfrastructure(connectionString);
builder.Services.AddScoped<EmployeeService>();
builder.Services.AddScoped<AttendanceService>();
builder.Services.AddSingleton(TimeProvider.System);

// A day on a timesheet is a day in this time zone. Without a setting, it is the time zone of the server.
var timeZoneId = builder.Configuration["Attendance:TimeZoneId"];
builder.Services.AddSingleton(string.IsNullOrWhiteSpace(timeZoneId)
    ? TimeZoneInfo.Local
    : TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
builder.Services.AddScoped<TimesheetService>();

// The rules of the reports: when work starts, the grace period and the working days.
var reportOptions = builder.Configuration.GetSection("Attendance").Get<ReportOptions>() ?? new ReportOptions();
if (reportOptions.WorkingDays.Length == 0)
{
    reportOptions.WorkingDays = ReportOptions.DefaultWorkingDays;
}

builder.Services.AddSingleton(reportOptions);
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<ReportExportService>();
builder.Services.AddScoped<EmployeeImportService>();

// Authentication: callers present a bearer token that /auth/login issued.
builder.Services.AddAuthentication(IdentityConstants.BearerScheme)
    .AddBearerToken(IdentityConstants.BearerScheme);

// Identity: users and roles in the same database, plus the SignInManager that checks passwords.
builder.Services.AddIdentityCore<IdentityUser>()
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AttendanceDbContext>()
    .AddSignInManager();

// Authorization: a named policy per rule, so endpoints say what they need.
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.ManagerOnly, policy => policy.RequireRole(Roles.Manager))
    .AddPolicy(Policies.HasEmployeeRecord, policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim(AttendanceClaims.EmployeeId));

builder.Services.Configure<SeedUsersOptions>(builder.Configuration.GetSection(SeedUsersOptions.SectionName));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    await IdentitySeeder.SeedAsync(app.Services);
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapEmployeeEndpoints();
app.MapAttendanceEndpoints();
app.MapMyAttendanceEndpoints();
app.MapReportEndpoints();

app.Run();

/// <summary>
/// Exposed so the integration tests can boot this application with
/// WebApplicationFactory.
/// </summary>
public partial class Program;
