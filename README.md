# Many Rooms

**Room for everyone.**

Many Rooms is a web and mobile platform in active development for churches and community organizations. It starts with a small, practical question: **who is serving, and have they confirmed?**

I’m building it to make volunteer coordination easier for the people organizing gatherings and the people giving their time. The goal is useful software shaped by real community needs, with space to grow into communication, reminders, and SMS responses.

## Current milestone

Milestone 3 adds leader scheduling and conflict resolution to the existing React web and Expo volunteer workflows. Leaders can manage membership and positions in existing teams, set event staffing needs, preview and create assignments, replace volunteers, cancel assignments, and review assignment history. A conflict center brings overlapping commitments, unavailable time, and declined invitations together.

Weekly templates generate bounded, independent event instances in the organization's timezone. They do not automatically assign volunteers. Web and mobile volunteers continue to accept/decline and manage unavailable ranges through the same PostgreSQL-backed API; cancelled assignments are visible and reassignment requires a fresh response from the new volunteer.

Production authentication, recurring availability, notifications, and SMS delivery remain roadmap work. The mobile workflow has automated native-component tests and bundle validation; it has not been validated on a native device or simulator.

**The demo is development-only, fictional, and shared. It is not production authentication.**

## What Milestone 3 demonstrates

- Shared assignment state and API contracts across web and native mobile clients.
- Organization-scoped relational relationships, forward migrations, and preservation of existing responses.
- Server-calculated staffing totals and deterministic replacement suggestions using time-range conflicts.
- Optimistic concurrency and transactional assignment history for leader and volunteer changes.
- Bounded, repeatable weekly generation with explicit timezone and daylight-saving rules.
- Responsive, keyboard-accessible leader workflows and honest native-validation limits.
- Application tests, real PostgreSQL integration tests, and browser workflow checks.

Milestone 2 established volunteer availability and conflict detection; Milestone 3 uses those rules to support concrete scheduling decisions. See [scheduling rules](docs/scheduling.md) and [verification evidence](docs/verification.md) for precise behavior and limitations.

## Screenshots

Captured from the working local demo, using fictional seed data:

![Many Rooms public landing page](docs/screenshots/many-rooms-landing.png)

![Volunteer dashboard with upcoming events and assignment statuses](docs/screenshots/many-rooms-dashboard.png)

Milestone 2 screens: [volunteer conflicts](docs/screenshots/many-rooms-volunteer-conflicts.png), [availability](docs/screenshots/many-rooms-availability.png), and [leader assignment overview](docs/screenshots/many-rooms-leader-schedule.png).

The [Expo assignment workflow](docs/screenshots/many-rooms-expo-browser-assignment.png) and [Expo availability](docs/screenshots/many-rooms-expo-browser-availability.png) screenshots show the **Expo browser preview**, using the same API. They are not native device captures. See also the [web event response screen](docs/screenshots/many-rooms-event.png) and [mobile-width web dashboard](docs/screenshots/many-rooms-dashboard-mobile.png). Browser validation saves additional evidence under `apps/web/test-results/` (ignored by Git), uploaded by GitHub Actions. Native screenshots will follow device validation.

## Architecture

One application and one database, with four small .NET projects and two independent clients:

```mermaid
flowchart LR
  Web[React web] --> API[ASP.NET Core API]
  Mobile[Expo mobile] --> API
  API --> Application[Application use cases and DTOs]
  Application --> Domain[Domain entities and response rules]
  API --> Infrastructure[EF Core, PostgreSQL, demo seed]
  Infrastructure --> Application
  Infrastructure --> Database[(PostgreSQL)]
```

- **Domain:** entities, assignment response rules, and time-range rules, independent of HTTP and EF Core.
- **Application:** responses, availability, conflict detection, leader scheduling, DTOs, and narrow persistence/SMS interfaces.
- **Infrastructure:** EF mappings, migrations, queries, transactional persistence, and demo data.
- **API:** ASP.NET authentication/authorization boundary, REST endpoints, validation, ProblemDetails, and OpenAPI.

This keeps business rules shared across web/mobile and ready for future SMS responses while avoiding microservices, message buses, and generic repository layers. Clients have separate npm lockfiles so Expo’s React Native compatibility requirements do not dictate web dependencies. See [time ranges and conflict policy](docs/scheduling.md) for the precise rules and timezone conventions.

```text
apps/web/                         React + TypeScript + Vite
apps/mobile/                      Expo + React Native + TypeScript
src/ManyRooms.Api/                 HTTP endpoints and demo authentication
src/ManyRooms.Application/         Use cases, DTOs, narrow interfaces
src/ManyRooms.Domain/              Entities and business rules
src/ManyRooms.Infrastructure/      EF Core, PostgreSQL, migrations, seed
tests/ManyRooms.Application.Tests/ Business behavior tests
tests/ManyRooms.Api.Tests/         PostgreSQL API/integration tests
scripts/                          Local environment and database helpers
docs/                             Architecture and security notes
ManyRooms.sln                     Visual Studio-compatible solution
compose.yml                       Local PostgreSQL
```

## Tech stack and prerequisites

- **.NET 10 LTS SDK**, pinned by `global.json`; Visual Studio with .NET 10 support or any editor/CLI.
- **Node.js 24 LTS**, npm, and Git.
- **Docker Engine/Desktop with Compose v2**, for supported PostgreSQL 18.
- Backend: ASP.NET Core, EF Core 10, Npgsql, OpenAPI/Swagger UI, xUnit.
- Web: React, TypeScript, Vite, Vitest/Testing Library, Playwright.
- Mobile: Expo SDK 57 with its compatible React/React Native packages.

No paid cloud account, SMS account, API key, or production database is needed for the local web demo. Use each lockfile with `npm ci`; use Expo’s compatibility tooling when upgrading mobile dependencies.

## Run the web demo locally

From the repository root:

```bash
npm ci
npm run setup
npm run db:up
dotnet restore ManyRooms.sln
npm run db:migrate
npm run db:seed
npm run dev:api
```

Keep the API terminal running. In a second terminal:

```bash
cd apps/web
npm ci
npm run dev
```

Open **http://localhost:5173**. Enter as fictional volunteer **Alex Morgan**, open an upcoming event, choose **Accept** or **Decline**, then refresh. Open availability to add, edit, or remove an unavailable range. Assignments with overlapping commitments or unavailable time display a schedule-conflict explanation from the server.

The separate, explicitly labeled **leader demo** uses fictional leader **Jamie Chen**. Open Teams to manage members and positions. Open an event to set staffing needs, inspect response statuses, preview assignments, and review history. Use Conflicts to find a replacement for a declined or conflicted assignment. Event planning supports one-time events and bounded weekly generation. Previews and suggestions are advisory; saving checks the current server state again.

The API listens at **http://localhost:5080**. Inspect it at **http://localhost:5080/swagger**; its OpenAPI document is `/openapi/v1.json`. Readiness is `/health/ready` and liveness is `/health`.

`npm run setup` creates an ignored `.env` with a randomly generated local database password and preserves an existing `.env`. The helpers load it and construct `ConnectionStrings__ManyRooms` without printing credentials. PostgreSQL binds to `127.0.0.1:55432`, avoiding common local 5432 conflicts. Override the host port in `.env` if needed. The API does not automatically apply migrations during ordinary startup.

The Vite client defaults to the local API. To change it, set `VITE_API_BASE_URL` in `apps/web/.env.local` using its example and restart Vite. Browser configuration is public: never put secrets in `VITE_*` or `EXPO_PUBLIC_*` variables.

### Database commands

```bash
npm run db:up       # Start PostgreSQL and wait for its health check
npm run db:migrate  # Restore local dotnet-ef and apply committed migrations
npm run db:seed     # Add fictional demo data; preserve existing responses
npm run db:down     # Stop PostgreSQL; retain the volume
```

**Upgrading a Milestone 1 or 2 database:** retain the Docker volume, run `npm run db:migrate`, then `npm run db:seed`. Forward migrations add availability and leader scheduling without rewriting earlier migrations. Existing assignments, responses, and event dates are preserved. New fictional scheduling examples are additive; rerunning the seed preserves user edits. See [migration details](docs/development.md#migrations).

To recreate **only this local demo database**, the following intentionally deletes its Docker volume and all local responses:

```bash
docker compose down --volumes
npm run db:up
npm run db:migrate
npm run db:seed
```

The seed uses fixed entity IDs and future event dates when each milestone dataset is first added. Rerunning it does not reset decisions or move existing events. When seeded events have passed, leaders can create new events; recreate only a disposable demo database if you want a fresh seeded walkthrough.

For Visual Studio, open `ManyRooms.sln`. Keep the connection string in user secrets or environment variables; do not put it in `appsettings.json`. The root helpers provide the same environment for CLI use. See [development details](docs/development.md) for migration creation and production build commands.

## Mobile

```bash
cd apps/mobile
npm ci
npm run start
```

Use an Expo development client/Expo Go matching the SDK or the supported platform tooling. For a physical device, copy `apps/mobile/.env.example` to `.env.local`, set `EXPO_PUBLIC_API_BASE_URL` to your computer’s reachable LAN address, and restart Expo. `localhost` on a phone means the phone itself.

To permit a trusted device on your development LAN, set `ASPNETCORE_URLS=http://0.0.0.0:5080` and `AllowedHosts=localhost;127.0.0.1;YOUR_COMPUTER_LAN_IP` in the ignored root `.env` and restart the API; your OS firewall must allow the port. Replace `YOUR_COMPUTER_LAN_IP` with the same IP used by the phone. Restore the default loopback binding afterward. This openly labeled demo identity must not be exposed as public production authentication.

```bash
npm run typecheck
npm run lint
npm run test
npm run build
```

The mobile build command exports the Android JavaScript/Hermes bundle. It is **not** a signed native APK/AAB or an iOS build. Native device validation and distribution signing require the relevant device/toolchain and your own Expo/Apple/Google configuration. Use [the native validation checklist](docs/mobile-validation.md) for the remaining device checks.

## Demo identity and authorization

`GET /api/demo/context` exposes the fictional volunteer identity only when the API is in `Development` and `Demo:Enabled` is true. Clients send `X-ManyRooms-Demo: true` for Alex. The web leader entry requests `?persona=leader` and sends `X-ManyRooms-Demo: leader` for Jamie. The server supports only these fixed fictional identities, not arbitrary user IDs. Both values are public demo controls, not credentials.

Every organization endpoint checks a real database membership. Responses and availability edits check ownership. Leader endpoints check the membership’s stored role; a client label or selected screen does not grant authority. Database composite foreign keys keep organization-scoped records attached to parents in the same organization. This is a foundation for tenant isolation; it does not make the shared demo mechanism a real login.

Production will replace the demo handler with a validated identity provider through ASP.NET authentication, map its subject to a user, and retain the membership/ownership checks. See [authentication and organization boundaries](docs/authentication.md).

## Tests and validation

From the root, with PostgreSQL running:

```bash
npm run format:check
npm run lint
dotnet format ManyRooms.sln --verify-no-changes
dotnet build ManyRooms.sln --configuration Release
npm run test:backend
npm run format:check --prefix apps/web
npm run lint --prefix apps/web
npm run typecheck --prefix apps/web
npm test --prefix apps/web
npm run build --prefix apps/web
npm run format:check --prefix apps/mobile
npm run lint --prefix apps/mobile
npm run typecheck --prefix apps/mobile
npm test --prefix apps/mobile
npm run build --prefix apps/mobile
npm run build:web --prefix apps/mobile
npm run check:dependencies --prefix apps/mobile
docker compose config --quiet
```

`npm run test:backend` uses a dedicated `manyrooms_test` database and passes `TEST_DATABASE_CONNECTION`. API integration tests use real PostgreSQL; application tests cover decisions, availability validation, interval boundaries, ownership, and stale/closed assignments. Integration tests also cover leader authorization, privacy, database constraints, and migration preservation. Do not point integration tests at an application or production database.

For the complete browser flow, keep API and web running:

```bash
cd apps/web
npx playwright install chromium
npm run test:e2e
```

CI runs formatting, builds, backend tests against PostgreSQL, web tests and browser persistence/accessibility checks, and mobile type checking/dependency/export validation. No release or deployment happens automatically. See [verified results and limits](docs/verification.md).

### Known dependency limitation

The current stable Expo SDK has upstream dependency advisories reported by `npm audit`; see [mobile dependency review](docs/dependencies.md). No forced major overrides or incompatible SDK downgrades are applied to hide them. Review patched compatible releases before native distribution. The web runtime dependency audit reports no known vulnerabilities at this milestone.

## SMS foundation

`ISmsProvider` and the development provider establish an extension point only. No paid provider is connected, no real SMS is sent, and the response workflow does not require SMS. Later, a verified incoming `ACCEPT` or `DECLINE` reply can resolve the sender and assignment, then call the same application use case after authentication, replay protection, and organization checks.

## Roadmap

1. Production authentication, invitations, and membership administration.
2. Native device validation, compatible dependency fixes, and distribution preparation.
3. Volunteer change notifications, reminder delivery, and clear communication after reassignment.
4. Recurring availability, event/template editing, and more flexible scheduling constraints.
5. Authenticated SMS Accept/Decline replies, calendar integrations, and focused team communication.

Keep future work focused on demonstrated needs and preserve a simple, welcoming experience.
