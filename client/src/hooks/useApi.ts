import { useState, useEffect, useCallback, useRef } from 'react';
import { ApiResponseError } from '../api/client';

interface UseApiState<T> {
  data: T | null;
  loading: boolean;
  error: string | null;
}

/**
 * Generic hook for GET requests that fetch on mount.
 * Returns data, loading, error, and a refetch function.
 * Pass `enabled: false` to skip fetching.
 */
export function useApiQuery<T>(
  fetcher: (signal: AbortSignal) => Promise<T>,
  deps: readonly unknown[] = [],
  options?: { enabled?: boolean },
) {
  const enabled = options?.enabled ?? true;

  const [state, setState] = useState<UseApiState<T>>({
    data: null,
    loading: enabled,
    error: null,
  });

  const fetcherRef = useRef(fetcher);
  fetcherRef.current = fetcher;

  const load = useCallback(() => {
    const controller = new AbortController();

    setState((s) => ({ ...s, loading: true, error: null }));

    fetcherRef.current(controller.signal)
      .then((data) => {
        if (!controller.signal.aborted) {
          setState({ data, loading: false, error: null });
        }
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        const message =
          err instanceof ApiResponseError
            ? err.body.error
            : err instanceof Error
              ? err.message
              : 'Unknown error';
        setState({ data: null, loading: false, error: message });
      });

    return () => controller.abort();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, enabled]);

  useEffect(() => {
    if (!enabled) {
      setState({ data: null, loading: false, error: null });
      return;
    }
    const cleanup = load();
    return cleanup;
  }, [load, enabled]);

  return { ...state, refetch: load };
}

interface MutationState<T> {
  data: T | null;
  loading: boolean;
  error: string | null;
}

/**
 * Generic hook for mutations (POST/PUT/DELETE).
 * Returns execute function and mutation state.
 */
export function useApiMutation<TResult, TArgs extends unknown[]>(
  mutator: (...args: TArgs) => Promise<TResult>,
) {
  const [state, setState] = useState<MutationState<TResult>>({
    data: null,
    loading: false,
    error: null,
  });

  const execute = useCallback(
    async (...args: TArgs): Promise<TResult> => {
      setState({ data: null, loading: true, error: null });
      try {
        const data = await mutator(...args);
        setState({ data, loading: false, error: null });
        return data;
      } catch (err: unknown) {
        const message =
          err instanceof ApiResponseError
            ? err.body.error
            : err instanceof Error
              ? err.message
              : 'Unknown error';
        setState({ data: null, loading: false, error: message });
        throw err;
      }
    },
    [mutator],
  );

  return { execute, ...state };
}
