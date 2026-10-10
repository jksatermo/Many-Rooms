# Milestone 1 verification

The web vertical slice was exercised against a real local PostgreSQL 18 container. It does not use a browser-only or in-memory database.

| Check                                                                      | Result                                                                    |
| -------------------------------------------------------------------------- | ------------------------------------------------------------------------- |
| .NET restore, formatting verification, Release solution build, API publish | Passed, build has zero warnings/errors                                    |
| Application tests                                                          | 18 passed                                                                 |
| PostgreSQL API/integration tests                                           | 24 passed, none skipped                                                   |
| Web ESLint, TypeScript, formatting, production build                       | Passed                                                                    |
| Web Vitest/Testing Library                                                 | 18 passed                                                                 |
| Playwright with real API/PostgreSQL                                        | 3 passed                                                                  |
| Mobile ESLint, TypeScript, formatting                                      | Passed                                                                    |
| Mobile API adapter tests                                                   | 3 passed                                                                  |
| Expo Android JavaScript/Hermes export                                      | Passed                                                                    |
| Expo Metro startup                                                         | Passed; HTTP status reports the packager running                          |
| Expo bundled SDK dependency compatibility check                            | Passed in offline mode                                                    |
| Docker Compose configuration and PostgreSQL health                         | Passed                                                                    |
| Clean database migration and demo seed                                     | Passed after recreating the local volume and verifying zero public tables |
| OpenAPI document                                                           | Served successfully, 7 paths                                              |
| GitHub Actions static validation                                           | Passed with actionlint                                                    |

Gitleaks scanned the staged repository with no detected leaks. The generated local `.env`, dependencies, and build artifacts are ignored by Git.

The integration tests cover accepted/declined responses, changing a response before the event, stale versions, truly concurrent requests, closed events, malformed requests, organization membership, assignment ownership, cross-organization foreign keys, seed idempotency, and production demo restrictions. Each API test run applies migrations to a fresh isolated schema in a dedicated test database.

The browser checks exercise landing → explicit demo entry → dashboard → event → Accept → refresh → Decline → refresh. They also cover keyboard entry, narrow-screen overflow at 390/320 pixels, and automated WCAG A/AA checks on the primary screens. Automated accessibility checks do not replace a full assistive-technology review. Screenshots are captured from the running product.

## Environment limits and remaining work

- Mobile was not run on a physical Android/iOS device or simulator. An exported JavaScript bundle is not a signed native application.
- Expo’s online documentation/metadata hosts were unavailable in this environment. Dependency compatibility was checked against the installed SDK’s version map. Metro used its fallback React Native DevTools because the environment’s default cache location was read-only.
- The mobile dependency audit has unresolved upstream findings documented in [dependencies.md](dependencies.md). It is not reported as passing.
- The shared demo identity is deliberately development-only. Production identity, invitations, leader permissions, SMS delivery, scheduling-conflict detection, and mobile responses remain roadmap work.
- Local database reset commands remove fictional demo data. They are not a production migration procedure.

The root README lists the commands used for repeatable validation. CI repeats the build/test/browser workflow on pull requests and pushes to `main`; it does not deploy or merge changes.
