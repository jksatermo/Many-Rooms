import { useCallback, useRef, useState } from "react";
import { useFocusEffect } from "expo-router";
import { AppState } from "react-native";

/** Refresh on navigation focus, pull-to-refresh, and return from the background. */
export function useFocusedResource<T>(
  loader: (signal: AbortSignal) => Promise<T>,
) {
  const [data, setData] = useState<T | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const refreshRef = useRef<() => void>(() => {});
  const refresh = useCallback(() => refreshRef.current(), []);
  useFocusEffect(
    useCallback(() => {
      let controller: AbortController;
      function load() {
        controller?.abort();
        controller = new AbortController();
        const signal = controller.signal;
        setLoading(true);
        setError(null);
        void loader(signal)
          .then((result) => {
            if (!signal.aborted) setData(result);
          })
          .catch((failure: unknown) => {
            if (!signal.aborted)
              setError(
                failure instanceof Error
                  ? failure.message
                  : "We couldn’t load this schedule. Please try again.",
              );
          })
          .finally(() => {
            if (!signal.aborted) setLoading(false);
          });
      }
      refreshRef.current = load;
      load();
      const subscription = AppState.addEventListener("change", (state) => {
        if (state === "active") load();
      });
      return () => {
        controller?.abort();
        subscription.remove();
        refreshRef.current = () => {};
      };
    }, [loader]),
  );
  return { data, setData, loading, error, refresh };
}
