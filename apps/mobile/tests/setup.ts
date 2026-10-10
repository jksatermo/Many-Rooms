import { jest } from "@jest/globals";

jest.mock(
  "react-native-safe-area-context",
  () =>
    jest.requireActual<{ default: unknown }>(
      "react-native-safe-area-context/jest/mock",
    ).default,
);

jest.mock("expo-router", () => ({
  router: { push: jest.fn() },
  // Test screens while retaining the real resource lifecycle; navigation focus is
  // represented by mount/unmount. Foreground refresh is tested via AppState.
  useFocusEffect: (effect: () => (() => void) | void) => {
    const { useEffect } = jest.requireActual<typeof import("react")>("react");
    useEffect(effect, [effect]);
  },
}));
