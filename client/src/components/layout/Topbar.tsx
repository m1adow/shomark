import { Badge } from 'primereact/badge';
import { Avatar } from 'primereact/avatar';
import { Button } from 'primereact/button';
import { Toast } from 'primereact/toast';
import { useCallback, useRef, useState } from 'react';
import MobileSidebar from './MobileSidebar';
import NotificationPanel, { type NotificationPanelHandle } from './NotificationPanel';
import { useAuth } from '../../auth';
import {
  useNotifications,
  useUnreadCount,
  useMarkAsRead,
  useMarkAllAsRead,
  useDeleteNotification,
} from '../../hooks/useNotifications';
import { useNotificationStream } from '../../hooks/useNotificationStream';
import type { NotificationDto } from '../../api/types';

export default function Topbar() {
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const { user, logout } = useAuth();

  const { data: notifications, refetch: refetchNotifications } = useNotifications();
  const { data: unreadData, refetch: refetchUnread } = useUnreadCount();
  const { execute: markAsRead } = useMarkAsRead();
  const { execute: markAllAsRead } = useMarkAllAsRead();
  const { execute: deleteNotification } = useDeleteNotification();

  const panelRef = useRef<NotificationPanelHandle>(null);
  const toastRef = useRef<Toast>(null);

  const unreadCount = unreadData?.count ?? 0;

  const handleNewNotification = useCallback(
    (notification: NotificationDto) => {
      refetchNotifications();
      refetchUnread();
      toastRef.current?.show({
        severity: notification.type === 'PostFailed' ? 'error' : 'success',
        summary: notification.title,
        detail: notification.message ?? undefined,
        life: 5000,
      });
    },
    [refetchNotifications, refetchUnread],
  );

  useNotificationStream(handleNewNotification);

  const handleMarkAsRead = useCallback(
    async (id: string) => {
      await markAsRead(id);
      refetchNotifications();
      refetchUnread();
    },
    [markAsRead, refetchNotifications, refetchUnread],
  );

  const handleMarkAllAsRead = useCallback(async () => {
    await markAllAsRead();
    refetchNotifications();
    refetchUnread();
  }, [markAllAsRead, refetchNotifications, refetchUnread]);

  const handleDelete = useCallback(
    async (id: string) => {
      await deleteNotification(id);
      refetchNotifications();
      refetchUnread();
    },
    [deleteNotification, refetchNotifications, refetchUnread],
  );

  return (
    <>
      <Toast ref={toastRef} position="top-right" />

      <header className="flex items-center justify-between h-16 px-6 bg-white border-b border-gray-200">
        {/* Left: mobile menu toggle + breadcrumb area */}
        <div className="flex items-center gap-3">
          <div className="md:hidden">
            <Button
              icon="pi pi-bars"
              text
              onClick={() => setMobileMenuOpen(true)}
              aria-label="Open navigation menu"
            />
          </div>
          <span className="text-sm font-medium text-gray-500 hidden md:block">
            Marketing Automation
          </span>
        </div>

        {/* Right: notifications + profile */}
        <div className="flex items-center gap-4">
          <button
            type="button"
            className="relative p-2 rounded-lg hover:bg-gray-100 transition-colors"
            aria-label="Notifications"
            onClick={(e) => panelRef.current?.toggle(e)}
          >
            <i className="pi pi-bell text-gray-600 text-lg" />
            {unreadCount > 0 && (
              <Badge
                value={unreadCount > 99 ? '99+' : String(unreadCount)}
                severity="danger"
                className="absolute -top-0.5 -right-0.5"
              />
            )}
          </button>

          <button
            type="button"
            className="p-2 rounded-lg hover:bg-gray-100 transition-colors"
            onClick={logout}
            aria-label="Sign out"
          >
            <i className="pi pi-sign-out text-gray-600 text-lg" />
          </button>

          <div className="flex items-center gap-3 pl-4 border-l border-gray-200">
            <div className="hidden sm:block text-right">
              <p className="text-sm font-medium text-gray-900 leading-tight">
                {user?.name || user?.preferred_username || 'User'}
              </p>
              <p className="text-xs text-gray-500">{user?.email}</p>
            </div>
            <Avatar
              label={user?.name?.charAt(0)?.toUpperCase() ?? 'U'}
              shape="circle"
              className="bg-blue-100 text-blue-700"
              aria-label="User profile"
            />
          </div>
        </div>
      </header>

      <NotificationPanel
        ref={panelRef}
        notifications={notifications ?? []}
        onMarkAsRead={handleMarkAsRead}
        onMarkAllAsRead={handleMarkAllAsRead}
        onDelete={handleDelete}
      />

      <MobileSidebar
        visible={mobileMenuOpen}
        onHide={() => setMobileMenuOpen(false)}
      />
    </>
  );
}
