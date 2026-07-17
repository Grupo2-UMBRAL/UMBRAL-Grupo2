import {
  createContext,
  useContext,
  useEffect,
  useState,
  type ReactNode
} from "react";
import { loginWithPassword, refreshSession } from "../lib/auth";
import {
  clearStoredEnrollment,
  clearStoredSession,
  isSessionExpired,
  loadStoredSession,
  resetOnboardingState,
  saveStoredSession,
  type UmbralMobileSession
} from "../lib/session-storage";

type SessionContextValue = {
  loading: boolean;
  session: UmbralMobileSession | null;
  signIn: (username: string, password: string) => Promise<void>;
  signOut: () => Promise<void>;
  renewSession: () => Promise<void>;
};

const SessionContext = createContext<SessionContextValue | null>(null);

function isRefreshTokenUsable(session: UmbralMobileSession): boolean {
  return Boolean(
    session.refreshToken &&
      (!session.refreshExpiresAt || Date.parse(session.refreshExpiresAt) > Date.now())
  );
}

export function SessionProvider({ children }: { children: ReactNode }) {
  const [loading, setLoading] = useState(true);
  const [session, setSession] = useState<UmbralMobileSession | null>(null);

  useEffect(() => {
    let active = true;

    async function hydrateSession() {
      const storedSession = await loadStoredSession();
      if (!active) {
        return;
      }

      if (storedSession && isSessionExpired(storedSession)) {
        // Access token expired while the app was closed — try to renew before giving up.
        if (isRefreshTokenUsable(storedSession)) {
          try {
            const nextSession = await refreshSession(storedSession.refreshToken!);
            if (!active) {
              return;
            }
            await saveStoredSession(nextSession);
            setSession(nextSession);
            setLoading(false);
            return;
          } catch {
            // Refresh token no longer valid; fall through and clear the session.
          }
        }

        if (!active) {
          return;
        }
        await clearStoredSession();
        await clearStoredEnrollment();
        setSession(null);
      } else {
        setSession(storedSession);
      }

      setLoading(false);
    }

    void hydrateSession();

    return () => {
      active = false;
    };
  }, []);

  // Proactively renew the access token ~60s before it expires so a player is never dropped
  // mid-game. Re-runs on every session change (login + each successful refresh reschedules it).
  useEffect(() => {
    if (!session) {
      return;
    }

    const refreshLeadMs = 60_000;
    const delay = Math.max(0, Date.parse(session.expiresAt) - refreshLeadMs - Date.now());
    let cancelled = false;

    const timer = setTimeout(async () => {
      if (cancelled) {
        return;
      }

      if (!isRefreshTokenUsable(session)) {
        // Cannot renew (legacy session or refresh token expired) — end it cleanly.
        await clearStoredSession();
        await clearStoredEnrollment();
        if (!cancelled) {
          setSession(null);
        }
        return;
      }

      try {
        const nextSession = await refreshSession(session.refreshToken!);
        if (cancelled) {
          return;
        }
        await saveStoredSession(nextSession);
        setSession(nextSession);
      } catch {
        if (cancelled) {
          return;
        }
        await clearStoredSession();
        await clearStoredEnrollment();
        setSession(null);
      }
    }, delay);

    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, [session]);

  async function signIn(username: string, password: string) {
    const nextSession = await loginWithPassword({ username, password });
    await saveStoredSession(nextSession);
    await resetOnboardingState();
    setSession(nextSession);
  }

  async function signOut() {
    await clearStoredSession();
    await clearStoredEnrollment();
    setSession(null);
  }

  // Re-mints the access token on demand. Claims are baked into the JWT, so after a rename the stored
  // token still carries the old preferred_username and the app would greet the player by a name they
  // no longer have. The scheduled refresh above would fix it eventually; a rename needs it now.
  async function renewSession() {
    if (!session || !isRefreshTokenUsable(session)) {
      return;
    }

    const nextSession = await refreshSession(session.refreshToken!);
    await saveStoredSession(nextSession);
    setSession(nextSession);
  }

  return (
    <SessionContext.Provider
      value={{
        loading,
        session,
        signIn,
        signOut,
        renewSession
      }}
    >
      {children}
    </SessionContext.Provider>
  );
}

export function useSession() {
  const context = useContext(SessionContext);
  if (!context) {
    throw new Error("useSession must be used inside SessionProvider.");
  }

  return context;
}
