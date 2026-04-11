import { del, get, patch } from './client';
import type { NotificationDto } from './types';

export const notificationsApi = {
  getAll: (signal?: AbortSignal) =>
    get<NotificationDto[]>('/notifications', signal),

  getUnreadCount: (signal?: AbortSignal) =>
    get<{ count: number }>('/notifications/unread-count', signal),

  markAsRead: (id: string) =>
    patch(`/notifications/${id}/read`),

  markAllAsRead: () =>
    patch('/notifications/read-all'),

  delete: (id: string) =>
    del(`/notifications/${id}`),
};
