import {
  HubConnectionBuilder,
  HttpTransportType,
  LogLevel,
  type HubConnection
} from "@microsoft/signalr";
import { Redirect } from "expo-router";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { ActivityIndicator, Pressable, StyleSheet, Text, View } from "react-native";
import { LoadingScreen } from "../../../src/components/loading-screen";
import { ScreenShell, shellStyles } from "../../../src/components/screen-shell";
import { StatusChip } from "../../../src/components/status-chip";
import {
  createAuthorizedApiClient,
  type SessionTeamSnapshot
} from "../../../src/lib/api-client";
import { getClientConfig } from "../../../src/lib/config";
import {
  loadStoredEnrollment,
  type StoredEnrollment
} from "../../../src/lib/session-storage";
import { useSession } from "../../../src/providers/session-provider";

type RankingStatus = "idle" | "loading" | "fresh" | "reconnecting" | "error";

type RankingItem = {
  rank: number;
  sessionTeamId: string;
  visibleScore: number;
  resolutionTime: string;
};

type RankingPayload = {
  liveSessionId: string;
  generatedAtUtc: string;
  items: RankingItem[];
};

type ApiClient = ReturnType<typeof createAuthorizedApiClient>;

function readErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : "Ranking request failed.";
}

function getRankingUrl(edgeProxyPublicBaseUrl: string, liveSessionId: string) {
  return `${edgeProxyPublicBaseUrl}/scoring-monitoring/api/scoring-monitoring/sessions/${liveSessionId}/ranking`;
}

function formatResolutionTime(value: string) {
  const [hours = "00", minutes = "00", seconds = "00"] = value.split(":");
  const secondParts = seconds.split(".");
  return `${hours.padStart(2, "0")}:${minutes.padStart(2, "0")}:${secondParts[0].padStart(2, "0")}`;
}

function shortTeamId(sessionTeamId: string) {
  return sessionTeamId.slice(0, 8);
}

function resolveMedal(rank: number) {
  switch (rank) {
    case 1:
      return "🥇";
    case 2:
      return "🥈";
    case 3:
      return "🥉";
    default:
      return "";
  }
}

function resolveRankingStatus(status: RankingStatus) {
  switch (status) {
    case "fresh":
      return { label: "Ranking fresco", tone: "success" as const };
    case "loading":
      return { label: "Cargando ranking", tone: "warn" as const };
    case "reconnecting":
      return { label: "Reconectando", tone: "warn" as const };
    case "error":
      return { label: "Ranking no disponible", tone: "error" as const };
    default:
      return { label: "Esperando ranking", tone: "info" as const };
  }
}

async function fetchRanking(
  edgeProxyPublicBaseUrl: string,
  accessToken: string,
  liveSessionId: string
) {
  const response = await fetch(getRankingUrl(edgeProxyPublicBaseUrl, liveSessionId), {
    headers: {
      Authorization: `Bearer ${accessToken}`
    }
  });

  if (response.status === 404) {
    return {
      liveSessionId,
      generatedAtUtc: new Date().toISOString(),
      items: []
    } satisfies RankingPayload;
  }

  if (!response.ok) {
    const detail = await response.text();
    throw new Error(detail.trim() || `${response.status} ${response.statusText}`);
  }

  return (await response.json()) as RankingPayload;
}

export default function RankingPage() {
  const { session } = useSession();
  const config = useMemo(() => getClientConfig(), []);
  const apiClient = useMemo(
    () => (session ? createAuthorizedApiClient(session.accessToken) : null),
    [session]
  );
  const apiClientRef = useRef<ApiClient | null>(null);
  const snapshotRef = useRef<SessionTeamSnapshot | null>(null);
  const [loadingEnrollment, setLoadingEnrollment] = useState(true);
  const [storedEnrollment, setStoredEnrollment] = useState<StoredEnrollment | null>(null);
  const [snapshot, setSnapshot] = useState<SessionTeamSnapshot | null>(null);
  const [ranking, setRanking] = useState<RankingPayload | null>(null);
  const [rankingStatus, setRankingStatus] = useState<RankingStatus>("idle");
  const [rankingError, setRankingError] = useState<string | null>(null);

  useEffect(() => {
    apiClientRef.current = apiClient;
  }, [apiClient]);

  useEffect(() => {
    snapshotRef.current = snapshot;
  }, [snapshot]);

  const refreshRanking = useCallback(async (liveSessionId: string) => {
    if (!session) {
      return;
    }

    setRankingStatus("loading");
    setRankingError(null);

    try {
      const payload = await fetchRanking(config.edgeProxyPublicBaseUrl, session.accessToken, liveSessionId);
      setRanking(payload);
      setRankingStatus("fresh");
    } catch (error) {
      setRankingStatus("error");
      setRankingError(readErrorMessage(error));
    }
  }, [config.edgeProxyPublicBaseUrl, session]);

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
    const client = apiClientRef.current;
    if (!client || !storedEnrollment) {
      return;
    }

    const currentClient = client;
    const currentEnrollment = storedEnrollment;
    let active = true;

    async function loadSnapshotAndRanking() {
      setRankingStatus("loading");
      setRankingError(null);

      try {
        const nextSnapshot = await currentClient.getSessionTeamSnapshot(currentEnrollment.teamId);
        if (!active) {
          return;
        }

        setSnapshot(nextSnapshot);
        await refreshRanking(nextSnapshot.liveSessionId);
      } catch (error) {
        if (!active) {
          return;
        }

        setRankingStatus("error");
        setRankingError(readErrorMessage(error));
      }
    }

    void loadSnapshotAndRanking();

    return () => {
      active = false;
    };
  }, [refreshRanking, storedEnrollment]);

  useEffect(() => {
    if (!session || !snapshot?.liveSessionId) {
      return;
    }

    const accessToken = session.accessToken;
    let active = true;
    let connection: HubConnection | null = null;

    async function connect() {
      connection = new HubConnectionBuilder()
        .withUrl(config.scoringHubUrl, {
          accessTokenFactory: () => accessToken,
          skipNegotiation: false,
          transport: HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000])
        .configureLogging(LogLevel.Warning)
        .build();

      connection.on("ReceiveRankingUpdated", (payload: RankingPayload) => {
        const currentSnapshot = snapshotRef.current;
        if (!currentSnapshot || payload.liveSessionId !== currentSnapshot.liveSessionId) {
          return;
        }

        setRanking(payload);
        setRankingStatus("fresh");
        setRankingError(null);
      });

      connection.onreconnecting(() => {
        if (active) {
          setRankingStatus("reconnecting");
        }
      });

      connection.onreconnected(() => {
        const currentSnapshot = snapshotRef.current;
        if (!active || !currentSnapshot) {
          return;
        }

        void refreshRanking(currentSnapshot.liveSessionId);
      });

      connection.onclose((error) => {
        if (!active || !error) {
          return;
        }

        setRankingStatus("error");
        setRankingError(error.message);
      });

      try {
        await connection.start();
      } catch (error) {
        if (!active) {
          return;
        }

        setRankingStatus("error");
        setRankingError(readErrorMessage(error));
      }
    }

    void connect();

    return () => {
      active = false;
      if (connection) {
        void connection.stop();
      }
    };
  }, [config.scoringHubUrl, refreshRanking, session, snapshot?.liveSessionId]);

  if (!session) {
    return <LoadingScreen message="Checking participant session..." />;
  }

  if (loadingEnrollment) {
    return <LoadingScreen message="Checking Session Team enrollment..." />;
  }

  if (!storedEnrollment) {
    return <Redirect href="/mobile/join" />;
  }

  const status = resolveRankingStatus(rankingStatus);
  const ownTeamId = storedEnrollment.teamId;
  const ownRanking = ranking?.items.find((item) => item.sessionTeamId === ownTeamId);

  return (
    <ScreenShell
      eyebrow="Ranking"
      title={snapshot ? `${snapshot.teamName} standings` : "Session Team standings"}
      description="Posiciones en vivo de tu sesión. Se ordena por puntaje y, en empate, por tiempo de resolución."
    >
      <View style={shellStyles.card}>
        <View style={shellStyles.row}>
          <StatusChip label={status.label} tone={status.tone} />
          <StatusChip label="SignalR Scoring" tone="info" />
        </View>
        <Text style={shellStyles.cardText}>
          {ownRanking
            ? `Tu equipo va en puesto #${ownRanking.rank} con ${ownRanking.visibleScore} pts.`
            : "Ranking listo cuando tu equipo sume su primer puntaje."}
        </Text>
        {rankingError ? <Text style={styles.error}>{rankingError}</Text> : null}
        <Pressable
          onPress={() => {
            const liveSessionId = snapshot?.liveSessionId;
            if (liveSessionId) {
              void refreshRanking(liveSessionId);
            }
          }}
          style={({ pressed }) => [styles.secondaryButton, pressed && styles.buttonPressed]}
        >
          <Text style={styles.secondaryButtonLabel}>Actualizar ranking</Text>
        </Pressable>
      </View>

      {rankingStatus === "loading" && !ranking ? (
        <View style={styles.inlineStatus}>
          <ActivityIndicator color="#4B4B4B" />
          <Text style={styles.inlineStatusText}>Cargando ranking...</Text>
        </View>
      ) : null}

      <View style={shellStyles.section}>
        {ranking?.items.length ? (
          ranking.items.map((entry) => {
            const isOwnTeam = entry.sessionTeamId === ownTeamId;

            return (
              <View key={entry.sessionTeamId} style={[shellStyles.card, isOwnTeam && styles.ownTeamCard]}>
                <View style={shellStyles.row}>
                  {resolveMedal(entry.rank) ? (
                    <Text style={styles.medal}>{resolveMedal(entry.rank)}</Text>
                  ) : null}
                  <StatusChip label={`#${entry.rank}`} tone={isOwnTeam ? "success" : "info"} />
                  {isOwnTeam ? <StatusChip label="Tu equipo" tone="success" /> : null}
                </View>
                <Text style={shellStyles.cardTitle}>
                  {isOwnTeam ? storedEnrollment.teamName : `Session Team ${shortTeamId(entry.sessionTeamId)}`}
                </Text>
                <Text style={styles.score}>{entry.visibleScore} pts</Text>
                <Text style={shellStyles.mono}>Tiempo {formatResolutionTime(entry.resolutionTime)}</Text>
              </View>
            );
          })
        ) : (
          <View style={shellStyles.card}>
            <StatusChip label="Sin puntaje" tone="warn" />
            <Text style={shellStyles.cardText}>
              El ranking aparecerá cuando una respuesta aceptada sume puntaje.
            </Text>
          </View>
        )}
      </View>
    </ScreenShell>
  );
}

const styles = StyleSheet.create({
  ownTeamCard: {
    borderColor: "#58A700",
    borderWidth: 2
  },
  medal: {
    fontSize: 22
  },
  score: {
    color: "#58CC02",
    fontSize: 22,
    fontWeight: "900"
  },
  secondaryButton: {
    alignItems: "center",
    backgroundColor: "#4B4B4B",
    borderRadius: 18,
    paddingHorizontal: 16,
    paddingVertical: 14
  },
  secondaryButtonLabel: {
    color: "#FFFFFF",
    fontSize: 15,
    fontWeight: "700"
  },
  buttonPressed: {
    opacity: 0.85
  },
  inlineStatus: {
    alignItems: "center",
    flexDirection: "row",
    gap: 10
  },
  inlineStatusText: {
    color: "#4B4B4B",
    fontSize: 14,
    fontWeight: "700"
  },
  error: {
    color: "#EA2B2B",
    fontSize: 14,
    lineHeight: 20
  }
});
