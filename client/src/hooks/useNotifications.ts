import { useCallback } from 'react';
import { notificationsApi } from '../api/notifications';
import { useApiMutation, useApiQuery } from './useApi';

export function useNotifications() {
  return useApiQuery(
    (signal) => notificationsApi.getAll(signal),
    [],
  );
}

export function useUnreadCount() {
  return useApiQuery(
    (signal) => notificationsApi.getUnreadCount(signal),
    [],
  );
}

export function useMarkAsRead() {
  return useApiMutation(
    useCallback((id: string) => notificationsApi.markAsRead(id), []),
  );
}

export function useMarkAllAsRead() {
  return useApiMutation(
    useCallback(() => notificationsApi.markAllAsRead(), []),
  );
}

export function useDeleteNotification() {
  return useApiMutation(
    useCallback((id: string) => notificationsApi.delete(id), []),
  );
}
