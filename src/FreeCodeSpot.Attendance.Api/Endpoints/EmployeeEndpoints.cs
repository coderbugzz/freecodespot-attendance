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

        group.MapGet("/", async (EmployeeService employees, CancellationToken cancellationToken, bool includeInactive = false) =>
                Results.Ok(await employees.GetRosterAsync(includeInactive, cancellationToken)))
            .WithName("GetEmployees")
            .Produces<IReadOnlyList<Employee>>();

        group.MapGet("/{id:int}", async (int id, EmployeeService employees, CancellationToken cancellationToken) =>
            {
                var employee = await employees.GetEmployeeAsync(id, cancellationToken);
                return employee is null ? Results.NotFound() : Results.Ok(employee);
            })
            .WithName("GetEmployeeById")
            .Produces<Employee>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", async (EmployeeDetails details, EmployeeService employees, CancellationToken cancellationToken) =>
            {
                var result = await employees.CreateAsync(details, cancellationToken);
                return result.Outcome == EmployeeOutcome.Success
                    ? Results.CreatedAtRoute("GetEmployeeById", new { id = result.Employee!.Id }, result.Employee)
                    : ToErrorResult(result);
            })
            .WithName("CreateEmployee")
            .Produces<Employee>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapPut("/{id:int}", async (int id, EmployeeDetails details, EmployeeService employees, CancellationToken cancellationToken) =>
            {
                var result = await employees.UpdateAsync(id, details, cancellationToken);
                return result.Outcome == EmployeeOutcome.Success
                    ? Results.Ok(result.Employee)
                    : ToErrorResult(result);
            })
            .WithName("UpdateEmployee")
            .Produces<Employee>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        // Deactivate instead of DELETE: the employee and their history stay in the database.
        group.MapPost("/{id:int}/deactivate", async (int id, EmployeeService employees, CancellationToken cancellationToken) =>
                ToStatusResult(await employees.DeactivateAsync(id, cancellationToken)))
            .WithName("DeactivateEmployee")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:int}/activate", async (int id, EmployeeService employees, CancellationToken cancellationToken) =>
                ToStatusResult(await employees.ActivateAsync(id, cancellationToken)))
            .WithName("ActivateEmployee")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static IResult ToErrorResult(EmployeeResult result) =>
        result.Outcome == EmployeeOutcome.NotFound
            ? Results.NotFound()
            : Results.ValidationProblem(result.Errors.ToDictionary());

    private static IResult ToStatusResult(EmployeeResult result) =>
        result.Outcome == EmployeeOutcome.Success ? Results.NoContent() : ToErrorResult(result);
}
