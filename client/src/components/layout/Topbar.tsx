import { Badge } from 'primereact/badge';
import { Avatar } from 'primereact/avatar';
import { Button } from 'primereact/button';
import { useState } from 'react';
import MobileSidebar from './MobileSidebar';
import { useAuth } from '../../auth';

export default function Topbar() {
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const { user, logout } = useAuth();

  return (
    <>
      <header className="flex items-center justify-between h-16 px-6 bg-white border-b border-gray-200">
        {/* Left: mobile menu toggle + breadcrumb area */}
        <div className="flex items-center gap-3">
          <Button
            icon="pi pi-bars"
            text
            className="md:hidden"
            onClick={() => setMobileMenuOpen(true)}
            aria-label="Open navigation menu"
          />
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
          >
            <i className="pi pi-bell text-gray-600 text-lg" />
            <Badge
              value="3"
              severity="danger"
              className="absolute -top-0.5 -right-0.5"
            />
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

      <MobileSidebar
        visible={mobileMenuOpen}
        onHide={() => setMobileMenuOpen(false)}
      />
    </>
  );
}
