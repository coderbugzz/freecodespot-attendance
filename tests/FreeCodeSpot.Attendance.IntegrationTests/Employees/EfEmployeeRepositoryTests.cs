using FreeCodeSpot.Attendance.Application.Employees;
using FreeCodeSpot.Attendance.Domain.Employees;
using Microsoft.Extensions.DependencyInjection;

namespace FreeCodeSpot.Attendance.IntegrationTests.Employees;

/// <summary>
/// Runs the EF Core repository against the migrated test database, so the
/// mapping, the seed rows and the SQL Server queries are checked for real.
/// Tests that add rows use their own email addresses, so they do not affect
/// each other or the seeded roster.
/// </summary>
[Collection(AttendanceApiCollection.Name)]
public sealed class EfEmployeeRepositoryTests : IDisposable
{
    private static readonly string[] SeededNames =
        ["Regie Baquero", "Anna Cruz", "Marco Diaz", "Lina Reyes", "Paolo Santos"];

    private readonly IServiceScope scope;
    private readonly IEmployeeRepository repository;

    public EfEmployeeRepositoryTests(AttendanceApiFactory factory)
    {
        scope = factory.Services.CreateScope();
        repository = scope.ServiceProvider.GetRequiredService<IEmployeeRepository>();
    }

    public void Dispose() => scope.Dispose();

    [Fact]
    public async Task GetAll_ReturnsTheSeededRosterAsActive()
    {
        var employees = await repository.GetAllAsync(includeInactive: true);

        var seeded = employees.Where(employee => SeededNames.Contains(employee.FullName)).ToList();
        Assert.Equal(5, seeded.Count);
        Assert.All(seeded, employee => Assert.True(employee.IsActive));
    }

    [Fact]
    public async Task GetAll_ReturnsEmployeesWithUniqueIds()
    {
        var ids = (await repository.GetAllAsync(includeInactive: true)).Select(employee => employee.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public async Task GetById_ReturnsTheMatchingEmployee()
    {
        var employee = await repository.GetByIdAsync(3);

        Assert.NotNull(employee);
        Assert.Equal("Marco Diaz", employee.FullName);
        Assert.Equal("Support", employee.Department);
    }

    [Fact]
    public async Task GetById_ReturnsNullWhenTheEmployeeDoesNotExist()
    {
        Assert.Null(await repository.GetByIdAsync(999));
    }

    [Fact]
    public async Task Add_StoresTheEmployeeAndAssignsAnId()
    {
        var email = $"{Guid.NewGuid():N}@test.local";

        var added = await repository.AddAsync(new Employee(0, "Repo Test", "QA", email));

        Assert.True(added.Id > 0);
        Assert.Equal(added, await repository.GetByIdAsync(added.Id));
    }

    [Fact]
    public async Task Update_SavesTheChangedRecord()
    {
        var added = await repository.AddAsync(new Employee(0, "Repo Test", "QA", $"{Guid.NewGuid():N}@test.local"));

        await repository.UpdateAsync(added with { Department = "Support", IsActive = false });

        var reloaded = await repository.GetByIdAsync(added.Id);
        Assert.Equal("Support", reloaded!.Department);
        Assert.False(reloaded.IsActive);
        Assert.DoesNotContain(await repository.GetAllAsync(includeInactive: false), employee => employee.Id == added.Id);
    }

    [Fact]
    public async Task EmailExists_IgnoresCaseAndTheExcludedEmployee()
    {
        Assert.True(await repository.EmailExistsAsync("ANNA@freecodespot.local", exceptId: null));
        Assert.False(await repository.EmailExistsAsync("anna@freecodespot.local", exceptId: 2));
        Assert.False(await repository.EmailExistsAsync("nobody@freecodespot.local", exceptId: null));
    }
}
