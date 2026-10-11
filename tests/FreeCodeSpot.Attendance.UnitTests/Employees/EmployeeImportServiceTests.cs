using FreeCodeSpot.Attendance.Application.Employees;
using FreeCodeSpot.Attendance.Application.Employees.Import;
using FreeCodeSpot.Attendance.Domain.Employees;

namespace FreeCodeSpot.Attendance.UnitTests.Employees;

public class EmployeeImportServiceTests
{
    /// <summary>A list-backed repository. Email comparison ignores case, like SQL Server's default collation.</summary>
    private sealed class FakeRepository(params Employee[] seed) : IEmployeeRepository
    {
        public List<Employee> Employees { get; } = [.. seed];

        public Task<IReadOnlyList<Employee>> GetAllAsync(bool includeInactive, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Employee>>(Employees.Where(employee => includeInactive || employee.IsActive).ToList());

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

        public Task UpdateAsync(Employee employee, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    /// <summary>Hands the service the rows of the test, so no Excel file is needed.</summary>
    private sealed class FakeSpreadsheet(EmployeeSheetReadResult result) : IEmployeeSpreadsheet
    {
        public EmployeeSheetReadResult Read(Stream xlsx) => result;

        public byte[] CreateTemplate() => [];
    }

    private static readonly Employee Anna = new(1, "Anna Cruz", "Engineering", "anna@example.com");

    private static EmployeeImportRow Row(int number, string? name, string? department, string? email) =>
        new(number, name, department, email);

    private static (EmployeeImportService Service, FakeRepository Repository) CreateService(params EmployeeImportRow[] rows)
    {
        var repository = new FakeRepository(Anna);
        var service = new EmployeeImportService(
            new EmployeeService(repository),
            new FakeSpreadsheet(EmployeeSheetReadResult.Of(rows)));
        return (service, repository);
    }

    [Fact]
    public async Task Preview_ChecksEveryRowAndSavesNothing()
    {
        var (service, repository) = CreateService(
            Row(2, "Joy Mendoza", "Finance", "joy@example.com"),
            Row(3, "Paolo Santos", "Operations", "paolo@example.com"));

        var result = await service.PreviewAsync(Stream.Null);

        var report = result.Report!;
        Assert.False(report.Saved);
        Assert.Equal(2, report.ValidCount);
        Assert.All(report.Rows, row => Assert.Null(row.EmployeeId));
        Assert.Single(repository.Employees);
    }

    [Fact]
    public async Task ARowWithMissingValuesGetsAMessageForEachOne()
    {
        var (service, _) = CreateService(Row(2, " ", null, "not-an-email"));

        var row = Assert.Single((await service.PreviewAsync(Stream.Null)).Report!.Rows);

        Assert.False(row.IsValid);
        Assert.Equal(2, row.RowNumber);
        Assert.Equal(["Name is required.", "Department is required.", "Email is not a valid email address."], row.Errors);
    }

    [Fact]
    public async Task AnEmailThatAnEmployeeAlreadyUsesIsAnError_EvenInAnotherCase()
    {
        var (service, _) = CreateService(Row(2, "Another Anna", "Finance", "ANNA@example.com"));

        var row = Assert.Single((await service.PreviewAsync(Stream.Null)).Report!.Rows);

        Assert.Equal(["Another employee already uses this email."], row.Errors);
    }

    [Fact]
    public async Task ASecondRowWithTheSameEmailInTheFileIsAnError()
    {
        var (service, _) = CreateService(
            Row(2, "Joy Mendoza", "Finance", "joy@example.com"),
            Row(3, "Joy Again", "Finance", "JOY@example.com"));

        var rows = (await service.PreviewAsync(Stream.Null)).Report!.Rows;

        Assert.True(rows[0].IsValid);
        Assert.Equal(["An earlier row in this file already uses this email."], rows[1].Errors);
    }

    [Fact]
    public async Task ARowThatIsNotImportedDoesNotBlockALaterRowWithTheSameEmail()
    {
        var (service, _) = CreateService(
            Row(2, "", "Finance", "joy@example.com"),
            Row(3, "Joy Mendoza", "Finance", "joy@example.com"));

        var rows = (await service.PreviewAsync(Stream.Null)).Report!.Rows;

        Assert.False(rows[0].IsValid);
        Assert.True(rows[1].IsValid);
    }

    [Fact]
    public async Task Import_SavesTheRowsThatAreFineAndSkipsTheOthers()
    {
        var (service, repository) = CreateService(
            Row(2, "Joy Mendoza", "Finance", "joy@example.com"),
            Row(3, "", "Finance", "nobody@example.com"),
            Row(4, "Paolo Santos", "Operations", "paolo@example.com"));

        var report = (await service.ImportAsync(Stream.Null)).Report!;

        Assert.True(report.Saved);
        Assert.Equal(2, report.ValidCount);
        Assert.Equal(1, report.InvalidCount);
        Assert.Equal([2, 3], report.Rows.Where(row => row.EmployeeId is not null).Select(row => row.EmployeeId!.Value));
        Assert.Null(report.Rows[1].EmployeeId);
        Assert.Equal(
            ["Anna Cruz", "Joy Mendoza", "Paolo Santos"],
            repository.Employees.Select(employee => employee.FullName));
    }

    [Fact]
    public async Task TheValuesAreTrimmedBeforeTheyAreSaved()
    {
        var (service, repository) = CreateService(Row(2, "  Joy Mendoza ", " Finance", " joy@example.com  "));

        await service.ImportAsync(Stream.Null);

        var joy = repository.Employees.Last();
        Assert.Equal(("Joy Mendoza", "Finance", "joy@example.com"), (joy.FullName, joy.Department, joy.Email));
    }

    [Fact]
    public async Task AFileTheReaderCannotUseIsRefusedWithItsReason()
    {
        var service = new EmployeeImportService(
            new EmployeeService(new FakeRepository()),
            new FakeSpreadsheet(EmployeeSheetReadResult.Failed("The file is not a valid .xlsx workbook.")));

        var result = await service.PreviewAsync(Stream.Null);

        Assert.Equal(EmployeeImportOutcome.InvalidFile, result.Outcome);
        Assert.Equal("The file is not a valid .xlsx workbook.", result.Error);
    }

    [Fact]
    public async Task AFileWithNoEmployeesIsRefused()
    {
        var (service, _) = CreateService();

        var result = await service.ImportAsync(Stream.Null);

        Assert.Equal(EmployeeImportOutcome.InvalidFile, result.Outcome);
    }

    [Fact]
    public async Task AFileWithMoreThanFiveHundredEmployeesIsRefused_AndNothingIsSaved()
    {
        var rows = Enumerable.Range(2, EmployeeImportService.MaxRows + 1)
            .Select(number => Row(number, $"Employee {number}", "Support", $"employee{number}@example.com"))
            .ToArray();
        var (service, repository) = CreateService(rows);

        var result = await service.ImportAsync(Stream.Null);

        Assert.Equal(EmployeeImportOutcome.InvalidFile, result.Outcome);
        Assert.Single(repository.Employees);
    }
}
