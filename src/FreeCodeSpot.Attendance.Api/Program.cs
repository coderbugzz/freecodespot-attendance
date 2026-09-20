using FreeCodeSpot.Attendance.Api.Endpoints;
using FreeCodeSpot.Attendance.Application.Employees;
using FreeCodeSpot.Attendance.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddInfrastructure();
builder.Services.AddScoped<EmployeeService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapEmployeeEndpoints();

app.Run();

/// <summary>
/// Exposed so the integration tests can boot this application with
/// WebApplicationFactory.
/// </summary>
public partial class Program;
