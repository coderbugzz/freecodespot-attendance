using FreeCodeSpot.Attendance.Application.Employees;
using FreeCodeSpot.Attendance.Domain.Employees;

namespace FreeCodeSpot.Attendance.UnitTests.Employees;

public class EmployeeServiceTests
{
    /// <summary>
    /// A list-backed repository, so the service rules can be tested without SQL Server.
    /// Email comparison ignores case, like SQL Server's default collation.
    /// </summary>
    private sealed class FakeRepository(params Employee[] seed) : IEmployeeRepository
    {
        public List<Employee> Employees { get; } = [.. seed];

        public Task<IReadOnlyList<Employee>> GetAllAsync(bool includeInactive, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Employee>>(
                Employees.Where(employee => includeInactive || employee.IsActive).ToList());

        public Task<Employee?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Employees.Find(employee => employee.Id == id));

        public Task<bool> EmailExistsAsync(string email, int? exceptId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Employees.Exists(employee =>
                string.Equals(employee.Email, email, StringComparison.OrdinalIgnoreCase) && employee.Id != exceptId));

        public Task<Employee> AddAsync(Employee employee, CancellationToken cancellationToken = default)
        {
            var added = employee with { Id = Employees.Count == 0 ? 1 : Employees.Max(e => e.Id) + 1 };
            Employees.Add(added);
            return Task.FromResult(added);
        }

        public Task UpdateAsync(Employee employee, CancellationToken cancellationToken = default)
        {
            Employees[Employees.FindIndex(e => e.Id == employee.Id)] = employee;
            return Task.CompletedTask;
        }
    }

    private static readonly Employee Anna = new(1, "Anna Cruz", "Engineering", "anna@example.com");
    private static readonly Employee Marco = new(2, "Marco Diaz", "Support", "marco@example.com");

    [Fact]
    public async Task GetRoster_OrdersEmployeesByFullName()
    {
        var service = new EmployeeService(new FakeRepository(
            new Employee(1, "Zed Ortiz", "Support", "zed@example.com"),
            new Employee(2, "amy Lee", "Engineering", "amy@example.com"),
            new Employee(3, "Ben Cruz", "Operations", "ben@example.com")));

        var roster = await service.GetRosterAsync();

        Assert.Equal(["amy Lee", "Ben Cruz", "Zed Ortiz"], roster.Select(employee => employee.FullName));
    }

    [Fact]
    public async Task GetRoster_ReturnsEmptyListWhenRepositoryIsEmpty()
    {
        var service = new EmployeeService(new FakeRepository());

        Assert.Empty(await service.GetRosterAsync());
    }

    [Fact]
    public async Task GetRoster_LeavesOutInactiveEmployeesUnlessAskedFor()
    {
        var service = new EmployeeService(new FakeRepository(Anna, Marco with { IsActive = false }));

        Assert.Equal(["Anna Cruz"], (await service.GetRosterAsync()).Select(e => e.FullName));
        Assert.Equal(2, (await service.GetRosterAsync(includeInactive: true)).Count);
    }

    [Fact]
    public async Task Create_TrimsTheValuesAndReturnsTheNewEmployee()
    {
        var repository = new FakeRepository(Anna);
        var service = new EmployeeService(repository);

        var result = await service.CreateAsync(new EmployeeDetails("  Joy Mendoza ", " Finance", "joy@example.com  "));

        Assert.Equal(EmployeeOutcome.Success, result.Outcome);
        Assert.Equal(new Employee(2, "Joy Mendoza", "Finance", "joy@example.com"), result.Employee);
        Assert.True(result.Employee!.IsActive);
        Assert.Equal(2, repository.Employees.Count);
    }

    [Fact]
    public async Task Create_ReportsEveryMissingField()
    {
        var service = new EmployeeService(new FakeRepository());

        var result = await service.CreateAsync(new EmployeeDetails(" ", "", null!));

        Assert.Equal(EmployeeOutcome.Invalid, result.Outcome);
        Assert.Equal(["Name is required."], result.Errors["FullName"]);
        Assert.Equal(["Department is required."], result.Errors["Department"]);
        Assert.Equal(["Email is required."], result.Errors["Email"]);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("joy@")]
    [InlineData("Joy Mendoza <joy@example.com>")]
    public async Task Create_RejectsAnInvalidEmail(string email)
    {
        var service = new EmployeeService(new FakeRepository());

        var result = await service.CreateAsync(new EmployeeDetails("Joy Mendoza", "Finance", email));

        Assert.Equal(["Email is not a valid email address."], result.Errors["Email"]);
    }

    [Fact]
    public async Task Create_RejectsAValueLongerThanTheColumn()
    {
        var service = new EmployeeService(new FakeRepository());

        var result = await service.CreateAsync(new EmployeeDetails(new string('a', 101), "Finance", "joy@example.com"));

        Assert.Equal(["Name must be 100 characters or fewer."], result.Errors["FullName"]);
    }

    [Fact]
    public async Task Create_RejectsAnEmailThatIsAlreadyUsed_EvenByAnInactiveEmployee()
    {
        var service = new EmployeeService(new FakeRepository(Anna with { IsActive = false }));

        var result = await service.CreateAsync(new EmployeeDetails("Anna Reyes", "Finance", "ANNA@example.com"));

        Assert.Equal(EmployeeOutcome.Invalid, result.Outcome);
        Assert.Equal(["Another employee already uses this email."], result.Errors["Email"]);
    }

    [Fact]
    public async Task Update_ChangesTheDetailsAndKeepsTheStatus()
    {
        var repository = new FakeRepository(Anna, Marco);
        var service = new EmployeeService(repository);

        var result = await service.UpdateAsync(2, new EmployeeDetails("Marco Diaz", "Operations", "marco@example.com"));

        Assert.Equal(EmployeeOutcome.Success, result.Outcome);
        Assert.Equal(Marco with { Department = "Operations" }, repository.Employees[1]);
    }

    [Fact]
    public async Task Update_AllowsAnEmployeeToKeepTheirOwnEmail()
    {
        var service = new EmployeeService(new FakeRepository(Anna));

        var result = await service.UpdateAsync(1, new EmployeeDetails("Anna Cruz", "Support", "anna@example.com"));

        Assert.Equal(EmployeeOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task Update_RejectsTheEmailOfAnotherEmployee()
    {
        var service = new EmployeeService(new FakeRepository(Anna, Marco));

        var result = await service.UpdateAsync(2, new EmployeeDetails("Marco Diaz", "Support", "anna@example.com"));

        Assert.Equal(["Another employee already uses this email."], result.Errors["Email"]);
    }

    [Fact]
    public async Task Update_ReturnsNotFoundForAnUnknownId()
    {
        var service = new EmployeeService(new FakeRepository(Anna));

        var result = await service.UpdateAsync(99, new EmployeeDetails("Nobody", "None", "nobody@example.com"));

        Assert.Equal(EmployeeOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task Deactivate_KeepsTheEmployeeAndMarksThemInactive()
    {
        var repository = new FakeRepository(Anna, Marco);
        var service = new EmployeeService(repository);

        var result = await service.DeactivateAsync(2);

        Assert.Equal(EmployeeOutcome.Success, result.Outcome);
        Assert.Equal(2, repository.Employees.Count);
        Assert.False(repository.Employees[1].IsActive);
    }

    [Fact]
    public async Task Deactivate_TwiceStillSucceeds()
    {
        var service = new EmployeeService(new FakeRepository(Anna with { IsActive = false }));

        var result = await service.DeactivateAsync(1);

        Assert.Equal(EmployeeOutcome.Success, result.Outcome);
        Assert.False(result.Employee!.IsActive);
    }

    [Fact]
    public async Task Activate_BringsAnInactiveEmployeeBack()
    {
        var repository = new FakeRepository(Anna with { IsActive = false });
        var service = new EmployeeService(repository);

        await service.ActivateAsync(1);

        Assert.True(repository.Employees[0].IsActive);
    }

    [Fact]
    public async Task DeactivateAndActivate_ReturnNotFoundForAnUnknownId()
    {
        var service = new EmployeeService(new FakeRepository());

        Assert.Equal(EmployeeOutcome.NotFound, (await service.DeactivateAsync(5)).Outcome);
        Assert.Equal(EmployeeOutcome.NotFound, (await service.ActivateAsync(5)).Outcome);
    }
}
