import { Redirect } from "expo-router";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Pressable, StyleSheet, Text, View } from "react-native";
import { LoadingScreen } from "../../src/components/loading-screen";
import { ScreenShell, shellStyles } from "../../src/components/screen-shell";
import { StatusChip } from "../../src/components/status-chip";
import { useSessionOperationsConnection } from "../../src/hooks/use-session-operations-connection";
import {
  createAuthorizedApiClient,
  SnapshotRefreshPolicies,
  type HintUnlockedPayload,
  type RealtimeEventMetadata,
  type SessionStateChangedPayload,
  type SessionTeamSnapshot,
  type TeamProgressChangedPayload,
  type VisibleHintSnapshot
} from "../../src/lib/api-client";
import { getClientConfig } from "../../src/lib/config";
import {
  loadStoredEnrollment,
  type StoredEnrollment
} from "../../src/lib/session-storage";
import { useSession } from "../../src/providers/session-provider";

type SnapshotStatus = "idle" | "loading" | "fresh" | "resyncing" | "stale" | "error";

type ApiClient = ReturnType<typeof createAuthorizedApiClient>;

function formatClock(totalSeconds: number | null) {
  if (totalSeconds === null) {
    return "--:--";
  }

  const safeSeconds = Math.max(0, totalSeconds);
  const minutes = Math.floor(safeSeconds / 60);
  const seconds = safeSeconds % 60;

  return `${minutes.toString().padStart(2, "0")}:${seconds.toString().padStart(2, "0")}`;
}

function resolveStreamStatus(kind: string) {
  switch (kind) {
    case "connected":
      return { label: "Sincronizado", tone: "success" as const };
    case "connecting":
    case "reconnecting":
      return { label: "Reconectando", tone: "warn" as const };
    case "disconnected":
    case "error":
      return { label: "Sin conexión", tone: "error" as const };
    default:
      return { label: "Buscando red", tone: "info" as const };
  }
}

function resolveSnapshotStatus(status: SnapshotStatus) {
  switch (status) {
    case "fresh":
      return { label: "Datos frescos", tone: "success" as const };
    case "loading":
    case "resyncing":
      return { label: "Actualizando snapshot", tone: "warn" as const };
    case "stale":
      return { label: "Datos desactualizados", tone: "warn" as const };
    case "error":
      return { label: "Snapshot no disponible", tone: "error" as const };
    default:
      return { label: "Esperando snapshot", tone: "info" as const };
  }
}

function readErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : "Session Team snapshot request failed.";
}

function metadataMatchesSnapshot(
  metadata: RealtimeEventMetadata,
  snapshot: SessionTeamSnapshot | null
) {
  return !snapshot || metadata.liveSessionId === snapshot.liveSessionId;
}

function isNewer(metadata: RealtimeEventMetadata, currentSequence: number) {
  return metadata.sequenceNumber > currentSequence;
}

function applyMetadataSync(
  snapshot: SessionTeamSnapshot,
  metadata: RealtimeEventMetadata
): SessionTeamSnapshot {
  return {
    ...snapshot,
    sync: {
      sequenceNumber: metadata.sequenceNumber,
      lastUpdatedUtc: metadata.occurredAtUtc,
      serverTimeUtc: metadata.occurredAtUtc
    }
  };
}

function formatDateTime(value?: string) {
  if (!value) {
    return "pending";
  }

  return new Date(value).toLocaleString();
}

export default function BoardPage() {
  const { session } = useSession();
  const config = useMemo(() => getClientConfig(), []);
  const apiClient = useMemo(
    () => (session ? createAuthorizedApiClient(session.accessToken) : null),
    [session]
  );
  const [loadingEnrollment, setLoadingEnrollment] = useState(true);
  const [storedEnrollment, setStoredEnrollment] = useState<StoredEnrollment | null>(null);
  const [snapshot, setSnapshot] = useState<SessionTeamSnapshot | null>(null);
  const [visibleHints, setVisibleHints] = useState<VisibleHintSnapshot[]>([]);
  const [snapshotStatus, setSnapshotStatus] = useState<SnapshotStatus>("idle");
  const [snapshotError, setSnapshotError] = useState<string | null>(null);
  const [remainingSeconds, setRemainingSeconds] = useState<number | null>(null);
  const apiClientRef = useRef<ApiClient | null>(null);
  const enrollmentRef = useRef<StoredEnrollment | null>(null);
  const snapshotRef = useRef<SessionTeamSnapshot | null>(null);
  const sequenceRef = useRef(0);

  useEffect(() => {
    apiClientRef.current = apiClient;
  }, [apiClient]);

  useEffect(() => {
    enrollmentRef.current = storedEnrollment;
  }, [storedEnrollment]);

  useEffect(() => {
    snapshotRef.current = snapshot;
  }, [snapshot]);

  const refreshSnapshot = useCallback(async (mode: "initial" | "resync" = "resync") => {
    const client = apiClientRef.current;
    const enrollment = enrollmentRef.current;

    if (!client || !enrollment) {
      return;
    }

    setSnapshotStatus(mode === "initial" ? "loading" : "resyncing");
    setSnapshotError(null);

    try {
      const nextSnapshot = await client.getSessionTeamSnapshot(enrollment.teamId);

      if (nextSnapshot.sync.sequenceNumber >= sequenceRef.current) {
        sequenceRef.current = nextSnapshot.sync.sequenceNumber;
        setSnapshot(nextSnapshot);
        setVisibleHints(nextSnapshot.visibleHints);
      }

      setSnapshotStatus("fresh");
    } catch (error) {
      setSnapshotStatus(mode === "initial" ? "error" : "stale");
      setSnapshotError(readErrorMessage(error));
    }
  }, []);

  const connectionState = useSessionOperationsConnection({
    accessToken: session?.accessToken ?? "",
    hubUrl: config.sessionHubUrl,
    enabled: Boolean(session),
    onResync: () => {
      void refreshSnapshot("resync");
    }
  });

  useEffect(() => {
    let active = true;

    async function hydrateEnrollment() {
      const enrollment = await loadStoredEnrollment();
      if (!active) {
        return;
      }

      setStoredEnrollment(enrollment);
      setLoadingEnrollment(false);
    }

    void hydrateEnrollment();

    return () => {
      active = false;
    };
  }, []);

  useEffect(() => {
    if (!storedEnrollment || !apiClient) {
      return;
    }

    void refreshSnapshot("initial");
  }, [apiClient, refreshSnapshot, storedEnrollment]);

  useEffect(() => {
    if (remainingSeconds === null || remainingSeconds <= 0) {
      return;
    }

    const intervalId = setInterval(() => {
      setRemainingSeconds((current) => {
        if (current === null) {
          return current;
        }

        return Math.max(0, current - 1);
      });
    }, 1000);

    return () => clearInterval(intervalId);
  }, [remainingSeconds]);

  useEffect(() => {
    const connection = connectionState.connection;
    if (!connection || connectionState.kind !== "connected") {
      return;
    }

    function shouldRefresh(metadata: RealtimeEventMetadata) {
      return metadata.refreshPolicy === SnapshotRefreshPolicies.refreshSnapshot;
    }

    function handleSessionStateChanged(payload: SessionStateChangedPayload) {
      const currentSnapshot = snapshotRef.current;
      if (!metadataMatchesSnapshot(payload.metadata, currentSnapshot)) {
        return;
      }

      if (shouldRefresh(payload.metadata)) {
        void refreshSnapshot("resync");
        return;
      }

      if (!isNewer(payload.metadata, sequenceRef.current)) {
        return;
      }

      sequenceRef.current = payload.metadata.sequenceNumber;

      if (typeof payload.remainingSeconds === "number") {
        setRemainingSeconds(payload.remainingSeconds);
      }

      setSnapshot((current) => {
        if (!current) {
          return current;
        }

        return {
          ...applyMetadataSync(current, payload.metadata),
          sessionState: payload.currentState
        };
      });
      setSnapshotStatus("fresh");
    }

    function handleTeamProgressChanged(payload: TeamProgressChangedPayload) {
      const enrollment = enrollmentRef.current;
      const currentSnapshot = snapshotRef.current;
      if (!enrollment || payload.sessionTeamId !== enrollment.teamId) {
        return;
      }

      if (!metadataMatchesSnapshot(payload.metadata, currentSnapshot)) {
        return;
      }

      if (shouldRefresh(payload.metadata)) {
        void refreshSnapshot("resync");
        return;
      }

      if (!isNewer(payload.metadata, sequenceRef.current)) {
        return;
      }

      sequenceRef.current = payload.metadata.sequenceNumber;
      setSnapshot((current) => {
        if (!current) {
          return current;
        }

        return {
          ...applyMetadataSync(current, payload.metadata),
          currentStage: payload.currentStage ?? current.currentStage,
          progressState: payload.progressState
        };
      });
      setSnapshotStatus("fresh");
    }

    function handleHintUnlocked(payload: HintUnlockedPayload) {
      const enrollment = enrollmentRef.current;
      const currentSnapshot = snapshotRef.current;
      if (!enrollment || payload.sessionTeamId !== enrollment.teamId) {
        return;
      }

      if (!metadataMatchesSnapshot(payload.metadata, currentSnapshot)) {
        return;
      }

      if (shouldRefresh(payload.metadata)) {
        void refreshSnapshot("resync");
        return;
      }

      if (!isNewer(payload.metadata, sequenceRef.current)) {
        return;
      }

      sequenceRef.current = payload.metadata.sequenceNumber;
      setVisibleHints((current) => {
        if (current.some((hint) => hint.hintId === payload.hint.hintId)) {
          return current;
        }

        return [...current, payload.hint];
      });
      setSnapshot((current) => {
        if (!current) {
          return current;
        }

        return applyMetadataSync(current, payload.metadata);
      });
      setSnapshotStatus("fresh");
    }

    connection.on("ReceiveSessionStateChanged", handleSessionStateChanged);
    connection.on("ReceiveTeamProgressChanged", handleTeamProgressChanged);
    connection.on("ReceiveHintUnlocked", handleHintUnlocked);

    return () => {
      connection.off("ReceiveSessionStateChanged", handleSessionStateChanged);
      connection.off("ReceiveTeamProgressChanged", handleTeamProgressChanged);
      connection.off("ReceiveHintUnlocked", handleHintUnlocked);
    };
  }, [connectionState.connection, connectionState.kind, refreshSnapshot]);

  if (!session) {
    return <LoadingScreen message="Checking participant session..." />;
  }

  if (loadingEnrollment) {
    return <LoadingScreen message="Checking Session Team enrollment..." />;
  }

  if (!storedEnrollment) {
    return <Redirect href="/join" />;
  }

  if (snapshotStatus === "loading" && !snapshot) {
    return <LoadingScreen message="Loading Session Team snapshot..." />;
  }

  const streamStatus = resolveStreamStatus(connectionState.kind);
  const dataStatus = resolveSnapshotStatus(snapshotStatus);
  const currentStage = snapshot?.currentStage;
  const currentStageHints = currentStage
    ? visibleHints.filter((hint) => hint.missionStageId === currentStage.missionStageId)
    : [];

  return (
    <ScreenShell
      eyebrow="Participant Stage View"
      title={snapshot ? `${snapshot.teamName} board` : "Session Team board"}
      description="Live board consumes Session Operations snapshot and SignalR events without redefining backend contracts."
    >
      <View style={shellStyles.card}>
        <Text style={shellStyles.cardTitle}>Realtime sync</Text>
        <View style={shellStyles.row}>
          <StatusChip label={streamStatus.label} tone={streamStatus.tone} />
          <StatusChip label={dataStatus.label} tone={dataStatus.tone} />
        </View>
        <Text style={shellStyles.cardText}>{connectionState.detail}</Text>
        {snapshotError ? <Text style={styles.error}>{snapshotError}</Text> : null}
        <Pressable
          onPress={() => {
            void refreshSnapshot("resync");
          }}
          style={({ pressed }) => [styles.secondaryButton, pressed && styles.buttonPressed]}
        >
          <Text style={styles.secondaryButtonLabel}>Refresh snapshot</Text>
        </Pressable>
      </View>

      <View style={shellStyles.card}>
        <Text style={shellStyles.cardTitle}>Session Team</Text>
        <StatusChip label={snapshot?.sessionState ?? "Snapshot pending"} tone="info" />
        <Text style={shellStyles.cardText}>Join Code: {storedEnrollment.joinCode}</Text>
        <Text style={shellStyles.cardText}>Team: {snapshot?.teamName ?? storedEnrollment.teamName}</Text>
        <Text style={shellStyles.cardText}>Progress: {snapshot?.progressState ?? "pending"}</Text>
        <Text style={shellStyles.mono}>TeamId: {storedEnrollment.teamId}</Text>
      </View>

      <View style={shellStyles.card}>
        <View style={shellStyles.row}>
          <Text style={shellStyles.cardTitle}>Session clock</Text>
          <StatusChip label={remainingSeconds === null ? "Waiting event" : "Running"} tone="warn" />
        </View>
        <Text style={styles.clock}>{formatClock(remainingSeconds)}</Text>
        <Text style={shellStyles.cardText}>
          Clock updates from `ReceiveSessionStateChanged.remainingSeconds` when backend emits it.
        </Text>
      </View>

      <View style={shellStyles.card}>
        <Text style={shellStyles.cardTitle}>Current playable stage</Text>
        {currentStage ? (
          <>
            <StatusChip label={currentStage.difficulty} tone="success" />
            <Text style={styles.stageTitle}>{currentStage.name}</Text>
            <Text style={shellStyles.cardText}>Game type: {currentStage.gameType}</Text>
            <Text style={shellStyles.cardText}>
              Stage order: {currentStage.sessionStageOrder} · Source order: {currentStage.sourceOrder}
            </Text>
            <Text style={shellStyles.cardText}>
              Time budget: {currentStage.resolvedTimeBudgetMinutes} min
            </Text>
            <Text style={shellStyles.mono}>MissionStageId: {currentStage.missionStageId}</Text>
          </>
        ) : (
          <>
            <StatusChip label="No active stage" tone="warn" />
            <Text style={shellStyles.cardText}>
              Snapshot has no current stage yet. Board stays read-only until Session Progression moves this Session Team.
            </Text>
          </>
        )}
      </View>

      <View style={shellStyles.card}>
        <Text style={shellStyles.cardTitle}>Visible hints</Text>
        {currentStageHints.length > 0 ? (
          <View style={shellStyles.list}>
            {currentStageHints.map((hint) => (
              <View key={hint.hintId} style={styles.hintItem}>
                <View style={shellStyles.row}>
                  <StatusChip label={hint.isSolution ? "Solution" : "Hint"} tone={hint.isSolution ? "warn" : "info"} />
                  <StatusChip label={hint.unlockReason} tone="neutral" />
                </View>
                <Text style={shellStyles.cardText}>{hint.content}</Text>
                <Text style={shellStyles.mono}>Unlocked: {formatDateTime(hint.unlockedAtUtc)}</Text>
              </View>
            ))}
          </View>
        ) : (
          <Text style={shellStyles.cardText}>
            No visible hints for current stage. New `ReceiveHintUnlocked` events append here.
          </Text>
        )}
      </View>

      <View style={shellStyles.card}>
        <Text style={shellStyles.cardTitle}>Sync metadata</Text>
        <Text style={shellStyles.cardText}>
          Sequence: {snapshot?.sync.sequenceNumber ?? sequenceRef.current}
        </Text>
        <Text style={shellStyles.cardText}>
          Last updated: {formatDateTime(snapshot?.sync.lastUpdatedUtc)}
        </Text>
        <Text style={shellStyles.cardText}>
          Server time: {formatDateTime(snapshot?.sync.serverTimeUtc)}
        </Text>
      </View>
    </ScreenShell>
  );
}

const styles = StyleSheet.create({
  secondaryButton: {
    backgroundColor: "#17313b",
    borderRadius: 18,
    alignItems: "center",
    paddingHorizontal: 16,
    paddingVertical: 14
  },
  secondaryButtonLabel: {
    color: "#f7fbfc",
    fontSize: 15,
    fontWeight: "700"
  },
  buttonPressed: {
    opacity: 0.85
  },
  error: {
    color: "#9e2f2f",
    fontSize: 14,
    lineHeight: 20
  },
  clock: {
    color: "#17313b",
    fontSize: 48,
    fontWeight: "800",
    letterSpacing: 1.5
  },
  stageTitle: {
    color: "#17313b",
    fontSize: 24,
    fontWeight: "800",
    lineHeight: 30
  },
  hintItem: {
    backgroundColor: "#f6efe6",
    borderColor: "#eadcc8",
    borderRadius: 18,
    borderWidth: 1,
    gap: 10,
    padding: 14
  }
});
