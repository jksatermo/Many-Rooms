# Mobile validation

The Expo app uses native React Native components and Expo Router routes. It reads events and conflicts, manages unavailable ranges, and submits responses to the same ASP.NET Core endpoints as the web client. There is no mobile-specific scheduling backend.

## Automated and environment validation

Use the commands in the root README for formatting, ESLint, TypeScript, tests, SDK compatibility, and the Android JavaScript/Hermes export. The repository keeps native component interaction tests separate from API transport tests so both UI behavior and the actual request contract are exercised.

This development environment has no `adb`, Android emulator, Android SDK, `/dev/kvm`, or Apple simulator tooling. Native device/simulator validation has therefore **not** been performed. Any Expo-web screenshots are labeled browser previews of React Native components; they are not evidence of native device testing.

## Remaining device checklist

Use Expo Go/development tooling compatible with the pinned SDK. Configure `EXPO_PUBLIC_API_BASE_URL` to a reachable development API as described in the root README.

1. Enter as Alex, open an event, and verify the current assignment and conflict explanation.
2. Accept, confirm the saved status, force-close/reopen the app, and retrieve the accepted response from PostgreSQL. Repeat with Decline.
3. Change the same assignment in the web app while mobile is open. Confirm mobile reloads on return, and that a deliberately stale submission requires review rather than silently overwriting the newer response.
4. Add, edit, and remove an unavailable range; verify the conflict changes on both clients after refreshing. Check timezone labels, numeric/date keyboards, and daylight-saving boundary input.
5. Exercise offline/server-error retry, slow requests, app background/foreground transitions, small screens, large text, screen-reader navigation, and touch targets.

Record the OS, device/emulator, Expo client/build version, API revision, and actual outcomes before marking native validation complete. Signing, store submission, and production identity remain separate future setup. Review the [known dependency findings](dependencies.md) before distribution.

## Expo browser preview evidence

The exported browser preview was exercised against the real PostgreSQL-backed API: demo entry, event details/conflicts, Accept and reload, Decline and reload, and availability retrieval passed. See [assignment workflow](screenshots/many-rooms-expo-browser-assignment.png) and [availability](screenshots/many-rooms-expo-browser-availability.png). These captures use React Native Web in Chromium and do not establish native rendering, lifecycle, or accessibility behavior.
