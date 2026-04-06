import { useEffect, useRef } from 'react';
import { getTokenProvider } from '../api/client';

const BASE = import.meta.env.VITE_API_BASE_URL ?? '/api';

interface ProcessingCompleteEvent {
  videoId: string;
  highlightCount: number;
}

/**
 * Hook that subscribes to SSE events for video processing completion.
 * Automatically reconnects on errors. Cleans up on unmount.
 */
export function useVideoProcessingEvents(
  videoId: string | null,
  onComplete: (event: ProcessingCompleteEvent) => void,
) {
  const onCompleteRef = useRef(onComplete);
  onCompleteRef.current = onComplete;

  useEffect(() => {
    if (!videoId) return;

    let es: EventSource | null = null;
    let cancelled = false;

    function connect() {
      if (cancelled) return;

      const tokenProvider = getTokenProvider();
      const token = tokenProvider?.();

      // EventSource doesn't support Authorization headers natively,
      // so we pass the token as a query param. The backend reads it
      // from the query string if the Authorization header is absent.
      const url = `${BASE}/videos/${videoId}/events${token ? `?access_token=${token}` : ''}`;

      es = new EventSource(url);

      es.addEventListener('processing-complete', (e) => {
        try {
          const data = JSON.parse(e.data) as ProcessingCompleteEvent;
          onCompleteRef.current(data);
        } catch {
          // Malformed event — ignore
        }
      });

      es.onerror = () => {
        // Browser auto-reconnects for most errors.
        // If the connection is fully closed, fall through.
        if (es?.readyState === EventSource.CLOSED) {
          es.close();
          // Retry after 5 seconds
          if (!cancelled) {
            setTimeout(connect, 5000);
          }
        }
      };
    }

    connect();

    return () => {
      cancelled = true;
      es?.close();
    };
  }, [videoId]);
}
