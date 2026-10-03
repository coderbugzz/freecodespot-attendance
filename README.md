# FreeCodeSpot Attendance

Source code for the FreeCodeSpot article series that builds an **Employee Attendance System** with .NET 10, ASP.NET Core and Blazor.

Each published article corresponds to an immutable Git tag `article-NNN`. Check out the tag to see the code exactly as the article describes it.

## What is here

An ASP.NET Core minimal API that manages an employee roster in SQL Server, and a Blazor Web App to add, edit, deactivate and reactivate employees. Employees are never deleted. Users sign in with an email and a password, and only managers can open the Employees pages or call the employee routes.

```
src/FreeCodeSpot.Attendance.Domain           Employee record (with IsActive)
src/FreeCodeSpot.Attendance.Application      EmployeeService (the roster rules), IEmployeeRepository, EmployeeDetails, EmployeeResult, Roles
src/FreeCodeSpot.Attendance.Infrastructure   AttendanceDbContext (Identity and employees), migrations, EfEmployeeRepository, IdentitySeeder, AddInfrastructure()
src/FreeCodeSpot.Attendance.Api              minimal API: bearer-token login and the protected roster routes, see Endpoints below
src/FreeCodeSpot.Attendance.Web              Blazor Web App (interactive server): login, role-aware navigation, Employees, Add and Edit pages
tests/FreeCodeSpot.Attendance.UnitTests      xUnit tests for EmployeeService
tests/FreeCodeSpot.Attendance.IntegrationTests   API and repository tests against a SQL Server test database
```

The roster is stored in SQL Server through Entity Framework Core. The first migration creates the `Employees` table and seeds five employees. The second adds the `IsActive` column, with existing employees set to active. The third adds the ASP.NET Core Identity tables for users and roles.

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

## Signing in

Every page needs a signed-in user. The API owns the users and roles, in the same database as the employees. In Development it creates two demo accounts on startup: `manager@attendance.local` (role Manager) and `employee@attendance.local` (role Employee). Both use the password in `SeedUsers:Password` in `src/FreeCodeSpot.Attendance.Api/appsettings.Development.json`. That file is for development only.

Only managers can open the Employees pages or call `/api/employees`. An employee who signs in sees the home page and an access-denied page if they try the roster.

## Endpoints

| Method | Route | Returns |
|---|---|---|
| POST | `/auth/login` | 200 with a bearer token, or 401; open to everyone |
| GET | `/auth/me` | the signed-in email and roles, or 401 |
| GET | `/api/employees` | active employees ordered by name; `?includeInactive=true` adds inactive ones |
| GET | `/api/employees/{id}` | one employee (active or not), or 404 |
| POST | `/api/employees` | 201 with the new employee, or 400 with field errors |
| PUT | `/api/employees/{id}` | 200 with the updated employee, 400, or 404 |
| POST | `/api/employees/{id}/deactivate` | 204, or 404 |
| POST | `/api/employees/{id}/activate` | 204, or 404 |

Every `/api/employees` route needs a Manager token: 401 without one, 403 for an Employee.

## Articles

| Tag | Article |
|---|---|
| `article-001` | Project setup: solution structure, employee API, and the Blazor Employees page |
| `article-002` | SQL Server with EF Core: DbContext, first migration, seeded roster |
| `article-003` | Employee management: add, edit, deactivate and reactivate, validation, async repository |
| `article-004` | Authentication and roles: ASP.NET Core Identity, bearer-token login, Manager and Employee roles, protected pages and API |
