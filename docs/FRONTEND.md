# RepairConnect frontend

The frontend stays in the existing .NET 10 Razor Pages project. It uses Bootstrap 5.3.3, HTML/CSS and plain JavaScript. Poppins, blue/navy colors, responsive cards, four-page navigation and the first-screen hero layout are retained.

## Main pages

| Route | Behavior |
| --- | --- |
| `/` | Home, real active service categories from SQL Server, compact service tiles |
| `/Explore` | Honest unavailable state until a public technician-directory API exists |
| `/Account` | Email/password sign-in and Client sign-up; successful sign-up signs in the new account |
| `/Dashboard` | Requires authentication; administrators manage users, technician approvals, and service categories; other roles see their account and available-service information |

The shared profile dialog uses `GET /api/auth/me` and opens only through My profile beside Account in the navbar. It is available on signed-in pages, with loading and retry states. My account navigation and the legacy Profile route open the workspace without opening a dialog. Privacy remains a shared dialog. `/Error` is a system screen, not a navigation item. Old page URLs redirect to the four main destinations.

## Connected user management

Every visitor signs up as a Client using the Sign up tab on `/Account`. `/Account?mode=register` opens that tab. Registration saves a real database account and signs it in. There is no public role selector. Administrator access is provisioned through trusted database/bootstrap configuration.

Administrators can read dashboard metrics, search/filter/page users, create normal Client accounts through Create user, change account status, deactivate accounts, and review/approve/reject technician applications. Creating another account does not change the administrator's current session. The signed-in administrator cannot deactivate their own account.

The desktop administration workspace uses a 288px sidebar fixed to the left edge for the full viewport height. Dashboard is the default menu item and exclusively contains the greeting and summary cards. Users, Technician approvals, and Service categories each display only their own management panel, filters, actions, and table. Backoffice title descriptions are omitted. The blue header and compact privacy footer stay in place; only the main content between them scrolls. On phones, the sidebar becomes a drawer and the page uses its normal scrolling layout. Navigation does not add separate pages.

Clients apply to become technicians from My workspace using their existing account. The form reads active service categories from the database and accepts their specialization, experience, optional bio/address, and a supporting PDF/PNG/JPEG up to 5 MB. Pending applications retain Client access. An administrator reviews the application and its protected document; approval adds the Technician enum flag while preserving Client. Rejected applicants keep Client access, see the reason, and can resubmit. One account and ID serve both roles. A role change invalidates an existing session; sign in again to receive the updated roles.

The UI uses `wwwroot/js/api.js` for same-origin requests with an HTTP-only authentication cookie. Every write obtains a current anti-forgery token. Passwords are cleared from form fields after submission and are never placed in browser storage or URLs. Failures, empty lists, loading, validation and session expiry have explicit UI states.

`wwwroot/js/admin.js` and `Pages/Shared/_AdminWorkspace.cshtml` contain the administration UI. `wwwroot/js/site.js` manages session, sign-in/logout, profile display, and shared presentation. There is no demo store or hardcoded user/technician fixture list.

## Service categories

My workspace → Service categories provides real list/search/filter/pagination, creation, editing, activation/deactivation, and deletion. Forms and deletion confirmation use compact dialogs. Only unused categories can be deleted; linked categories can be deactivated to preserve records.

The homepage service dropdown and tiles display the exact saved names of active categories from the database. Inactive and deleted categories are excluded. An empty catalog shows an honest empty state and disables the service dropdown. Startup never creates starter categories or restores deleted ones.

## Unfinished domain features

General profile editing, password recovery, repair submissions, bookings, reviews, and the public directory do not yet have backend APIs. Their UI does not simulate success or save fake records. The home repair entry explains that requests are unavailable.

Technician documents are stored outside the public web folder and downloaded through an authenticated owner-or-administrator endpoint. They are not committed to Git.

## Local setup

Follow README database setup and docs/API.md initial-admin instructions, then:

```powershell
dotnet run --launch-profile http
```

The configured local administrator and generated password are stored in local User Secrets, outside this repository. The password uses the `SeedAdmin:Password` key. This is development configuration, not a shared or production credential.

See docs/API.md for tests and supported endpoint contracts.
