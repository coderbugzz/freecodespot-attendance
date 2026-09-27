using FreeCodeSpot.Attendance.Domain.Employees;

namespace FreeCodeSpot.Attendance.Application.Employees;

public enum EmployeeOutcome
{
    Success,
    NotFound,
    Invalid
}

/// <summary>
/// The result of a change to the roster. The API turns it into a status code;
/// Errors is keyed by field name so a form can show each message next to its input.
/// </summary>
public sealed record EmployeeResult(
    EmployeeOutcome Outcome,
    Employee? Employee,
    IReadOnlyDictionary<string, string[]> Errors)
{
    private static readonly IReadOnlyDictionary<string, string[]> NoErrors = new Dictionary<string, string[]>();

    public static EmployeeResult Success(Employee employee) => new(EmployeeOutcome.Success, employee, NoErrors);

    public static EmployeeResult NotFound() => new(EmployeeOutcome.NotFound, null, NoErrors);

    public static EmployeeResult Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        new(EmployeeOutcome.Invalid, null, errors);
}
