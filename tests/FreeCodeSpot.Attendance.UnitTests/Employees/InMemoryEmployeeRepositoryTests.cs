using FreeCodeSpot.Attendance.Application.Employees;
using FreeCodeSpot.Attendance.Infrastructure.Employees;

namespace FreeCodeSpot.Attendance.UnitTests.Employees;

public class InMemoryEmployeeRepositoryTests
{
    private readonly IEmployeeRepository repository = new InMemoryEmployeeRepository();

    [Fact]
    public void GetAll_ReturnsTheSeededRoster()
    {
        var employees = repository.GetAll();

        Assert.Equal(5, employees.Count);
        Assert.All(employees, employee => Assert.False(string.IsNullOrWhiteSpace(employee.FullName)));
    }

    [Fact]
    public void GetAll_ReturnsEmployeesWithUniqueIds()
    {
        var ids = repository.GetAll().Select(employee => employee.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void GetById_ReturnsTheMatchingEmployee()
    {
        var employee = repository.GetById(3);

        Assert.NotNull(employee);
        Assert.Equal(3, employee.Id);
    }

    [Fact]
    public void GetById_ReturnsNullWhenTheEmployeeDoesNotExist()
    {
        Assert.Null(repository.GetById(999));
    }
}
