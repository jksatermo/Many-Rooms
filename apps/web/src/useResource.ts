import { useEffect, useState } from 'react';

// A result belongs to one loader/revision. A changed request renders loading
// immediately, without exposing old data or setting state from an effect body.
export function useResource<T>(loader: (signal: AbortSignal) => Promise<T>) {
  const [revision, setRevision] = useState(0);
  const [resource, setResource] = useState<{
    data?: T;
    error?: Error;
    loader: typeof loader;
    revision: number;
  }>();
  useEffect(() => {
    const controller = new AbortController();
    let active = true;
    loader(controller.signal).then(
      (data) => {
        if (active) setResource({ data, loader, revision });
      },
      (error: unknown) => {
        if (active)
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
  }, [loader, revision]);
  const current = resource?.loader === loader && resource.revision === revision;
  return {
    data: current ? resource.data : undefined,
    error: current ? resource.error : undefined,
    loading: !current,
    reload: () => setRevision((value) => value + 1),
    setData: (data: T) => setResource({ data, loader, revision }),
  };
}
