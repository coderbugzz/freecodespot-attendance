using FreeCodeSpot.Attendance.Application.Employees;
using Microsoft.Extensions.DependencyInjection;

namespace FreeCodeSpot.Attendance.IntegrationTests.Employees;

/// <summary>
/// Runs the EF Core repository against the migrated test database, so the
/// mapping, the seed rows and the SQL Server queries are checked for real.
/// </summary>
[Collection(AttendanceApiCollection.Name)]
public sealed class EfEmployeeRepositoryTests : IDisposable
{
    private readonly IServiceScope scope;
    private readonly IEmployeeRepository repository;

    public EfEmployeeRepositoryTests(AttendanceApiFactory factory)
    {
        scope = factory.Services.CreateScope();
        repository = scope.ServiceProvider.GetRequiredService<IEmployeeRepository>();
    }

    public void Dispose() => scope.Dispose();

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
        Assert.Equal("Marco Diaz", employee.FullName);
        Assert.Equal("Support", employee.Department);
    }

    [Fact]
    public void GetById_ReturnsNullWhenTheEmployeeDoesNotExist()
    {
        Assert.Null(repository.GetById(999));
    }
}
