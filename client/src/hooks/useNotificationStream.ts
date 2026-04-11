import { useEffect, useRef } from 'react';
import { getTokenProvider } from '../api/client';
import type { NotificationDto } from '../api/types';

const BASE = import.meta.env.VITE_API_BASE_URL ?? '/api';

/**
 * Hook that subscribes to SSE notification events for the current user.
 * Calls `onNotification` whenever a new notification arrives.
 * Automatically reconnects on errors.
 */
export function useNotificationStream(
  onNotification: (notification: NotificationDto) => void,
) {
  const callbackRef = useRef(onNotification);
  callbackRef.current = onNotification;

  useEffect(() => {
    let es: EventSource | null = null;
    let cancelled = false;

    function connect() {
      if (cancelled) return;

      const tokenProvider = getTokenProvider();
      const token = tokenProvider?.();

      const url = `${BASE}/notifications/stream${token ? `?access_token=${token}` : ''}`;

      es = new EventSource(url);

      es.addEventListener('notification', (e) => {
        try {
          const data = JSON.parse(e.data) as NotificationDto;
          callbackRef.current(data);
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
  }, []);
}
