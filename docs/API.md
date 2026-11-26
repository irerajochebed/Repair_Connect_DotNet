# RepairConnect API integration

## Run and configure

Use SQL Server and the connection string described in README. Keep database passwords and initial-administrator credentials in local User Secrets or deployment secrets.

```powershell
dotnet restore
dotnet tool restore
dotnet ef database update -- --environment Development
dotnet run --launch-profile http
```

The original `20261122000920_InitialCreate` migration is preserved. `20261123120220_IntegrateAdminIdentity` updates EF metadata/history without recreating tables or changing existing rows. Never replace the initial migration on an already-created database.

Development startup creates an administrator only when `SeedAdmin:Email` and `SeedAdmin:Password` are explicitly configured. It never inserts service categories, sample people, repair requests, or bookings. Existing accounts are not reset on restart. Administrators create the service catalog from My workspace → Service categories.

Configure the initial admin using Manage User Secrets or the CLI, with your own values:
```powershell
dotnet user-secrets set "SeedAdmin:DisplayName" "Your name"
dotnet user-secrets set "SeedAdmin:Email" "your-admin@example.com"
dotnet user-secrets set "SeedAdmin:Password" "<your chosen password>"
```

The local administrator is already configured through User Secrets, outside Git. To view only that local password in your own terminal:
```powershell
dotnet user-secrets list | Select-String '^SeedAdmin:Password = '
```
Do not paste the result into chat, source files or commits. Changing the seed setting does not change the password of an account already created.

## Session and request verification

Authentication uses the protected, HTTP-only `RepairConnect.Auth` cookie. Client code sends same-origin credentials and never stores a bearer token or account record in local storage. Cookie validation rechecks active status and roles on every authenticated request; deactivation or a role change invalidates an existing session.

Before each POST/PUT/DELETE, fetch `GET /api/auth/csrf` and send its `requestToken` in `X-CSRF-TOKEN`, using the same cookie session. Renew after login because the identity has changed. `api.js` handles this.

API errors return JSON. Unauthenticated admin API requests return 401; authenticated non-admins return 403. Unsafe requests without a valid CSRF token return 400. A cookie session is required to view the Dashboard page.

## Endpoints

| Method | Endpoint | Access |
| --- | --- | --- |
| GET | `/api/auth/csrf` | Anonymous or signed-in |
| POST | `/api/auth/login` | Anonymous; JSON email, password, rememberMe |
| POST | `/api/auth/register` | Anonymous; creates and signs in a Client account |
| POST | `/api/auth/logout` | Signed-in |
| GET | `/api/auth/me` | Signed-in |
| GET | `/api/admin/dashboard` | Admin |
| GET | `/api/admin/users?search=&role=&status=&page=1&pageSize=10` | Admin |
| POST | `/api/admin/users` | Admin; creates a normal Client without switching the current session |
| PUT | `/api/admin/users/{id}/status` | Admin; JSON status Active or Inactive |
| DELETE | `/api/admin/users/{id}` | Admin; deactivates, preserves records |
| GET | `/api/admin/technicians?status=Pending&search=&page=1&pageSize=10` | Admin |
| GET | `/api/admin/technicians/{id}` | Admin |
| PUT | `/api/admin/technicians/{id}/approve` | Admin; documents required |
| PUT | `/api/admin/technicians/{id}/reject` | Admin; JSON reason |
| GET | `/api/technician-application` | Signed-in Client; own application or 404 if none |
| POST | `/api/technician-application` | Signed-in Client; multipart application and supporting document |
| GET | `/api/technician-application/documents/{id}` | Document owner or administrator; private attachment download |
| GET | `/api/categories` | Public; active categories ordered by name |
| GET | `/api/admin/categories?search=&isActive=&page=1&pageSize=10` | Admin; all categories with optional name/description search and active filter |
| GET | `/api/admin/categories/{id}` | Admin; category details |
| POST | `/api/admin/categories` | Admin; JSON name, description, isActive |
| PUT | `/api/admin/categories/{id}` | Admin; JSON name, description, isActive |
| DELETE | `/api/admin/categories/{id}` | Admin; deletes an unused category; returns 409 if linked to a request or technician |

JSON uses camelCase and named enum values. Paged responses contain items, pageIndex, totalPages, totalCount, hasPreviousPage and hasNextPage. Page size is limited to 50.

Registration and administrator user creation accept displayName, email, optional phoneNumber, and password. Display names are trimmed and required (up to 100 characters); email is required and unique after trimming and case normalization (up to 256 characters); phone is optional (up to 20 characters); passwords require 8–128 characters. Both paths always create Client accounts. Role, status, profile, and approval fields supplied by a caller cannot grant permissions. Administrator access is provisioned only through trusted database/bootstrap configuration.

Registration returns 201 with the same safe account fields as login and a session cookie. Already signed-in callers receive 409 rather than switching accounts. Duplicate email returns 409; missing or invalid fields return 400. Every write still requires CSRF verification. Passwords are hashed. No sample accounts, categories, or documents are created.

`UserRoles` is the flags enum: None = 0, Client = 1, Technician = 2, Admin = 4. A new user has Client (1). Applying creates a Pending/Offline technician profile on that same user ID and leaves Client unchanged. Approval adds Technician with a bitwise OR, producing Client | Technician (3); rejection removes Technician and preserves Client. Existing cookies are revoked when the stored roles change, so the user signs in again for the new permissions.

The multipart application fields are categoryId (one existing active category), specialization (required, up to 100 characters), yearsOfExperience (0–60), optional bio (up to 2,000), optional address (up to 300), and document (one PDF/PNG/JPEG up to 5 MB, checked by content as well as file metadata). New submission returns 201; a rejected application can be resubmitted with 200. Pending or approved duplicates return 409. Application/profile/document changes are saved together and failed submissions clean up their new file. Configure `TechnicianDocuments:StoragePath` for private storage outside wwwroot; the default is App_Data/technician-documents and is excluded from Git. Downloads require the owner or an administrator and are private attachments.

Category responses contain id, name, description and isActive. Names are trimmed, required, unique without case differences, and limited to 100 characters. Descriptions are optional and limited to 1,000 characters. Set isActive to false to hide a category from the public catalog while keeping existing references. Referenced categories cannot be deleted; deactivate them instead. The homepage dropdown and service tiles read active records from the same database and reflect saved changes on the next page load. There are no fallback category names or automatic category seeds.

Swagger is available under `/swagger` in Development.

## Verification

```powershell
dotnet test RepairConnect.Tests/RepairConnect.Tests.csproj
```

Tests use an isolated in-memory SQLite database with real authentication cookies and CSRF tokens. SQL Server-specific checks/default syntax are adapted only in the test context. No local accounts or jobs are created by those tests.

Coverage includes correct/incorrect/inactive login, me/logout, Client-only registration and administrator-created users, anonymous and role-restricted access, CSRF rejection, account search/pagination, duplicate email, validation/self-protection, account deactivation and existing-cookie revocation, changed-role revocation, and Razor routing.

Technician tests cover the same-account application/review/approval flow, Client | Technician enum permissions, rejection and resubmission, protected owner/administrator downloads, file validation and size limits, and database/private-file rollback. Test uploads use unique temporary storage that is removed after the tests.

Category tests cover create/read/update/delete, validation and duplicate names, permissions and CSRF, search/status/pagination, protection of categories linked to requests or technicians, no automatic category seeding, and homepage dropdown/tile visibility after rename, deactivation, and deletion.

Local SQL Server smoke checks also verify login/logout, current user, admin list/filter/metrics and CSRF handling against the configured account without creating test records.

Category SQL Server checks verify the full CRUD cycle, duplicate prevention, filtering, and public/homepage visibility. Temporary verification categories are deleted afterward; none are retained in the application catalog.

Local account-flow checks also verify sign-up as Client, a Pending application that retains Client, administrator document review, and approval granting Client | Technician on the same ID. Temporary verification users, application rows, documents, categories, and credentials are removed afterward.
