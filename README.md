# FreeCodeSpot Attendance

Source code for the FreeCodeSpot article series that builds an **Employee Attendance System** with .NET 10, ASP.NET Core and Blazor.

Each published article corresponds to an immutable Git tag `article-NNN`. Check out the tag to see the code exactly as the article describes it.

## What is here

An ASP.NET Core minimal API that manages an employee roster in SQL Server, and a Blazor Web App to add, edit, deactivate and reactivate employees. Employees are never deleted.

```
src/FreeCodeSpot.Attendance.Domain           Employee record (with IsActive)
src/FreeCodeSpot.Attendance.Application      EmployeeService (the roster rules), IEmployeeRepository, EmployeeDetails, EmployeeResult
src/FreeCodeSpot.Attendance.Infrastructure   AttendanceDbContext, migrations, EfEmployeeRepository, AddInfrastructure()
src/FreeCodeSpot.Attendance.Api              minimal API for the roster, see Endpoints below
src/FreeCodeSpot.Attendance.Web              Blazor Web App (interactive server), Employees page, Add and Edit pages
tests/FreeCodeSpot.Attendance.UnitTests      xUnit tests for EmployeeService
tests/FreeCodeSpot.Attendance.IntegrationTests   API and repository tests against a SQL Server test database
```

The roster is stored in SQL Server through Entity Framework Core. The first migration creates the `Employees` table and seeds five employees. The second adds the `IsActive` column, with existing employees set to active.

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

## Endpoints

| Method | Route | Returns |
|---|---|---|
| GET | `/api/employees` | active employees ordered by name; `?includeInactive=true` adds inactive ones |
| GET | `/api/employees/{id}` | one employee (active or not), or 404 |
| POST | `/api/employees` | 201 with the new employee, or 400 with field errors |
| PUT | `/api/employees/{id}` | 200 with the updated employee, 400, or 404 |
| POST | `/api/employees/{id}/deactivate` | 204, or 404 |
| POST | `/api/employees/{id}/activate` | 204, or 404 |

## Articles

| Tag | Article |
|---|---|
| `article-001` | Project setup: solution structure, employee API, and the Blazor Employees page |
| `article-002` | SQL Server with EF Core: DbContext, first migration, seeded roster |
| `article-003` | Employee management: add, edit, deactivate and reactivate, validation, async repository |
