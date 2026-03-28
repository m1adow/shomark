import type { ReactNode } from 'react';
import { useAuth } from './AuthProvider';
import { setTokenProvider } from '../api/client';
import keycloak from './keycloak';

export function AuthGate({ children }: { children: ReactNode }) {
  const { initialized, authenticated } = useAuth();

  // Set token provider synchronously so it's available before children mount.
  if (authenticated) {
    setTokenProvider(() => keycloak.token ?? null);
  }

  if (!initialized) {
    return (
      <div className="flex h-screen items-center justify-center">
        <span className="text-gray-500 text-lg">Loading…</span>
      </div>
    );
  }

  if (!authenticated) {
    return (
      <div className="flex h-screen items-center justify-center">
        <span className="text-red-500 text-lg">Authentication failed.</span>
      </div>
    );
  }

  return <>{children}</>;
}
