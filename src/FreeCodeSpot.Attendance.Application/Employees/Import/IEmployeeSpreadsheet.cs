namespace FreeCodeSpot.Attendance.Application.Employees.Import;

/// <summary>
/// Reads and writes the .xlsx file of the employee import. The Application layer owns this
/// contract, so the rules of the import can be tested with plain rows and without an
/// Excel library.
/// </summary>
public interface IEmployeeSpreadsheet
{
    /// <summary>Reads the employees from the first sheet of a workbook.</summary>
    EmployeeSheetReadResult Read(Stream xlsx);

    /// <summary>A workbook with the three column headers and no employees, ready to be filled in.</summary>
    byte[] CreateTemplate();
}
