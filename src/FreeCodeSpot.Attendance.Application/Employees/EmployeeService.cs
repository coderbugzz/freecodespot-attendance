using System.Net.Mail;
using FreeCodeSpot.Attendance.Domain.Employees;

namespace FreeCodeSpot.Attendance.Application.Employees;

/// <summary>
/// The roster use cases the API exposes. Endpoints call this instead of
/// reaching for a repository directly, and every rule for a valid employee
/// lives here so it can be unit tested without a database.
/// </summary>
public sealed class EmployeeService(IEmployeeRepository repository)
{
    // The same lengths as the columns in EmployeeConfiguration.
    public const int FullNameMaxLength = 100;
    public const int DepartmentMaxLength = 50;
    public const int EmailMaxLength = 256;

    /// <summary>
    /// Returns the roster ordered by name, which is the order the UI displays.
    /// Inactive employees are left out unless asked for.
    /// </summary>
    public async Task<IReadOnlyList<Employee>> GetRosterAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var employees = await repository.GetAllAsync(includeInactive, cancellationToken);

        return employees
            .OrderBy(employee => employee.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public Task<Employee?> GetEmployeeAsync(int id, CancellationToken cancellationToken = default) =>
        repository.GetByIdAsync(id, cancellationToken);

    public async Task<EmployeeResult> CreateAsync(EmployeeDetails details, CancellationToken cancellationToken = default)
    {
        var (clean, errors) = await ValidateAsync(details, exceptId: null, cancellationToken);
        if (errors.Count > 0)
        {
            return EmployeeResult.Invalid(errors);
        }

        var employee = await repository.AddAsync(
            new Employee(0, clean.FullName, clean.Department, clean.Email),
            cancellationToken);

        return EmployeeResult.Success(employee);
    }

    public async Task<EmployeeResult> UpdateAsync(int id, EmployeeDetails details, CancellationToken cancellationToken = default)
    {
        var existing = await repository.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return EmployeeResult.NotFound();
        }

        var (clean, errors) = await ValidateAsync(details, exceptId: id, cancellationToken);
        if (errors.Count > 0)
        {
            return EmployeeResult.Invalid(errors);
        }

        var updated = existing with
        {
            FullName = clean.FullName,
            Department = clean.Department,
            Email = clean.Email
        };
        await repository.UpdateAsync(updated, cancellationToken);

        return EmployeeResult.Success(updated);
    }

    public Task<EmployeeResult> DeactivateAsync(int id, CancellationToken cancellationToken = default) =>
        SetActiveAsync(id, isActive: false, cancellationToken);

    public Task<EmployeeResult> ActivateAsync(int id, CancellationToken cancellationToken = default) =>
        SetActiveAsync(id, isActive: true, cancellationToken);

    private async Task<EmployeeResult> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken)
    {
        var existing = await repository.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return EmployeeResult.NotFound();
        }

        // Deactivating an inactive employee (or activating an active one) changes nothing.
        if (existing.IsActive == isActive)
        {
            return EmployeeResult.Success(existing);
        }

        var updated = existing with { IsActive = isActive };
        await repository.UpdateAsync(updated, cancellationToken);

        return EmployeeResult.Success(updated);
    }

    private async Task<(EmployeeDetails Clean, Dictionary<string, string[]> Errors)> ValidateAsync(
        EmployeeDetails details,
        int? exceptId,
        CancellationToken cancellationToken)
    {
        var clean = new EmployeeDetails(
            details.FullName?.Trim() ?? string.Empty,
            details.Department?.Trim() ?? string.Empty,
            details.Email?.Trim() ?? string.Empty);

        var errors = new Dictionary<string, string[]>();

        if (clean.FullName.Length == 0)
            errors[nameof(EmployeeDetails.FullName)] = ["Name is required."];
        else if (clean.FullName.Length > FullNameMaxLength)
            errors[nameof(EmployeeDetails.FullName)] = [$"Name must be {FullNameMaxLength} characters or fewer."];

        if (clean.Department.Length == 0)
            errors[nameof(EmployeeDetails.Department)] = ["Department is required."];
        else if (clean.Department.Length > DepartmentMaxLength)
            errors[nameof(EmployeeDetails.Department)] = [$"Department must be {DepartmentMaxLength} characters or fewer."];

        if (clean.Email.Length == 0)
            errors[nameof(EmployeeDetails.Email)] = ["Email is required."];
        else if (clean.Email.Length > EmailMaxLength)
            errors[nameof(EmployeeDetails.Email)] = [$"Email must be {EmailMaxLength} characters or fewer."];
        else if (!IsEmailAddress(clean.Email))
            errors[nameof(EmployeeDetails.Email)] = ["Email is not a valid email address."];
        else if (await repository.EmailExistsAsync(clean.Email, exceptId, cancellationToken))
            errors[nameof(EmployeeDetails.Email)] = ["Another employee already uses this email."];

        return (clean, errors);
    }

    private static bool IsEmailAddress(string value) =>
        MailAddress.TryCreate(value, out var address) && address.Address == value;
}
