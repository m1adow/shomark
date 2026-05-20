import { useEffect, useRef } from 'react';
import { getTokenProvider } from '../api/client';

const BASE = import.meta.env.VITE_API_BASE_URL ?? '/api';

interface ProcessingCompleteEvent {
  videoId: string;
  highlightCount: number;
}

interface TranscriptionCompleteEvent {
  videoId: string;
  summary: string | null;
}

interface VideoSseCallbacks {
  onProcessingComplete?: (event: ProcessingCompleteEvent) => void;
  onTranscriptionComplete?: (event: TranscriptionCompleteEvent) => void;
}

/**
 * Hook that subscribes to SSE events for video processing.
 * Handles both "processing-complete" (Phase 2) and "transcription-complete" (Phase 1).
 * Automatically reconnects on errors. Cleans up on unmount.
 */
export function useVideoProcessingEvents(
  videoId: string | null,
  callbacksOrOnComplete: VideoSseCallbacks | ((event: ProcessingCompleteEvent) => void),
) {
  // Normalise: accept either the new callbacks object or the legacy single function
  const callbacks: VideoSseCallbacks = typeof callbacksOrOnComplete === 'function'
    ? { onProcessingComplete: callbacksOrOnComplete }
    : callbacksOrOnComplete;

  const callbacksRef = useRef(callbacks);
  callbacksRef.current = callbacks;

  useEffect(() => {
    if (!videoId) return;

    let es: EventSource | null = null;
    let cancelled = false;

    function connect() {
      if (cancelled) return;

      const tokenProvider = getTokenProvider();
      const token = tokenProvider?.();

      const url = `${BASE}/videos/${videoId}/events${token ? `?access_token=${token}` : ''}`;
      es = new EventSource(url);

      es.addEventListener('processing-complete', (e) => {
        try {
          const data = JSON.parse(e.data) as ProcessingCompleteEvent;
          callbacksRef.current.onProcessingComplete?.(data);
        } catch {
          // Malformed event — ignore
        }
      });

      es.addEventListener('transcription-complete', (e) => {
        try {
          const data = JSON.parse(e.data) as TranscriptionCompleteEvent;
          callbacksRef.current.onTranscriptionComplete?.(data);
        } catch {
          // Malformed event — ignore
        }
      });

      es.onerror = () => {
        if (es?.readyState === EventSource.CLOSED) {
          es.close();
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
