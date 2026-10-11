# FreeCodeSpot Attendance

Source code for the FreeCodeSpot article series that builds an **Employee Attendance System** with .NET 10, ASP.NET Core and Blazor.

Each published article corresponds to an immutable Git tag `article-NNN`. Check out the tag to see the code exactly as the article describes it.

## What is here

An ASP.NET Core minimal API that manages an employee roster and their clock-ins in SQL Server, and a Blazor Web App to add, edit, deactivate and reactivate employees and to clock them in and out, to see timesheets, to read the attendance report and to move data in and out of Excel. Employees are never deleted. Users sign in with an email and a password. Managers can open the Employees pages and the Clock in and out page, and employees clock themselves in and out on a My attendance page. Timesheets show the records and the hours worked between two dates: managers see anyone, and employees see their own. The Reports page is for managers. It shows a summary per employee, the late arrivals and the absences between two dates, and it downloads them as an .xlsx file. The Employees page can import new employees from an .xlsx file.

```
src/FreeCodeSpot.Attendance.Domain           Employee record (with IsActive), AttendanceRecord
src/FreeCodeSpot.Attendance.Application      EmployeeService (the roster rules), IEmployeeRepository, EmployeeDetails, EmployeeResult, Roles, AttendanceService (the clock-in and clock-out rules), IAttendanceRepository, TimesheetService (days and hours), ITimesheetRepository, ReportService (late arrivals, absences, summary), ReportOptions, ReportExportService, EmployeeImportService
src/FreeCodeSpot.Attendance.Infrastructure   AttendanceDbContext (Identity and employees), migrations, EfEmployeeRepository, EfAttendanceRepository, EfTimesheetRepository, ExcelReportWorkbook and ExcelEmployeeSpreadsheet (ClosedXML), IdentitySeeder, AddInfrastructure()
src/FreeCodeSpot.Attendance.Api              minimal API: bearer-token login and the protected roster routes, see Endpoints below
src/FreeCodeSpot.Attendance.Web              Blazor Web App (interactive server): login, role-aware navigation, Employees, Add and Edit pages, Clock in and out page
tests/FreeCodeSpot.Attendance.UnitTests      xUnit tests for EmployeeService
tests/FreeCodeSpot.Attendance.IntegrationTests   API and repository tests against a SQL Server test database
```

The roster is stored in SQL Server through Entity Framework Core. The first migration creates the `Employees` table and seeds five employees. The second adds the `IsActive` column, with existing employees set to active. The third adds the ASP.NET Core Identity tables for users and roles. The fourth adds the `AttendanceRecords` table, with a unique index that allows only one open record per employee. The fifth adds an index on the employee and the clock-in time, for the timesheet queries.

## Requirements

- .NET 10 SDK
- A trusted HTTPS development certificate (`dotnet dev-certs https --trust`)
- SQL Server LocalDB (installed with Visual Studio), or any SQL Server instance
- The EF Core CLI: `dotnet tool install --global dotnet-ef`

## Database

The API reads its connection string from `ConnectionStrings:AttendanceDb` in `src/FreeCodeSpot.Attendance.Api/appsettings.json`. The default points at LocalDB:

```
Server=(localdb)\MSSQLLocalDB;Database=FreeCodeSpotAttendance;Trusted_Connection=True;TrustServerCertificate=True
```

Create the database from the repository root:

```bash
dotnet ef database update --project src/FreeCodeSpot.Attendance.Infrastructure --startup-project src/FreeCodeSpot.Attendance.Api
```

If you already have the database from an earlier article, run the same command. It applies only the migrations you are missing.

## Running it

Build and test from the repository root. The integration tests create a separate `FreeCodeSpotAttendance_Tests` database on LocalDB and drop it when they finish:

```bash
dotnet build
dotnet test
```

Run the API and the Web app in two terminals:

```bash
cd src/FreeCodeSpot.Attendance.Api
dotnet run --launch-profile https
```

```bash
cd src/FreeCodeSpot.Attendance.Web
dotnet run --launch-profile https
```

| Project | URL |
|---|---|
| API | https://localhost:7020 |
| Web | https://localhost:7098 |

Open https://localhost:7098/employees to manage the roster. The Web app reads the API address from `AttendanceApi:BaseUrl` in `appsettings.json`.

## Time zone

A day on a timesheet is a day in the time zone from `Attendance:TimeZoneId` in `src/FreeCodeSpot.Attendance.Api/appsettings.json`, for example `Asia/Manila`. When the setting is empty, the API uses the time zone of the server. A record counts on the day it was clocked in, even if it ends after midnight. A range can be at most 31 days, and records that are still open are listed but add nothing to the totals. The timesheet pages show the times in the time zone the API used, so both apps should run on the same machine or use the same setting.

## Report rules

The reports read three settings from the `Attendance` section of `src/FreeCodeSpot.Attendance.Api/appsettings.json`, in the same time zone as the timesheets:

| Setting | Default | Meaning |
|---|---|---|
| `WorkStart` | `08:00` | when work starts |
| `GraceMinutes` | `10` | minutes after the start that are still on time |
| `WorkingDays` | Monday to Friday | the days people are expected to work; there is no holiday calendar |

An employee is late when the first clock-in of a working day comes after `WorkStart` plus the grace minutes, and the report shows how late, counted from `WorkStart`. An active employee is absent on a working day that is over when they have no record that day. Today is never an absence, an open record counts as present, and inactive employees are never absent. The roster has no start date, so an employee added in the middle of a range is listed as absent for the days before they were added.

## Excel files

Both directions use [ClosedXML](https://github.com/ClosedXML/ClosedXML) (MIT licence), in the Infrastructure project. The Web app has two links that download a file through the Web app, so the browser never needs the API token: `/reports/export` and `/employees/import/template`.

The report workbook has a header row with a filter on every sheet, dates and times as Excel values with a number format, and hours as `[h]:mm`. Times are in the time zone of the API. A record that is still open has no clock-out time and no hours.

The import reads the first sheet. The first row must have the columns `Full name`, `Department` and `Email`, in any order, and other columns are ignored. Rows with nothing in the three columns are skipped. A file can have at most 500 employees and be at most 1 MB. Each row follows the same rules as the Add employee form. An email that exists already, or that an earlier valid row of the same file uses, is an error on that row. Nothing is updated: the import only adds employees. The size limit is on the uploaded file, so a workbook that is small but expands to a lot of data when it is opened is not stopped.

## Signing in

Every page needs a signed-in user. The API owns the users and roles, in the same database as the employees. In Development it creates two demo accounts on startup: `manager@attendance.local` (role Manager) and `employee@attendance.local` (role Employee). Both use the password in `SeedUsers:Password` in `src/FreeCodeSpot.Attendance.Api/appsettings.Development.json`. That file is for development only.

A user can be linked to one employee record. The link is an `employee_id` claim on the user, so it travels in the token. The seeder links the demo manager to Regie Baquero and the demo employee to Anna Cruz, through `SeedUsers:ManagerRosterEmail` and `SeedUsers:EmployeeRosterEmail`. There is no screen for linking users yet.

Only managers can open the Employees and Clock in and out pages or call `/api/employees` and `/api/attendance`. A linked user can open My attendance and call `/api/attendance/me`, which only ever uses their own employee. A user with no link gets 403 there.

## Endpoints

| Method | Route | Returns |
|---|---|---|
| POST | `/auth/login` | 200 with a bearer token, or 401; open to everyone |
| GET | `/auth/me` | the signed-in email, roles and linked employee id (or null), or 401 |
| GET | `/api/employees` | active employees ordered by name; `?includeInactive=true` adds inactive ones |
| GET | `/api/employees/{id}` | one employee (active or not), or 404 |
| POST | `/api/employees` | 201 with the new employee, or 400 with field errors |
| PUT | `/api/employees/{id}` | 200 with the updated employee, 400, or 404 |
| POST | `/api/employees/{id}/deactivate` | 204, or 404 |
| POST | `/api/employees/{id}/activate` | 204, or 404 |
| GET | `/api/attendance` | the active employees with the time they clocked in, or null when they are not clocked in |
| POST | `/api/attendance/{employeeId}/clock-in` | 200 with the open record, 409 if already clocked in or inactive, or 404 |
| POST | `/api/attendance/{employeeId}/clock-out` | 200 with the closed record, 409 if not clocked in, or 404 |
| GET | `/api/attendance/me` | the signed-in user's own status and latest five records, or 403 without a linked employee |
| POST | `/api/attendance/me/clock-in` | like clock-in, for the linked employee only |
| POST | `/api/attendance/me/clock-out` | like clock-out, for the linked employee only |
| GET | `/api/attendance/timesheet` | records and hours worked per employee and day between `from` and `to` (default: the last 7 days), for one `employeeId` or everyone; 400 for a bad range, 404 for an unknown employee; managers only |
| GET | `/api/attendance/me/timesheet` | the same, for the linked employee only |
| GET | `/api/reports/export` | the same report as an .xlsx file with the sheets Summary, Late arrivals, Absences and Records; same `from` and `to`, same 400; managers only |
| GET | `/api/employees/import/template` | an .xlsx file with the three column headers; managers only |
| POST | `/api/employees/import/preview` | multipart upload, field `file`: checks every row of an .xlsx file and saves nothing; 200 with a report of each row, or 400 for a file that cannot be used; managers only |
| POST | `/api/employees/import` | the same upload, saves the rows that are fine and skips the others; managers only |
| GET | `/api/reports` | a summary per employee, the late arrivals and the absences between `from` and `to` (default: the current month up to today); 400 for a bad range; managers only |

Every `/api/employees` (including the import routes), `/api/attendance` and `/api/reports` route needs a Manager token: 401 without one, 403 for an Employee. The `/me` routes need a token with an `employee_id` claim instead. Times are stored and returned in UTC.

## Articles

| Tag | Article |
|---|---|
| `article-001` | Project setup: solution structure, employee API, and the Blazor Employees page |
| `article-002` | SQL Server with EF Core: DbContext, first migration, seeded roster |
| `article-003` | Employee management: add, edit, deactivate and reactivate, validation, async repository |
| `article-004` | Authentication and roles: ASP.NET Core Identity, bearer-token login, Manager and Employee roles, protected pages and API |
| `article-005` | Clock in and clock out: attendance records, the clocking rules, and a manager page |
| `article-006` | Employees clock themselves in: linking a user to an employee, the My attendance page and routes |
| `article-007` | Attendance history and timesheets: date ranges, hours worked, LINQ projections |
| `article-008` | Reports: late arrivals, absences and a summary per employee, with the rules in appsettings.json |
| `article-009` | Excel export and import: the report as an .xlsx file, and new employees from an .xlsx file with a row-by-row preview |
