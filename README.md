# RepairConnect

RepairConnect is a group project for connecting clients who need repair services with technicians who can help. A client submits a repair request, a technician can accept and complete the booking, and the client can review the completed work.

## Contents

- [Project status](#project-status)
- [Quick start](#quick-start)
- [Rider, Visual Studio, and SSMS](#rider-visual-studio-and-ssms)
- [Database design](#database-design)
- [Team workflow](#team-workflow)
- [Deploy to the shared test environment](#deploy-to-the-shared-test-environment)
- [Troubleshooting](#troubleshooting)

## Project status

| Area | Status |
| --- | --- |
| Web app | ASP.NET Core Razor Pages targeting .NET 10, with a Bootstrap 5 frontend across four main pages: Home, Find Help, Account, My Workspace |
| Database schema | Eight C# models, a SQL Server database context, and the `InitialCreate` migration |
| User features | Client sign-up and sign-in/logout, same-account technician applications with private supporting documents, administrator review and enum role grants, user status management, and full service-category management. Repair submissions and bookings are not implemented yet. |
| Stored procedures | Planned for backend operations if the team adopts the stored-procedure approach |
| Deployment | This README describes the team workflow. The repository has no deployment pipeline yet. |

## Quick start

### Connected frontend

Complete the database setup below, then run `dotnet run --launch-profile http` and open [http://localhost:5202](http://localhost:5202). Account and admin screens use the real SQL Server APIs. The browser-only demo records and sample technician profiles have been removed.

See [the frontend guide](docs/FRONTEND.md) for screen routes and [the API guide](docs/API.md) for endpoints, initial administrator setup, and tests.

### 1. Install the prerequisites and open the project

- A [.NET SDK that supports .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- A running Microsoft SQL Server instance for your local development database
- Rider and Visual Studio with .NET 10 support are both suitable. SSMS is optional for inspecting the database and running SQL scripts.

Pull the project from your team’s Git repository. Open [RepairConnect.slnx](RepairConnect.slnx) in your IDE, then open its **Terminal** and work from the folder containing [RepairConnect.csproj](RepairConnect.csproj). Run the commands below in PowerShell.

```powershell
dotnet --list-sdks
dotnet restore
dotnet tool restore
```

`dotnet restore` downloads the project’s NuGet dependencies. `dotnet tool restore` installs the repository-pinned EF Core command-line tool from [the tool manifest](.config/dotnet-tools.json).

### 2. Configure your local database connection

Use the SQL Server name you successfully connect to in SSMS. This example uses the default instance on your PC and Windows Authentication.

```powershell
dotnet user-secrets set "ConnectionStrings:RepairConnect" "Server=localhost;Database=RepairConnect;Integrated Security=True;TrustServerCertificate=True"
```

For a named instance, replace `Server=localhost` with your actual instance, for example `Server=localhost\SQLEXPRESS`. A teammate using SQL Server Express may have a different server name from yours. `TrustServerCertificate=True` in this example is for local development.

The application reads `ConnectionStrings:RepairConnect` in [Program.cs](Program.cs). User Secrets stores the value on your machine, outside the repository. Each teammate sets it once per machine and updates it when their SQL Server details change.

### 3. Create or update the tables

```powershell
dotnet ef database update -- --environment Development
```

The arguments after `--` select the `Development` environment so the app loads the connection string from User Secrets. EF Core applies pending migrations to that database. On a fresh setup, it creates `RepairConnect` if your account has permission and then creates the eight application tables. If your account can update the schema but cannot create databases, ask a SQL Server administrator to create an empty `RepairConnect` database and grant the needed permissions.

Rider’s Database panel and SSMS are optional for this step. The command sends the SQL to SQL Server.

### 4. Build, run, and verify

```powershell
dotnet build
dotnet run --launch-profile http
```

Open [http://localhost:5202](http://localhost:5202). If the port differs on your machine, use the URL printed by `dotnet run`. Stop the app with **Ctrl+C**.

To verify the schema, refresh your connection in Rider or SSMS. Expand **RepairConnect → Tables** and look for the eight tables below plus `__EFMigrationsHistory`, which records the migrations that have already run. The homepage loads service categories from SQL Server and shows an unavailable state if that connection fails; authentication and administration also require the database.

## Rider, Visual Studio, and SSMS

| Tool | How to use it |
| --- | --- |
| **JetBrains Rider** | Open the solution and run the Quick start commands in the Terminal. To inspect SQL Server, open **View → Tool Windows → Database**, add a **Microsoft SQL Server** data source, and test the connection. Use **Windows credentials** when your server account uses Windows Authentication. |
| **Visual Studio** | Open the same solution and run the same commands in the Terminal. The commands in this README use the .NET CLI in PowerShell. |
| **SQL Server Management Studio** | Connect to your SQL Server instance and use **Object Explorer → Databases → RepairConnect → Tables** to inspect the schema. Use **Refresh** after applying a migration. |

The IDE’s data source lets you browse SQL Server. The app uses the connection string from step 2. Set up each connection with the server and database you intend to use.

### Run the initial schema as SQL in SSMS

Use this option if the team wants to review or run the schema as a SQL script. **One teammate** generates the file from the C# migration:

```powershell
dotnet tool restore
dotnet ef migrations script 0 InitialCreate --idempotent --output InitialCreate.sql
```

1. Share the generated `InitialCreate.sql` with your teammates.
2. In SSMS, connect to the intended SQL Server and create an empty database named `RepairConnect` if it does not exist.
3. Open `InitialCreate.sql`, select **RepairConnect** in the query window’s database dropdown, and click **Execute**.
4. Refresh **Tables** and check the migration history. The generated script creates the schema inside the selected database.

A teammate who receives the SQL file can run it in SSMS without the EF command-line tool. They still configure the app’s connection string to run RepairConnect locally. Use the migration or its exported SQL for schema setup so the history table tracks what has been applied.

## Database design

[Database design PDF](output/pdf/RepairConnect_Database_Tables.pdf) · [SQL Server mappings](Data/RepairConnectDbContext.cs) · [Initial migration](Data/Migrations/20261122000920_InitialCreate.cs)

| Table | Purpose and key links |
| --- | --- |
| `Users` | Identity-backed `ApplicationUser` accounts with a unique normalized email and a role flags field |
| `TechnicianProfiles` | Technician details and verification. `UserId` is both the primary key and a reference to `Users.Id`. The reviewer is an optional user reference. |
| `TechnicianDocuments` | Uploaded CV or certificate references linked to a profile by `TechnicianUserId` |
| `ServiceCategories` | Repair service categories, with unique names |
| `TechnicianCategories` | Technician-to-category links, keyed by `TechnicianUserId` and `CategoryId` together |
| `RepairRequests` | The client’s repair issue, linked by `ClientUserId` and `CategoryId` |
| `Bookings` | Technician selections linked to a request by `RepairRequestId` and to a profile by `TechnicianUserId` |
| `Reviews` | One review per booking, with a rating from 1 to 5 |

One account can hold both the Client and Technician roles. Technicians’ IDs in the document, category, and booking tables reference `TechnicianProfiles.UserId`, which is their account ID. The database enforces at most one active booking per request through a unique index covering the `Pending`, `Accepted`, and `InProgress` statuses.

When the booking and review features are built, the backend must reject bookings where the technician is the client who submitted the request. It must also check that only the request owner can review a completed booking. These checks require the records and the signed-in user.

### Where to work in the project

| Path | Responsibility |
| --- | --- |
| [Pages/](Pages/) | Razor views and their C# page handlers |
| [wwwroot/](wwwroot/) | Static assets such as CSS, JavaScript, and images |
| [Models/](Models/) | C# database entities and enums |
| [Data/RepairConnectDbContext.cs](Data/RepairConnectDbContext.cs) | Table mappings, relationships, indexes, and constraints |
| [Data/Migrations/](Data/Migrations/) | Versioned database schema changes and the EF model snapshot |
| [Program.cs](Program.cs) | Registers Razor Pages and the SQL Server database context |
| [Properties/launchSettings.json](Properties/launchSettings.json) | Local development launch profiles |

If the team builds backend operations with stored procedures, keep their SQL definitions in version control along with the C# calls. A Razor Page handler can validate the request and call a procedure through the backend data access code. The current migration creates the tables and constraints those procedures would work with.

## Team workflow

Each teammate runs a local app with their own development database. The combined app uses a separate database in the team’s shared test environment. Git shares the C# code and migration files. Data stays in the database where it was created.

### After pulling changes

- Run `dotnet restore` when project dependencies change and `dotnet tool restore` when setting up or when the tool manifest changes.
- Run `dotnet ef database update -- --environment Development` when you pull a new migration.
- Run `dotnet build` and test your feature with local data before pushing.

### When your change affects the database

1. Start from the team’s latest `main` and check whether another teammate is changing the same tables.
2. Update the relevant entities in [Models/](Models/) and their mappings in [RepairConnectDbContext.cs](Data/RepairConnectDbContext.cs).
3. Give the migration a name that describes the change. For example, if you have just added a booking reference:

   ```powershell
   dotnet ef migrations add AddBookingReference --output-dir Data/Migrations
   dotnet ef database update -- --environment Development
   dotnet ef migrations has-pending-model-changes
   dotnet build
   ```

4. Review the generated migration, especially renames, deletions, and new required columns. Test it against your local database.
5. Commit the model changes, migration, and updated model snapshot together. For the first database setup, include the project configuration, [.NET tool manifest](.config/dotnet-tools.json), this README, and the linked database design PDF so teammates have the code and setup instructions in the same pull. Keep connection secrets, personal IDE configuration, and build output out of the commit.
6. Push through the team’s normal Git workflow. Teammates pull the migration and apply it to their own local databases.

## Deploy to the shared test environment

1. Select the revision of `main` the team wants to test. Use a host that supports ASP.NET Core on .NET 10 and provision a SQL Server database for that environment, for example `RepairConnect_Test`.
2. Set `ConnectionStrings__RepairConnect` in the hosting environment to the test database connection string using the host’s configuration or secrets settings.
3. Have one teammate or the deployment pipeline generate and review the SQL for that revision:

   ```powershell
   dotnet tool restore
   dotnet ef migrations script --idempotent --output RepairConnect.Deploy.sql
   ```

4. Apply that script to the **test database** with an account allowed to update its schema, then deploy the matching app revision. Coordinate the rollout if an older app version is still running and the migration changes columns it uses.
5. Open the deployed app and verify the database schema. As features are completed, smoke-test the client, technician, and admin flows using test accounts.

[Applying EF Core migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying) · [ASP.NET Core configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/)

## Troubleshooting

| Symptom | What to check |
| --- | --- |
| `dotnet` is unavailable or cannot target `net10.0` | Install a supported SDK, reopen the terminal, and run `dotnet --list-sdks`. |
| `dotnet ef` is unavailable | Run `dotnet tool restore` from the project folder and verify with `dotnet ef --version`. |
| Connection refused or timed out | Confirm the SQL Server service is running and the host, instance, and port match. If SSMS connects locally and Rider times out, check whether SQL Server TCP/IP is enabled and listening on the configured port. |
| Login failed | Verify the authentication method and that your account has access on that SQL Server instance. With Windows Authentication, the CLI runs as your current Windows user. |
| Certificate trust error on your local server | Compare the connection string with the local Quick start example. Use a trusted server certificate for the shared environment. |
| `The ConnectionString property has not been initialized.` | The build can succeed before EF Core tries to connect. From the project folder, set `ConnectionStrings:RepairConnect` with the User Secrets command in Quick start, then run `dotnet ef database update -- --environment Development`. Use the host configuration for deployment. |
| Migration cannot create the database | Ask the SQL Server administrator to create an empty database and grant the permissions needed to apply the schema. |
| Tables are missing in Rider or SSMS | Confirm you applied the migration, refresh the database tree, and check that the IDE and app point to the same instance and database. |
| EF reports pending model changes | Pull the latest changes first. If your model differs from the latest migration, add or regenerate the migration for your own schema change before applying it. |
