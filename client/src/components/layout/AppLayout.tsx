import type { ReactNode } from 'react';
import Sidebar from './Sidebar';
import Topbar from './Topbar';

interface AppLayoutProps {
  children: ReactNode;
}

export default function AppLayout({ children }: AppLayoutProps) {
  return (
    <div className="flex h-screen bg-gray-50">
      <Sidebar />

      <div className="flex flex-col flex-1 min-w-0">
        <Topbar />

        <main className="flex-1 overflow-y-auto p-6">
          {children}
        </main>
      </div>
    </div>
  );
}
