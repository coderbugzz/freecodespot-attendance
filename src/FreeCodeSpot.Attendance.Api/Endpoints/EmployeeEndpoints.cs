using FreeCodeSpot.Attendance.Application.Employees;
using FreeCodeSpot.Attendance.Domain.Employees;

namespace FreeCodeSpot.Attendance.Api.Endpoints;

/// <summary>
/// Maps the employee routes. Keeping them here stops Program.cs from turning
/// into a list of every route in the application.
/// </summary>
public static class EmployeeEndpoints
{
    public static IEndpointRouteBuilder MapEmployeeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/employees")
            .WithTags("Employees");

        group.MapGet("/", (EmployeeService employees) => Results.Ok(employees.GetRoster()))
            .WithName("GetEmployees")
            .Produces<IReadOnlyList<Employee>>();

        group.MapGet("/{id:int}", (int id, EmployeeService employees) =>
            {
                var employee = employees.GetEmployee(id);
                return employee is null ? Results.NotFound() : Results.Ok(employee);
            })
            .WithName("GetEmployeeById")
            .Produces<Employee>()
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
