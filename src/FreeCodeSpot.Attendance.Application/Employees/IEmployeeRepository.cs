using FreeCodeSpot.Attendance.Domain.Employees;

namespace FreeCodeSpot.Attendance.Application.Employees;

/// <summary>
/// Storage for the employee roster. The Application layer owns this contract
/// so the storage that satisfies it can change without touching callers.
/// There is no delete: employees are deactivated through UpdateAsync.
/// </summary>
public interface IEmployeeRepository
{
    Task<IReadOnlyList<Employee>> GetAllAsync(bool includeInactive, CancellationToken cancellationToken = default);

    Task<Employee?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when another employee, active or not, already uses this email.
    /// </summary>
    Task<bool> EmailExistsAsync(string email, int? exceptId, CancellationToken cancellationToken = default);

    Task<Employee> AddAsync(Employee employee, CancellationToken cancellationToken = default);

    Task UpdateAsync(Employee employee, CancellationToken cancellationToken = default);
}
