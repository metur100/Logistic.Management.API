# LogistiTrack API

ASP.NET Core 8 + EF Core + SQL Server API for the LogistiTrack transport
operations platform.

## What it does

- **Company isolation.** Every vehicle, cargo item, transport, incident and
  support ticket belongs to exactly one company. Managers and drivers can only
  read and write their own company's data.
- **Three separate roles.**
  - `Admin` — platform owner. Sees company registry, aggregate activity and
    support tickets. Read-only for company operations. Can suspend / verify
    companies and block users, but cannot edit another company's transports,
    cargo or fleet.
  - `Manager` — company owner/dispatcher. Owns the company profile, fleet,
    cargo, transports, drivers, messages, incidents and support tickets.
  - `Driver` — company driver. Sees only their own transports, conversations,
    incidents and tickets. Can start a route, advance status, submit POD/CMR.
- **Company self-registration** with the first manager account.
- **Driver invitations** by email plus a shareable 7-day link. Drivers choose
  their own password. No manager ever needs to know it.
- **Automatic route measurement** through OpenStreetMap/Nominatim/OSRM, with a
  distance + duration fallback when the routing service is unavailable.
- **Google Maps driver navigation** (`Start route` opens driving directions and
  records the departure).
- **SMTP settings stored in the database**, managed from the admin panel.
- **JWT signing key generated into the database on first start** — never stored
  in source control. Suspended companies and blocked users are rejected on every
  request, not only at login.

## Local run

```bash
dotnet restore LogisticsAPI.csproj
dotnet run --urls http://localhost:5099
```

Swagger: `http://localhost:5099/swagger`

The API runs EF migrations and the optional seeder on startup.

## Seeding

The seeder is idempotent and **never commits credentials**. Supply these
environment variables once on the machine that creates the initial data:

| Variable | Purpose |
| --- | --- |
| `SEED_ADMIN_PASSWORD` | Creates the platform admin when no admin exists |
| `SEED_ADMIN_USERNAME` | Admin username (default `admin`) |
| `SEED_ADMIN_EMAIL` | Admin email |
| `SEED_ADMIN_NAME` | Admin display name |
| `SEED_DEMO=true` | Creates the demo company, drivers, fleet and transports |
| `SEED_MANAGER_PASSWORD` | Demo manager password |
| `SEED_DRIVER1_PASSWORD` | Demo driver 1 password |
| `SEED_DRIVER2_PASSWORD` | Demo driver 2 password |
| `SMTP_USER` / `SMTP_PASSWORD` | Seeds the first SMTP settings row |
| `SMTP_FROM_NAME` | Sender display name (default `LogistiTrack`) |

After the first run these values live in the database; the environment variables
are not needed again.

## Database reset

`db-scripts/reset.sql` drops the LogistiTrack tables. The API recreates the
schema on the next start. This is destructive — use it only when starting over.

## Main endpoints

| Area | Endpoints |
| --- | --- |
| Auth | `POST /api/auth/login`, `GET /api/auth/me`, `POST /api/auth/change-password` |
| Public | `POST /api/companies/register`, `GET /api/companies/invite/{token}`, `POST /api/companies/invite/{token}/accept` |
| Company | `GET/PUT /api/company`, `GET /api/company/dashboard`, `/api/company/drivers...` |
| Transports | `/api/trips`, `/api/trips/{id}`, `/api/trips/{id}/status`, `/api/trips/{id}/start`, `/api/trips/{id}/pod`, `/api/trips/{id}/cmr`, `/api/trips/my-*`, `/api/trips/calendar` |
| Cargo / fleet | `/api/cargo`, `/api/vehicles` (manager only) |
| Team | `/api/company/drivers`, invite / resend / block / unblock |
| Messaging | `/api/messages/threads`, `/api/messages?withUserId=`, `/api/messages/mark-read` |
| Incidents | `/api/incidents`, `/api/incidents/{id}/resolve` |
| Support | `/api/support-tickets` |
| Routing | `GET /api/routing/distance?origin=...&destination=...` |
| Admin | `/api/admin/overview`, `/api/admin/companies`, `/api/admin/users`, `/api/admin/settings/smtp`, `/api/admin/settings/smtp/test` |

## Tests

`tests/api-smoke.ps1` (repository root) exercises registration, role
boundaries, cross-company isolation, invitations, status transitions, route
measurement, admin analytics and SMTP. Set `TEST_ADMIN_PASS`,
`TEST_MANAGER_PASS`, `TEST_DRIVER1_PASS`, `TEST_DRIVER2_PASS` before running.
