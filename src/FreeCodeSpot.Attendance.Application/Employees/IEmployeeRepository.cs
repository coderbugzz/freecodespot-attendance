using FreeCodeSpot.Attendance.Domain.Employees;

namespace FreeCodeSpot.Attendance.Application.Employees;

/// <summary>
/// Read access to the employee roster. The Application layer owns this contract
/// so the storage that satisfies it can change without touching callers.
/// </summary>
public interface IEmployeeRepository
{
    IReadOnlyList<Employee> GetAll();

    Employee? GetById(int id);
}
