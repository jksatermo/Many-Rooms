import { useEffect, useRef, useState } from 'react';

// A result belongs to one loader/revision. A changed request renders loading
// immediately, without exposing old data or setting state from an effect body.
export function useResource<T>(
  loader: (signal: AbortSignal) => Promise<T>,
  { refreshOnFocus = true }: { refreshOnFocus?: boolean } = {},
) {
  const [revision, setRevision] = useState(0);
  const [backgroundRevision, setBackgroundRevision] = useState(0);
  const requestGeneration = useRef(0);
  const [resource, setResource] = useState<{
    data?: T;
    error?: Error;
    loader: typeof loader;
    revision: number;
  }>();
  useEffect(() => {
    if (!refreshOnFocus) return;
    let lastRefresh = 0;
    const refresh = () => {
      // Browsers commonly emit both events when a tab returns. Coalesce them.
      const now = Date.now();
      if (document.visibilityState !== 'hidden' && now - lastRefresh > 300) {
        lastRefresh = now;
        setBackgroundRevision((value) => value + 1);
      }
    };
    window.addEventListener('focus', refresh);
    document.addEventListener('visibilitychange', refresh);
    return () => {
      window.removeEventListener('focus', refresh);
      document.removeEventListener('visibilitychange', refresh);
    };
  }, [refreshOnFocus]);
  useEffect(() => {
    const controller = new AbortController();
    const generation = ++requestGeneration.current;
    let active = true;
    loader(controller.signal).then(
      (data) => {
        if (active && generation === requestGeneration.current)
          setResource({ data, loader, revision });
      },
      (error: unknown) => {
        if (active && generation === requestGeneration.current)
          setResource({
            error:
              error instanceof Error
                ? error
                : new Error('Unable to load this page.'),
            loader,
            revision,
          });
      },
    );
    return () => {
      active = false;
      controller.abort();
    };
  }, [loader, revision, backgroundRevision]);
  const current = resource?.loader === loader && resource.revision === revision;
  return {
    data: current ? resource.data : undefined,
    error: current ? resource.error : undefined,
    loading: !current,
    reload: () => setRevision((value) => value + 1),
    setData: (data: T) => {
      // A confirmed mutation wins over an earlier background read.
      requestGeneration.current += 1;
      setResource({ data, loader, revision });
    },
  };
}
