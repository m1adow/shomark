import {
  createContext,
  useContext,
  useEffect,
  useState,
  useCallback,
  type ReactNode,
} from 'react';
import type Keycloak from 'keycloak-js';
import keycloak from './keycloak';

interface AuthContextValue {
  initialized: boolean;
  authenticated: boolean;
  token: string | undefined;
  user: { id: string; name: string; email: string; preferred_username: string } | null;
  login: () => void;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

const MIN_VALIDITY_SEC = 30;
const REFRESH_INTERVAL_MS = 10_000;

export function AuthProvider({ children }: { children: ReactNode }) {
  const [initialized, setInitialized] = useState(false);
  const [authenticated, setAuthenticated] = useState(false);

  useEffect(() => {
    keycloak
      .init({ onLoad: 'login-required', checkLoginIframe: false })
      .then((auth) => {
        setAuthenticated(auth);
        setInitialized(true);
      })
      .catch((err) => {
        console.error('Keycloak init failed', err);
        setInitialized(true);
      });
  }, []);

  // Silently refresh the token before it expires
  useEffect(() => {
    if (!authenticated) return;

    const id = setInterval(() => {
      keycloak.updateToken(MIN_VALIDITY_SEC).catch(() => {
        keycloak.login();
      });
    }, REFRESH_INTERVAL_MS);

    return () => clearInterval(id);
  }, [authenticated]);

  const login = useCallback(() => keycloak.login(), []);
  const logout = useCallback(() => keycloak.logout({ redirectUri: window.location.origin }), []);

  const user = authenticated ? parseUser(keycloak) : null;

  return (
    <AuthContext.Provider
      value={{
        initialized,
        authenticated,
        token: keycloak.token,
        user,
        login,
        logout,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within <AuthProvider>');
  return ctx;
}

function parseUser(kc: Keycloak) {
  const parsed = kc.tokenParsed;
  if (!parsed) return null;
  return {
    id: (parsed as Record<string, string>)['sub'] ?? '',
    name: (parsed as Record<string, string>)['name'] ?? '',
    email: (parsed as Record<string, string>)['email'] ?? '',
    preferred_username: (parsed as Record<string, string>)['preferred_username'] ?? '',
  };
}
