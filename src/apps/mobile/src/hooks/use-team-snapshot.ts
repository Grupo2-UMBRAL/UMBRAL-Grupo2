import { useCallback, useEffect, useMemo, useState } from "react";
import {
  createAuthorizedApiClient,
  type SessionTeamSnapshot
} from "../lib/api-client";
import {
  clearStoredEnrollment,
  loadStoredEnrollment,
  type StoredEnrollment
} from "../lib/session-storage";
import { useSession } from "../providers/session-provider";

function readErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : "No pudimos cargar tu sesión.";
}

/**
 * Loads the stored enrollment and the matching Session Team snapshot for the
 * read-only participant screens (hub, progress, hints). The live board and
 * ranking keep their own realtime wiring; this hook only covers the simple
 * fetch-on-mount surfaces so they stop duplicating the same effect.
 */
export function useTeamSnapshot() {
  const { session } = useSession();
  const apiClient = useMemo(
    () => (session ? createAuthorizedApiClient(session.accessToken) : null),
    [session]
  );
  const [loading, setLoading] = useState(true);
  const [enrollment, setEnrollment] = useState<StoredEnrollment | null>(null);
  const [snapshot, setSnapshot] = useState<SessionTeamSnapshot | null>(null);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    if (!apiClient || !enrollment) {
      return;
    }

    setError(null);

    try {
      const next = await apiClient.getSessionTeamSnapshot(enrollment.teamId);
      setSnapshot(next);
    } catch (caught) {
      setError(readErrorMessage(caught));
    }
  }, [apiClient, enrollment]);

  const leave = useCallback(async () => {
    await clearStoredEnrollment();
    setEnrollment(null);
    setSnapshot(null);
    setError(null);
  }, []);

  useEffect(() => {
    let active = true;

    async function hydrate() {
      const stored = await loadStoredEnrollment();
      if (!active) {
        return;
      }

      setEnrollment(stored);

      if (apiClient && stored) {
        try {
          const next = await apiClient.getSessionTeamSnapshot(stored.teamId);
          if (active) {
            setSnapshot(next);
          }
        } catch (caught) {
          if (active) {
            setError(readErrorMessage(caught));
          }
        }
      }

      if (active) {
        setLoading(false);
      }
    }

    void hydrate();

    return () => {
      active = false;
    };
  }, [apiClient]);

  return { loading, enrollment, snapshot, error, refresh, leave, apiClient, session };
}
