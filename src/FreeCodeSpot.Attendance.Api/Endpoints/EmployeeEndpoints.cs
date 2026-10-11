using FreeCodeSpot.Attendance.Api.Auth;
using FreeCodeSpot.Attendance.Application;
using FreeCodeSpot.Attendance.Application.Employees;
using FreeCodeSpot.Attendance.Application.Employees.Import;
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
            .WithTags("Employees")
            .RequireAuthorization(Policies.ManagerOnly);

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

        // Import from Excel. The preview checks every row and saves nothing. The import saves the
        // rows that are fine. The routes read an upload, which has no antiforgery token, because
        // the caller sends a bearer token and not a cookie.
        group.MapGet("/import/template", (EmployeeImportService import) =>
                Results.File(import.CreateTemplate(), ExcelFile.ContentType, "employee-import-template.xlsx"))
            .WithName("GetEmployeeImportTemplate")
            .Produces(StatusCodes.Status200OK, contentType: ExcelFile.ContentType);

        group.MapPost("/import/preview", (IFormFile? file, EmployeeImportService import, CancellationToken cancellationToken) =>
                RunImportAsync(file, save: false, import, cancellationToken))
            .DisableAntiforgery()
            .WithName("PreviewEmployeeImport")
            .Produces<EmployeeImportReport>()
            .ProducesValidationProblem();

        group.MapPost("/import", (IFormFile? file, EmployeeImportService import, CancellationToken cancellationToken) =>
                RunImportAsync(file, save: true, import, cancellationToken))
            .DisableAntiforgery()
            .WithName("ImportEmployees")
            .Produces<EmployeeImportReport>()
            .ProducesValidationProblem();

        return app;
    }

    private static async Task<IResult> RunImportAsync(
        IFormFile? file,
        bool save,
        EmployeeImportService import,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return FileProblem("Choose an .xlsx file to upload.");

        if (file.Length > EmployeeImportService.MaxFileBytes)
            return FileProblem("The file can be at most 1 MB.");

        await using var stream = file.OpenReadStream();
        var result = save
            ? await import.ImportAsync(stream, cancellationToken)
            : await import.PreviewAsync(stream, cancellationToken);

        return result.Outcome == EmployeeImportOutcome.Success
            ? Results.Ok(result.Report)
            : FileProblem(result.Error!);
    }

    private static IResult FileProblem(string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [message] });

    private static IResult ToErrorResult(EmployeeResult result) =>
        result.Outcome == EmployeeOutcome.NotFound
            ? Results.NotFound()
            : Results.ValidationProblem(result.Errors.ToDictionary());

    private static IResult ToStatusResult(EmployeeResult result) =>
        result.Outcome == EmployeeOutcome.Success ? Results.NoContent() : ToErrorResult(result);
}
