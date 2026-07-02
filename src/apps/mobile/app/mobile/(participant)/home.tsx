import { useRouter } from "expo-router";
import { useMemo } from "react";
import { Pressable, StyleSheet, Text, View } from "react-native";
import { GameButton } from "../../../src/components/game-button";
import { LoadingScreen } from "../../../src/components/loading-screen";
import { ProgressBar } from "../../../src/components/progress-bar";
import { ScreenShell, shellStyles } from "../../../src/components/screen-shell";
import { StatusChip } from "../../../src/components/status-chip";
import { getClientConfig } from "../../../src/lib/config";
import {
  basePointsForDifficulty,
  buildStageProgress,
  formatDifficulty,
  formatGameType,
  isTreasureHunt
} from "../../../src/lib/stage-progress";
import { useSessionManagementConnection } from "../../../src/hooks/use-session-management-connection";
import { useTeamSnapshot } from "../../../src/hooks/use-team-snapshot";
import { useSession } from "../../../src/providers/session-provider";

function resolveConnection(kind: string) {
  switch (kind) {
    case "connected":
      return { label: "En línea", tone: "success" as const };
    case "reconnecting":
      return { label: "Reconectando", tone: "warn" as const };
    case "error":
      return { label: "Sin conexión", tone: "error" as const };
    case "connecting":
      return { label: "Conectando", tone: "info" as const };
    default:
      return { label: "Desconectado", tone: "neutral" as const };
  }
}

function resolveSessionState(state: string | undefined) {
  switch (state) {
    case "Running":
    case "Active":
      return { label: "En juego", tone: "success" as const };
    case "Paused":
      return { label: "En pausa", tone: "warn" as const };
    case "Finalized":
      return { label: "Finalizada", tone: "neutral" as const };
    case "Canceled":
      return { label: "Cancelada", tone: "error" as const };
    default:
      return { label: state ?? "Por iniciar", tone: "info" as const };
  }
}

type HubTileProps = {
  icon: string;
  title: string;
  onPress: () => void;
};

function HubTile({ icon, title, onPress }: HubTileProps) {
  return (
    <Pressable
      onPress={onPress}
      style={({ pressed }) => [styles.tile, pressed && styles.tilePressed]}
    >
      <Text style={styles.tileIcon}>{icon}</Text>
      <Text style={styles.tileTitle}>{title}</Text>
    </Pressable>
  );
}

export default function HomePage() {
  const router = useRouter();
  const { signOut } = useSession();
  const { loading, enrollment, snapshot, error, refresh, session } = useTeamSnapshot();
  const config = useMemo(() => getClientConfig(), []);

  const connectionState = useSessionManagementConnection({
    accessToken: session?.accessToken ?? "",
    hubUrl: config.sessionHubUrl,
    onResync: () => {
      void refresh();
    }
  });

  const progress = useMemo(() => buildStageProgress(snapshot), [snapshot]);

  if (loading) {
    return <LoadingScreen message="Preparando tu misión..." />;
  }

  const connection = resolveConnection(connectionState.kind);
  const sessionState = resolveSessionState(snapshot?.sessionState);
  const currentStage = snapshot?.currentStage;
  const isFinalized = snapshot?.sessionState === "Finalized";
  const stagePoints = currentStage ? basePointsForDifficulty(currentStage.difficulty) : null;

  return (
    <ScreenShell
      eyebrow={`Hola, ${session?.displayName ?? "participante"}`}
      title="Tu próxima aventura UMBRAL"
      description="Resuelve etapas de trivia y búsqueda de tesoro, desbloquea pistas y escala en el ranking de tu sesión."
    >
      <View style={styles.statusRow}>
        <StatusChip label={connection.label} tone={connection.tone} />
        {enrollment ? <StatusChip label={sessionState.label} tone={sessionState.tone} /> : null}
      </View>

      {error ? (
        <View style={shellStyles.card}>
          <StatusChip label="No pudimos sincronizar" tone="error" />
          <Text style={shellStyles.cardText}>{error}</Text>
          <GameButton label="Reintentar" variant="ghost" icon="↻" onPress={() => void refresh()} />
        </View>
      ) : null}

      {!enrollment ? (
        <View style={styles.heroCard}>
          <Text style={styles.heroEmoji}>🎯</Text>
          <Text style={styles.heroTitle}>Aún no estás en una sesión</Text>
          <Text style={shellStyles.cardText}>
            Pide el código de tu sesión al operador y únete a tu equipo para empezar a jugar.
          </Text>
          <GameButton
            label="Unirse a una sesión"
            icon="🚪"
            onPress={() => router.push("/mobile/join")}
          />
        </View>
      ) : (
        <View style={styles.heroCard}>
          <View style={shellStyles.row}>
            <StatusChip label={enrollment.teamName} tone="info" />
            {currentStage ? (
              <StatusChip label={formatGameType(currentStage.gameType)} tone="success" />
            ) : null}
          </View>

          {currentStage ? (
            <>
              <Text style={styles.stageEyebrow}>
                Etapa {currentStage.sessionStageOrder}
                {currentStage.difficulty ? ` · ${formatDifficulty(currentStage.difficulty)}` : ""}
                {stagePoints ? ` · ${stagePoints} pts` : ""}
              </Text>
              <Text style={styles.heroTitle}>{currentStage.name}</Text>
              {currentStage.prompt ? (
                <Text style={styles.prompt} numberOfLines={3}>
                  {currentStage.prompt}
                </Text>
              ) : null}
              <GameButton
                label={isFinalized ? "Ver resultados" : "Continuar misión"}
                icon={isFinalized ? "🏁" : isTreasureHunt(currentStage.gameType) ? "🧭" : "🧩"}
                onPress={() => router.push("/mobile/board")}
              />
            </>
          ) : (
            <>
              <Text style={styles.heroTitle}>
                {isFinalized ? "¡Sesión finalizada!" : "Esperando tu próxima etapa"}
              </Text>
              <Text style={shellStyles.cardText}>
                {isFinalized
                  ? "Revisa el tablero para ver las soluciones reveladas y tu posición final."
                  : "El operador aún no habilita una etapa jugable para tu equipo."}
              </Text>
              <GameButton
                label={isFinalized ? "Ver resultados" : "Abrir tablero"}
                icon={isFinalized ? "🏁" : "🎮"}
                onPress={() => router.push("/mobile/board")}
              />
            </>
          )}

          {progress.nodes.length ? (
            <ProgressBar
              completed={progress.completed}
              total={progress.total}
              caption={
                progress.total > 0
                  ? `${progress.completed}/${progress.total} etapas`
                  : `${progress.completed} etapas superadas`
              }
            />
          ) : null}
        </View>
      )}

      {enrollment ? (
        <View style={styles.tileRow}>
          <HubTile icon="🗺️" title="Progreso" onPress={() => router.push("/mobile/progress")} />
          <HubTile icon="💡" title="Pistas" onPress={() => router.push("/mobile/resolutions")} />
          <HubTile icon="🏆" title="Ranking" onPress={() => router.push("/mobile/ranking")} />
        </View>
      ) : null}

      <Pressable
        onPress={() => {
          void signOut().then(() => router.replace("/mobile/login"));
        }}
        style={({ pressed }) => [styles.signOut, pressed && styles.tilePressed]}
      >
        <Text style={styles.signOutLabel}>Cerrar sesión</Text>
      </Pressable>
    </ScreenShell>
  );
}

const styles = StyleSheet.create({
  statusRow: {
    flexDirection: "row",
    flexWrap: "wrap",
    gap: 10
  },
  heroCard: {
    backgroundColor: "#FFFFFF",
    borderColor: "#E5E5E5",
    borderRadius: 26,
    borderWidth: 1,
    gap: 14,
    padding: 22
  },
  heroEmoji: {
    fontSize: 40
  },
  heroTitle: {
    color: "#4B4B4B",
    fontSize: 26,
    fontWeight: "900",
    lineHeight: 32
  },
  stageEyebrow: {
    color: "#1CB0F6",
    fontSize: 13,
    fontWeight: "800",
    letterSpacing: 0.6,
    textTransform: "uppercase"
  },
  prompt: {
    color: "#777777",
    fontSize: 16,
    lineHeight: 23
  },
  tileRow: {
    flexDirection: "row",
    gap: 12
  },
  tile: {
    alignItems: "center",
    backgroundColor: "#FFFFFF",
    borderColor: "#E5E5E5",
    borderRadius: 20,
    borderWidth: 1,
    flex: 1,
    gap: 8,
    paddingHorizontal: 8,
    paddingVertical: 18
  },
  tilePressed: {
    opacity: 0.85
  },
  tileIcon: {
    fontSize: 26
  },
  tileTitle: {
    color: "#4B4B4B",
    fontSize: 14,
    fontWeight: "800"
  },
  signOut: {
    alignItems: "center",
    paddingVertical: 14
  },
  signOutLabel: {
    color: "#EA2B2B",
    fontSize: 15,
    fontWeight: "700"
  }
});
