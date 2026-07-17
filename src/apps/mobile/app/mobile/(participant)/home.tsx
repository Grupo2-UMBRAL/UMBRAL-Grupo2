import { Stack, useRouter } from "expo-router";
import { useEffect, useMemo, useState } from "react";
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
import { OnboardingChecklist, OnboardingTutorial } from "../../../src/components/onboarding";
import {
  loadOnboardingState,
  saveOnboardingState,
  type OnboardingState
} from "../../../src/lib/session-storage";

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

function ProfileHeaderButton({ onPress }: { onPress: () => void }) {
  return (
    <Pressable
      accessibilityLabel="Mi perfil"
      accessibilityRole="button"
      hitSlop={10}
      onPress={onPress}
      style={({ pressed }) => [styles.profileButton, pressed && styles.tilePressed]}
    >
      <View style={styles.profileIcon}>
        <View style={styles.profileHead} />
        <View style={styles.profileBody} />
      </View>
    </Pressable>
  );
}

export default function HomePage() {
  const router = useRouter();
  const { loading, enrollment, snapshot, error, refresh, leave, session } = useTeamSnapshot();
  const config = useMemo(() => getClientConfig(), []);

  const switchSession = () => {
    void leave().then(() => router.push("/mobile/join"));
  };

  const connectionState = useSessionManagementConnection({
    accessToken: session?.accessToken ?? "",
    hubUrl: config.sessionHubUrl,
    onResync: () => {
      void refresh();
    }
  });

  const progress = useMemo(() => buildStageProgress(snapshot), [snapshot]);

  // `null` mientras se lee AsyncStorage: sin ese tercer estado el tutorial parpadea en cada entrada
  // de quien ya lo vio, porque `false` se renderiza antes de saber la respuesta.
  const [onboarding, setOnboarding] = useState<OnboardingState | null>(null);

  useEffect(() => {
    let active = true;

    void loadOnboardingState().then((state) => {
      if (active) {
        setOnboarding(state);
      }
    });

    return () => {
      active = false;
    };
  }, []);

  const closeOnboarding = (patch: Partial<OnboardingState>) => {
    setOnboarding((current) => (current ? { ...current, ...patch } : current));
    void saveOnboardingState(patch);
  };

  if (loading) {
    return <LoadingScreen message="Preparando tu misión..." />;
  }

  const connection = resolveConnection(connectionState.kind);
  const sessionState = resolveSessionState(snapshot?.sessionState);
  const currentStage = snapshot?.currentStage;
  const isFinalized = snapshot?.sessionState === "Finalized";
  const isEnded = isFinalized || snapshot?.sessionState === "Canceled";
  const stagePoints = currentStage ? basePointsForDifficulty(currentStage.difficulty) : null;

  return (
    <View style={styles.root}>
      <Stack.Screen
        options={{
          headerRight: () => <ProfileHeaderButton onPress={() => router.push("/mobile/perfil")} />
        }}
      />
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
                onPress={() => router.push(isFinalized ? "/mobile/ranking" : "/mobile/board")}
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
                onPress={() => router.push(isFinalized ? "/mobile/ranking" : "/mobile/board")}
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
        <>
          <View style={styles.tileRow}>
            <HubTile icon="🗺️" title="Progreso" onPress={() => router.push("/mobile/progress")} />
            <HubTile icon="💡" title="Pistas" onPress={() => router.push("/mobile/resolutions")} />
            <HubTile icon="🏆" title="Ranking" onPress={() => router.push("/mobile/ranking")} />
          </View>
          {isEnded ? (
            <View style={styles.heroCard}>
              <Text style={styles.heroTitle}>¿Lista otra partida?</Text>
              <Text style={shellStyles.cardText}>
                Esta sesión ya terminó. Sal de tu equipo y únete con un nuevo código cuando el
                operador abra otra sesión.
              </Text>
              <GameButton label="Unirse a otra sesión" icon="🚪" onPress={switchSession} />
            </View>
          ) : (
            <Pressable
              onPress={switchSession}
              style={({ pressed }) => [styles.signOut, pressed && styles.tilePressed]}
            >
              <Text style={styles.switchLabel}>Cambiar de sesión</Text>
            </Pressable>
          )}
        </>
      ) : null}
    </ScreenShell>

      {onboarding?.tutorialSeen === false ? (
        <OnboardingTutorial onDone={() => closeOnboarding({ tutorialSeen: true })} />
      ) : null}

      {/* El checklist espera a que el tutorial se cierre: si no, su pill queda debajo del modal. */}
      {onboarding?.tutorialSeen && !onboarding.checklistDismissed ? (
        <OnboardingChecklist onDismiss={() => closeOnboarding({ checklistDismissed: true })} />
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  // Contenedor de posicionamiento para los overlays de entrada, que se dibujan sobre el shell.
  root: {
    flex: 1
  },
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
  // Mismo lenguaje que los tiles: superficie blanca, borde y el labio inferior sólido como única
  // pista de profundidad (§3). Antes era un aro gris suelto que leía como placeholder.
  profileButton: {
    alignItems: "center",
    backgroundColor: "#FFFFFF",
    borderColor: "#E5E5E5",
    borderRadius: 999,
    borderWidth: 1,
    borderBottomColor: "#D9D9D9",
    borderBottomWidth: 3,
    height: 44,
    justifyContent: "center",
    width: 44
  },
  profileIcon: {
    borderColor: "#4B4B4B",
    borderRadius: 999,
    borderWidth: 2,
    height: 26,
    overflow: "hidden",
    position: "relative",
    width: 26
  },
  // El origen de los hijos absolutos cae dentro del borde (caja de 22px), así que se centra en x=11.
  profileHead: {
    borderColor: "#4B4B4B",
    borderRadius: 999,
    borderWidth: 2,
    height: 8,
    left: 7,
    position: "absolute",
    top: 4,
    width: 8
  },
  profileBody: {
    borderColor: "#4B4B4B",
    borderRadius: 999,
    borderWidth: 2,
    height: 10,
    left: 3,
    position: "absolute",
    top: 13,
    width: 16
  },
  switchLabel: {
    color: "#1CB0F6",
    fontSize: 15,
    fontWeight: "700"
  }
});
