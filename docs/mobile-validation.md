# Mobile validation

The Expo app uses native React Native components and Expo Router routes. It reads events and conflicts, manages unavailable ranges, and submits responses to the same ASP.NET Core endpoints as the web client. There is no mobile-specific scheduling backend. Milestone 3 adds cancellation presentation and refresh behavior for leader reassignment/role changes; leader scheduling itself remains a web workflow.

## Automated and environment validation

Use the commands in the root README for formatting, ESLint, TypeScript, tests, SDK compatibility, and the Android JavaScript/Hermes export. The repository keeps native component interaction tests separate from API transport tests so both UI behavior and the actual request contract are exercised.

This development environment has no `adb`, Android emulator, Android SDK, `/dev/kvm`, or Apple simulator tooling. Native device/simulator validation has therefore **not** been performed. Any Expo-web screenshots are labeled browser previews of React Native components; they are not evidence of native device testing.

## Remaining device checklist

Use Expo Go/development tooling compatible with the pinned SDK. Configure `EXPO_PUBLIC_API_BASE_URL` to a reachable development API as described in the root README.

1. Enter as Alex, open an event, and verify the current assignment and conflict explanation.
2. Accept, confirm the saved status, force-close/reopen the app, and retrieve the accepted response from PostgreSQL. Repeat with Decline.
3. Change the same assignment in the web app while mobile is open. Confirm mobile reloads on return, and that a deliberately stale submission requires review rather than silently overwriting the newer response.
4. Add, edit, and remove an unavailable range; verify the conflict changes on both clients after refreshing. Check timezone labels, numeric/date keyboards, and daylight-saving boundary input.
5. Background the app, then cancel an assignment from the web leader view. Return and confirm Cancelled appears with no Accept/Decline controls. Repeat with reassignment away from Alex; the former owner must not see response controls. Change the role for Alex and confirm the fresh Pending state/version is used.
6. Make the API unreachable while the app is backgrounded. Returning should show a refresh error and disable cached response controls until a successful refresh.
7. Exercise offline/server-error retry, slow requests, small screens, large text, screen-reader navigation, and touch targets.

Record the OS, device/emulator, Expo client/build version, API revision, and actual outcomes before marking native validation complete. Signing, store submission, and production identity remain separate future setup. Review the [known dependency findings](dependencies.md) before distribution.

## Expo browser preview evidence

The exported browser preview was exercised against the real PostgreSQL-backed API: demo entry, event details/conflicts, Accept and reload, Decline and reload, and availability retrieval passed. See [assignment workflow](screenshots/many-rooms-expo-browser-assignment.png) and [availability](screenshots/many-rooms-expo-browser-availability.png). These captures use React Native Web in Chromium and do not establish native rendering, lifecycle, or accessibility behavior.

## Milestone 3 automated compatibility checks

The unchanged SDK 57 dependency combination passed formatting, lint, TypeScript, 9 adapter/time tests and 16 React Native component tests, Android JavaScript/Hermes export (1,251 modules), Expo browser export (788 modules), and the installed SDK's offline compatibility map. New tests cover additive assignment DTO fields, cancellation, changed ownership/roles after foreground refresh, stale-owner responses, and unavailable refresh disabling cached controls. These tests do not establish native lifecycle or device rendering behavior. No dependency or navigation package upgrades were needed.
