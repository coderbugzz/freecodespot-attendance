using FreeCodeSpot.Attendance.Application.Employees;
using FreeCodeSpot.Attendance.Domain.Employees;

namespace FreeCodeSpot.Attendance.Infrastructure.Employees;

/// <summary>
/// Holds the roster in memory. The list is fixed at startup and lives only for
/// the lifetime of the process, which is enough to get the roster on screen.
/// </summary>
public sealed class InMemoryEmployeeRepository : IEmployeeRepository
{
    private static readonly Employee[] Employees =
    [
        new(1, "Regie Baquero", "Engineering", "regie@freecodespot.local"),
        new(2, "Anna Cruz", "Engineering", "anna@freecodespot.local"),
        new(3, "Marco Diaz", "Support", "marco@freecodespot.local"),
        new(4, "Lina Reyes", "Human Resources", "lina@freecodespot.local"),
        new(5, "Paolo Santos", "Operations", "paolo@freecodespot.local")
    ];

    public IReadOnlyList<Employee> GetAll() => Employees;

    public Employee? GetById(int id) =>
        Array.Find(Employees, employee => employee.Id == id);
}
