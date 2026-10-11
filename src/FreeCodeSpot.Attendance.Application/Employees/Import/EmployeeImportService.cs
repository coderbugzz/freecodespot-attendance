namespace FreeCodeSpot.Attendance.Application.Employees.Import;

/// <summary>
/// Imports new employees from a spreadsheet. Every row goes through the same rules as the
/// Add employee form, because the service asks EmployeeService to check and to create.
/// A preview checks the rows and saves nothing. An import saves the rows that are fine and
/// skips the others, and the report says why.
/// </summary>
public sealed class EmployeeImportService(EmployeeService employees, IEmployeeSpreadsheet spreadsheet)
{
    public const int MaxRows = 500;
    public const long MaxFileBytes = 1024 * 1024;

    public byte[] CreateTemplate() => spreadsheet.CreateTemplate();

    /// <summary>Checks every row and saves nothing.</summary>
    public Task<EmployeeImportResult> PreviewAsync(Stream file, CancellationToken cancellationToken = default) =>
        RunAsync(file, save: false, cancellationToken);

    /// <summary>Saves the rows that are fine, and skips the rows that are not.</summary>
    public Task<EmployeeImportResult> ImportAsync(Stream file, CancellationToken cancellationToken = default) =>
        RunAsync(file, save: true, cancellationToken);

    private async Task<EmployeeImportResult> RunAsync(Stream file, bool save, CancellationToken cancellationToken)
    {
        var sheet = spreadsheet.Read(file);
        if (sheet.Error is not null)
        {
            return EmployeeImportResult.InvalidFile(sheet.Error);
        }

        if (sheet.Rows.Count == 0)
        {
            return EmployeeImportResult.InvalidFile("The file has no employees below the column headers.");
        }

        if (sheet.Rows.Count > MaxRows)
        {
            return EmployeeImportResult.InvalidFile($"The file can have at most {MaxRows} employees.");
        }

        // The database only knows the employees that exist. A second row with the same email
        // in the same file has to be caught here.
        var emailsOfValidRows = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<ImportRowResult>();

        foreach (var row in sheet.Rows)
        {
            var details = new EmployeeDetails(
                row.FullName?.Trim() ?? string.Empty,
                row.Department?.Trim() ?? string.Empty,
                row.Email?.Trim() ?? string.Empty);

            var errors = (await employees.CheckNewAsync(details, cancellationToken))
                .Values
                .SelectMany(messages => messages)
                .ToList();

            if (errors.Count == 0 && emailsOfValidRows.Contains(details.Email))
            {
                errors.Add("An earlier row in this file already uses this email.");
            }

            int? employeeId = null;
            if (errors.Count == 0)
            {
                emailsOfValidRows.Add(details.Email);

                if (save)
                {
                    var created = await employees.CreateAsync(details, cancellationToken);
                    if (created.Outcome == EmployeeOutcome.Success)
                    {
                        employeeId = created.Employee!.Id;
                    }
                    else
                    {
                        // Someone added the same email between the check and the save.
                        errors.AddRange(created.Errors.Values.SelectMany(messages => messages));
                    }
                }
            }

            rows.Add(new ImportRowResult(row.RowNumber, details.FullName, details.Department, details.Email, errors, employeeId));
        }

        return EmployeeImportResult.Success(new EmployeeImportReport(save, rows));
    }
}
