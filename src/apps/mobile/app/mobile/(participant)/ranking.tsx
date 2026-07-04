import {
  HubConnectionBuilder,
  HttpTransportType,
  LogLevel,
  type HubConnection
} from "@microsoft/signalr";
import { Redirect } from "expo-router";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { ActivityIndicator, Animated, Easing, StyleSheet, Text, View } from "react-native";
import { GameButton } from "../../../src/components/game-button";
import { LoadingScreen } from "../../../src/components/loading-screen";
import { Mascot } from "../../../src/components/mascot";
import { ScreenShell, shellStyles } from "../../../src/components/screen-shell";
import { StatusChip } from "../../../src/components/status-chip";
import { colors } from "../../../src/theme/tokens";
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
  return error instanceof Error ? error.message : "No pudimos cargar la clasificación.";
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
  return sessionTeamId.slice(0, 4).toUpperCase();
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
      return { label: "Actualizado", tone: "success" as const };
    case "loading":
      return { label: "Cargando", tone: "warn" as const };
    case "reconnecting":
      return { label: "Reconectando", tone: "warn" as const };
    case "error":
      return { label: "No disponible", tone: "error" as const };
    default:
      return { label: "Esperando", tone: "info" as const };
  }
}

function CountUpScore({ value }: { value: number }) {
  const anim = useRef(new Animated.Value(0)).current;
  const [display, setDisplay] = useState(0);

  useEffect(() => {
    anim.setValue(0);
    const listenerId = anim.addListener(({ value: current }) => setDisplay(Math.round(current)));
    Animated.timing(anim, {
      toValue: value,
      duration: 900,
      easing: Easing.out(Easing.cubic),
      useNativeDriver: false
    }).start();

    return () => anim.removeListener(listenerId);
  }, [anim, value]);

  return <Text style={styles.finalScore}>{display}</Text>;
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
    return <LoadingScreen message="Verificando tu sesión..." />;
  }

  if (loadingEnrollment) {
    return <LoadingScreen message="Buscando tu equipo..." />;
  }

  if (!storedEnrollment) {
    return <Redirect href="/mobile/join" />;
  }

  const status = resolveRankingStatus(rankingStatus);
  const ownTeamId = storedEnrollment.teamId;
  const ownRanking = ranking?.items.find((item) => item.sessionTeamId === ownTeamId);
  const isFinalized = snapshot?.sessionState === "Finalized";
  const podium = ranking?.items.slice(0, 3) ?? [];

  return (
    <ScreenShell
      eyebrow={isFinalized ? "🏁 Fin de la misión" : "Ranking"}
      title={isFinalized ? "¡Resultados finales!" : "Clasificación en vivo"}
      description="Posiciones en vivo de tu sesión. Se ordena por puntaje y, en empate, por tiempo de resolución."
    >
      {isFinalized && ownRanking ? (
        <View style={styles.resultHero}>
          <Mascot mood="celebrate" size={96} />
          <Text style={styles.resultMedal}>{resolveMedal(ownRanking.rank) || "🎖️"}</Text>
          <Text style={styles.resultPlace}>Terminaste en el puesto #{ownRanking.rank}</Text>
          <CountUpScore value={ownRanking.visibleScore} />
          <Text style={styles.resultPointsLabel}>puntos conseguidos</Text>
        </View>
      ) : null}

      {isFinalized && podium.length ? (
        <View style={styles.podium}>
          {podium.map((entry) => {
            const isOwnTeam = entry.sessionTeamId === ownTeamId;
            return (
              <View
                key={entry.sessionTeamId}
                style={[
                  styles.podiumColumn,
                  entry.rank === 1 && styles.podiumFirst,
                  isOwnTeam && styles.podiumOwn
                ]}
              >
                <Text style={styles.podiumMedal}>{resolveMedal(entry.rank)}</Text>
                <Text style={styles.podiumTeam} numberOfLines={1}>
                  {isOwnTeam ? storedEnrollment.teamName : `Equipo ${shortTeamId(entry.sessionTeamId)}`}
                </Text>
                <Text style={styles.podiumScore}>{entry.visibleScore} pts</Text>
              </View>
            );
          })}
        </View>
      ) : null}

      <View style={shellStyles.card}>
        <View style={shellStyles.row}>
          <StatusChip label={status.label} tone={status.tone} />
        </View>
        <Text style={shellStyles.cardText}>
          {ownRanking
            ? `Tu equipo va en puesto #${ownRanking.rank} con ${ownRanking.visibleScore} pts.`
            : "La clasificación aparece cuando tu equipo sume su primer puntaje."}
        </Text>
        {rankingError ? <Text style={styles.error}>{rankingError}</Text> : null}
        {isFinalized ? null : (
          <GameButton
            label="Actualizar"
            variant="ghost"
            icon="↻"
            onPress={() => {
              const liveSessionId = snapshot?.liveSessionId;
              if (liveSessionId) {
                void refreshRanking(liveSessionId);
              }
            }}
          />
        )}
      </View>

      {rankingStatus === "loading" && !ranking ? (
        <View style={styles.inlineStatus}>
          <ActivityIndicator color={colors.text.primary} />
          <Text style={styles.inlineStatusText}>Cargando clasificación...</Text>
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
                  {isOwnTeam ? storedEnrollment.teamName : `Equipo ${shortTeamId(entry.sessionTeamId)}`}
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
              La clasificación aparecerá cuando una respuesta aceptada sume puntaje.
            </Text>
          </View>
        )}
      </View>
    </ScreenShell>
  );
}

const styles = StyleSheet.create({
  resultHero: {
    alignItems: "center",
    backgroundColor: colors.brand.primaryTint,
    borderColor: colors.brand.primary,
    borderRadius: 26,
    borderWidth: 2,
    gap: 6,
    paddingHorizontal: 22,
    paddingVertical: 26
  },
  resultMedal: {
    fontSize: 44
  },
  resultPlace: {
    color: colors.text.primary,
    fontSize: 18,
    fontWeight: "800",
    textAlign: "center"
  },
  finalScore: {
    color: colors.brand.primaryStrong,
    fontSize: 52,
    fontWeight: "900",
    lineHeight: 58
  },
  resultPointsLabel: {
    color: colors.text.secondary,
    fontSize: 14,
    fontWeight: "700",
    textTransform: "uppercase",
    letterSpacing: 0.6
  },
  podium: {
    alignItems: "flex-end",
    flexDirection: "row",
    gap: 10,
    justifyContent: "center"
  },
  podiumColumn: {
    alignItems: "center",
    backgroundColor: colors.surface.card,
    borderColor: colors.surface.cardBorder,
    borderRadius: 18,
    borderWidth: 1,
    flex: 1,
    gap: 4,
    paddingHorizontal: 8,
    paddingVertical: 14
  },
  podiumFirst: {
    backgroundColor: colors.state.warn.fillMuted,
    borderColor: colors.state.warn.borderMuted,
    paddingVertical: 22
  },
  podiumOwn: {
    borderColor: colors.brand.primary,
    borderWidth: 2
  },
  podiumMedal: {
    fontSize: 26
  },
  podiumTeam: {
    color: colors.text.primary,
    fontSize: 13,
    fontWeight: "800",
    textAlign: "center"
  },
  podiumScore: {
    color: colors.brand.primaryStrong,
    fontSize: 14,
    fontWeight: "900"
  },
  ownTeamCard: {
    borderColor: colors.brand.primaryStrong,
    borderWidth: 2
  },
  medal: {
    fontSize: 22
  },
  score: {
    color: colors.brand.primary,
    fontSize: 22,
    fontWeight: "900"
  },
  inlineStatus: {
    alignItems: "center",
    flexDirection: "row",
    gap: 10
  },
  inlineStatusText: {
    color: colors.text.primary,
    fontSize: 14,
    fontWeight: "700"
  },
  error: {
    color: colors.state.error.text,
    fontSize: 14,
    lineHeight: 20
  }
});
