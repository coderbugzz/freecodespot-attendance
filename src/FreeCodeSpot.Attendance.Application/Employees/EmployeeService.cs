using FreeCodeSpot.Attendance.Domain.Employees;

namespace FreeCodeSpot.Attendance.Application.Employees;

/// <summary>
/// The roster use cases the API exposes. Endpoints call this instead of
/// reaching for a repository directly.
/// </summary>
public sealed class EmployeeService(IEmployeeRepository repository)
{
    /// <summary>
    /// Returns the roster ordered by name, which is the order the UI displays.
    /// </summary>
    public IReadOnlyList<Employee> GetRoster() =>
        repository.GetAll()
            .OrderBy(employee => employee.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public Employee? GetEmployee(int id) => repository.GetById(id);
}
