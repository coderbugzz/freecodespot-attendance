using FreeCodeSpot.Attendance.Application.Employees;
using FreeCodeSpot.Attendance.Domain.Employees;

namespace FreeCodeSpot.Attendance.UnitTests.Employees;

public class EmployeeServiceTests
{
    private sealed class StubRepository(params Employee[] employees) : IEmployeeRepository
    {
        public IReadOnlyList<Employee> GetAll() => employees;

        public Employee? GetById(int id) => Array.Find(employees, employee => employee.Id == id);
    }

    [Fact]
    public void GetRoster_OrdersEmployeesByFullName()
    {
        var service = new EmployeeService(new StubRepository(
            new Employee(1, "Zed Ortiz", "Support", "zed@example.com"),
            new Employee(2, "amy Lee", "Engineering", "amy@example.com"),
            new Employee(3, "Ben Cruz", "Operations", "ben@example.com")));

        var roster = service.GetRoster();

        Assert.Equal(["amy Lee", "Ben Cruz", "Zed Ortiz"], roster.Select(employee => employee.FullName));
    }

    [Fact]
    public void GetRoster_ReturnsEmptyListWhenRepositoryIsEmpty()
    {
        var service = new EmployeeService(new StubRepository());

        Assert.Empty(service.GetRoster());
    }

    [Fact]
    public void GetEmployee_PassesThroughToTheRepository()
    {
        var expected = new Employee(7, "Test Person", "QA", "test@example.com");
        var service = new EmployeeService(new StubRepository(expected));

        Assert.Equal(expected, service.GetEmployee(7));
        Assert.Null(service.GetEmployee(8));
    }
}
