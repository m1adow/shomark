import { forwardRef, useImperativeHandle, useRef, useCallback } from 'react';
import { OverlayPanel } from 'primereact/overlaypanel';
import { Button } from 'primereact/button';
import type { NotificationDto } from '../../api/types';

interface NotificationPanelProps {
  notifications: NotificationDto[];
  onMarkAsRead: (id: string) => void;
  onMarkAllAsRead: () => void;
  onDelete: (id: string) => void;
  onNavigate: (notification: NotificationDto) => void;
}

export interface NotificationPanelHandle {
  toggle: (e: React.SyntheticEvent) => void;
  close: () => void;
}

function typeIcon(type: string) {
  switch (type) {
    case 'VideoProcessingCompleted':
      return 'pi pi-video';
    case 'PostPublished':
      return 'pi pi-check-circle';
    case 'PostFailed':
      return 'pi pi-times-circle';
    default:
      return 'pi pi-info-circle';
  }
}

function typeColor(type: string) {
  switch (type) {
    case 'VideoProcessingCompleted':
      return 'text-blue-600';
    case 'PostPublished':
      return 'text-green-600';
    case 'PostFailed':
      return 'text-red-600';
    default:
      return 'text-gray-600';
  }
}

function timeAgo(dateStr: string) {
  const diff = Date.now() - new Date(dateStr).getTime();
  const mins = Math.floor(diff / 60000);
  if (mins < 1) return 'just now';
  if (mins < 60) return `${mins}m ago`;
  const hours = Math.floor(mins / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  return `${days}d ago`;
}

const NotificationPanel = forwardRef<NotificationPanelHandle, NotificationPanelProps>(
  ({ notifications, onMarkAsRead, onMarkAllAsRead, onDelete, onNavigate }, ref) => {
    const overlayRef = useRef<OverlayPanel>(null);

    useImperativeHandle(ref, () => ({
      toggle: (e: React.SyntheticEvent) => overlayRef.current?.toggle(e),
      close: () => overlayRef.current?.hide(),
    }));

    const hasUnread = notifications.some((n) => !n.isRead);

    const handleClick = useCallback(
      (n: NotificationDto) => {
        if (!n.isRead) onMarkAsRead(n.id);
        onNavigate(n);
      },
      [onMarkAsRead, onNavigate],
    );

    return (
      <OverlayPanel ref={overlayRef} className="w-80 md:w-96 p-0">
        <div className="flex items-center justify-between px-4 py-3 border-b border-gray-200">
          <h3 className="text-sm font-semibold text-gray-900">Notifications</h3>
          {hasUnread && (
            <Button
              label="Mark all read"
              text
              size="small"
              className="text-xs p-0"
              onClick={onMarkAllAsRead}
            />
          )}
        </div>

        <div className="max-h-80 overflow-y-auto">
          {notifications.length === 0 ? (
            <div className="px-4 py-8 text-center text-sm text-gray-400">
              No notifications yet
            </div>
          ) : (
            notifications.map((n) => (
              <div
                key={n.id}
                className={`group relative flex items-start gap-3 px-4 py-3 border-b border-gray-100 last:border-b-0 ${
                  !n.isRead ? 'bg-blue-50/50' : ''
                }`}
              >
                <button
                  type="button"
                  className="flex-1 text-left flex items-start gap-3 cursor-pointer hover:bg-gray-50/50 transition-colors -mx-1 px-1 rounded"
                  onClick={() => handleClick(n)}
                >
                  <i className={`${typeIcon(n.type)} ${typeColor(n.type)} text-lg mt-0.5`} />
                  <div className="flex-1 min-w-0">
                    <p
                      className={`text-sm leading-tight ${
                        !n.isRead ? 'font-semibold text-gray-900' : 'text-gray-700'
                      }`}
                    >
                      {n.title}
                    </p>
                    {n.message && (
                      <p className="text-xs text-gray-500 mt-0.5 truncate">{n.message}</p>
                    )}
                    <p className="text-xs text-gray-400 mt-1">{timeAgo(n.createdAt)}</p>
                  </div>
                  {!n.isRead && (
                    <span className="mt-1.5 h-2 w-2 rounded-full bg-blue-500 flex-shrink-0" />
                  )}
                </button>
                <button
                  type="button"
                  aria-label="Delete notification"
                  className="opacity-0 group-hover:opacity-100 transition-opacity ml-1 mt-0.5 p-1 rounded text-gray-400 hover:text-red-500 hover:bg-red-50 flex-shrink-0"
                  onClick={() => onDelete(n.id)}
                >
                  <i className="pi pi-trash text-xs" />
                </button>
              </div>
            ))
          )}
        </div>
      </OverlayPanel>
    );
  },
);

NotificationPanel.displayName = 'NotificationPanel';

export default NotificationPanel;
