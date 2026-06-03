import {
  createContext,
  useContext,
  useEffect,
  useState,
  type ReactNode
} from "react";
import { loginWithPassword } from "../lib/auth";
import {
  clearStoredEnrollment,
  clearStoredSession,
  isSessionExpired,
  loadStoredSession,
  saveStoredSession,
  type UmbralMobileSession
} from "../lib/session-storage";

type SessionContextValue = {
  loading: boolean;
  session: UmbralMobileSession | null;
  signIn: (username: string, password: string) => Promise<void>;
  signOut: () => Promise<void>;
};

const SessionContext = createContext<SessionContextValue | null>(null);

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

  async function signIn(username: string, password: string) {
    const nextSession = await loginWithPassword({ username, password });
    await saveStoredSession(nextSession);
    setSession(nextSession);
  }

  async function signOut() {
    await clearStoredSession();
    await clearStoredEnrollment();
    setSession(null);
  }

  return (
    <SessionContext.Provider
      value={{
        loading,
        session,
        signIn,
        signOut
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
