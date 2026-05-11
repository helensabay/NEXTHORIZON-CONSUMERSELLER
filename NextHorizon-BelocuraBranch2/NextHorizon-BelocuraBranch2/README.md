# NextHorizon

ASP.NET Core MVC app with pinned SDK/tool/library versions for reproducible setup across collaborators.

## Pinned Versions

- .NET SDK: `10.0.103` (see `global.json`, patch roll-forward only)
- Target framework: `net10.0`
- Tools (see `dotnet-tools.json`):
  - `microsoft.web.librarymanager.cli@3.0.71`
  - `dotnet-ef@10.0.3`
- EF Core packages (see `NextHorizon.csproj` / `packages.lock.json`):
  - `Microsoft.EntityFrameworkCore.SqlServer@10.0.3`
  - `Microsoft.EntityFrameworkCore.Design@10.0.3`
- Frontend libraries (see `NextHorizon/libman.json`):
  - `jquery@3.7.1`
  - `bootstrap@5.3.3`
  - `jquery-validation@1.21.0`
  - `jquery-validation-unobtrusive@4.0.0`
- NuGet lock file: `NextHorizon/packages.lock.json`

## Prerequisites

1. Install .NET SDK `10.0.103` (or latest `10.0.10x` patch).
2. Install SQL Server (LocalDB/Express/Developer) and ensure it is running.
3. Optional (recommended): install `sqlcmd` for running `.sql` scripts from CMD.
4. Confirm SDK:

   ```cmd
   dotnet --list-sdks
   ```

## Step-by-Step Setup

Run from repository root (`C:\dev\NextHorizon`):

1. Restore local tools (LibMan is pinned via manifest):

   ```cmd
   dotnet tool restore
   ```

2. Restore frontend static libraries from `libman.json`:

   ```cmd
   cd /d .\NextHorizon
   dotnet tool run libman restore
   cd /d ..
   ```

3. Restore NuGet packages using lock file:

   ```cmd
   dotnet restore .\NextHorizon\NextHorizon.csproj --locked-mode
   ```

4. Build:

   ```cmd
   dotnet build .\NextHorizon.slnx -c Debug --no-restore
   ```

5. Apply EF migration to create/update database:

   ```cmd
   dotnet tool run dotnet-ef database update --project .\NextHorizon\NextHorizon.csproj --startup-project .\NextHorizon\NextHorizon.csproj
   ```

6. Create/update stored procedure in SQL Server:

   ```cmd
   sqlcmd -S "(localdb)\MSSQLLocalDB" -d "NextHorizonDb" -i ".\NextHorizon\Database\StoredProcedures\sp_GetLatestCustomers.sql"
   ```

7. Run with HTTPS:

   ```cmd
   dotnet run --project .\NextHorizon\NextHorizon.csproj --launch-profile https
   ```

8. Run with hot reload (recommended for development):

   ```cmd
   dotnet watch --project .\NextHorizon\NextHorizon.csproj run --launch-profile https
   ```

9. Open:
   - `https://localhost:7172`

## EF + Stored Procedure Notes

- `AppDbContext` is configured for SQL Server via `ConnectionStrings:DefaultConnection` in:
  - `NextHorizon/appsettings.json`
  - `NextHorizon/appsettings.Development.json`
- Sample SP integration service:
  - `ICustomerStoredProcedureService.GetLatestCustomersAsync(...)`
  - Executes: `EXEC dbo.sp_GetLatestCustomers @Top={top}`
- SP script path:
  - `NextHorizon/Database/StoredProcedures/sp_GetLatestCustomers.sql`

## Collaborator Consistency Rules

1. Keep `global.json`, `dotnet-tools.json`, `NextHorizon/libman.json`, `NextHorizon/packages.lock.json`, and `NextHorizon/Data/Migrations/*` committed.
2. Use `--locked-mode` for normal restores to avoid accidental version drift.
3. If intentionally updating dependencies, regenerate lock artifacts and commit them in the same PR.
4. When schema changes, add a migration in CMD:

   ```cmd
   dotnet tool run dotnet-ef migrations add <MigrationName> --project .\NextHorizon\NextHorizon.csproj --startup-project .\NextHorizon\NextHorizon.csproj --output-dir Data\Migrations
   ```
