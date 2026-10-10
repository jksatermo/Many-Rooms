# Milestone 2 verification

Validated on October 10, 2026 against real local PostgreSQL 18. Tests do not substitute an in-memory database for the API persistence layer. Milestone 1 tests remain in the suites.

| Check                                                | Result                                                                                                                     |
| ---------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------- |
| .NET formatting, Release solution build, API publish | Passed; zero build warnings/errors                                                                                         |
| Application/domain tests                             | 45 passed                                                                                                                  |
| PostgreSQL API/integration tests                     | 52 passed, none skipped                                                                                                    |
| Web formatting, ESLint, TypeScript, production build | Passed                                                                                                                     |
| Web Vitest/Testing Library                           | 41 passed                                                                                                                  |
| Playwright against API/PostgreSQL                    | 5 passed                                                                                                                   |
| Mobile formatting, ESLint, TypeScript                | Passed                                                                                                                     |
| Mobile API/time adapter tests                        | 8 passed                                                                                                                   |
| React Native component workflow tests                | 10 passed                                                                                                                  |
| Android JavaScript/Hermes export                     | Passed; 1,251 modules, 2.7 MB `.hbc`                                                                                       |
| Expo browser preview export                          | Passed; 788 modules                                                                                                        |
| Expo SDK dependency compatibility                    | Passed against installed SDK 57 offline matrix                                                                             |
| Docker Compose configuration/PostgreSQL health       | Passed                                                                                                                     |
| Existing Milestone 1 database forward migration      | Passed; all 8 original assignments and 7 response-history rows unchanged, including after additive seed                    |
| Separate clean PostgreSQL database                   | Both migrations applied; seed created 6 events, 12 assignments, 2 unavailable ranges across fictional communities          |
| OpenAPI                                              | Served successfully; 13 paths                                                                                              |
| Workflow static validation                           | Passed with actionlint                                                                                                     |
| Secret scan                                          | Gitleaks found no leaks in tracked/nonignored source files; local credential `.env` files and build outputs remain ignored |
| Web production dependency audit                      | Zero known vulnerabilities                                                                                                 |
| Mobile dependency audit                              | Unresolved upstream findings; see [dependency review](dependencies.md)                                                     |

## Behavior exercised

Backend tests cover response transitions and version races; explicit-offset availability validation, persistence, ownership, stale edits/deletes and database constraints; exact/partial/contained/adjacent interval comparisons; different volunteers and organizations; declined/same-event rules; leader role boundaries, preview/create/duplicates, and private-note exclusion. Migration tests start at the original migration with accepted response history, then upgrade and preserve that history. Fresh isolated PostgreSQL schemas apply the entire chain. Seed tests preserve user changes and do not resurrect removed availability.

The live upgrade was separately performed on the existing demo database. Original rows were compared with a pre-upgrade snapshot before and after the additive seed. The original migration files remain unchanged. A second empty database verified the full CLI migration/seed instructions.

The five browser tests cover the original landing → demo → dashboard → event → Accept/refresh → Decline/refresh workflow, keyboard entry, responsive layouts, availability create/edit/delete and persisted conflict changes, and the leader overview/server preview/volunteer access boundary. Primary and new screens receive automated axe WCAG A/AA and narrow-screen overflow checks. Automated checks do not replace a full assistive-technology review.

Mobile component tests render React Native components with the compatible Expo Jest preset. They cover loading/errors/empty states, event navigation, Accept/Decline and remount retrieval, stale response handling, conflict display, foreground refresh, availability CRUD, and exact timestamp preservation when only a note changes. Adapter tests verify endpoint/method/version/error behavior and timezone input validation.

An additional **Expo browser preview** smoke check used the exported React Native Web app against the real API/PostgreSQL: demo entry, event navigation, conflicts, Accept and reload, Decline and reload, and availability retrieval. Screenshots labeled `expo-browser` come from this preview. This verifies shared application code and API integration in Chromium; it is **not native device or simulator validation**.

## Environment limits and remaining work

- No Android emulator, Android SDK, `adb`, `/dev/kvm`, or Apple simulator tooling was available. No native app was installed, signed, or distributed. See the [manual native checklist](mobile-validation.md).
- Expo's online docs/metadata requests were blocked by the environment proxy (403). Versioned package types and the installed SDK compatibility map were used; offline compatibility checks passed.
- Mobile has five underlying upstream dependency advisories. Compatible fixes were investigated; no forced SDK downgrade or incompatible package override was applied. The audit is not reported as passing.
- Both personas are openly shared Development-only identities. Production authentication, conflict resolution/reassignment, recurring schedules, notifications, and real SMS remain future work.
- Conflict detection is advisory and organization-scoped. A preview is a snapshot; another client may change a schedule later. Clients refresh after edits and on return/focus.
- Availability is explicit one-off ranges. Local input rejects DST gaps and chooses the earlier occurrence of a repeated hour; selecting a different offset in the form remains future work.

The README lists repeatable validation commands. CI runs the builds, tests, PostgreSQL/browser workflow, and both Expo exports on pull requests and pushes to `main`. It does not deploy or merge changes.
