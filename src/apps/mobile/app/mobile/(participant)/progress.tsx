import { Redirect } from "expo-router";
import { useMemo } from "react";
import { Text, View } from "react-native";
import { LoadingScreen } from "../../../src/components/loading-screen";
import { ProgressBar } from "../../../src/components/progress-bar";
import { ProgressTrack } from "../../../src/components/progress-track";
import { ScreenShell, shellStyles } from "../../../src/components/screen-shell";
import { StatusChip } from "../../../src/components/status-chip";
import { buildStageProgress } from "../../../src/lib/stage-progress";
import { useTeamSnapshot } from "../../../src/hooks/use-team-snapshot";

export default function ProgressPage() {
  const { loading, enrollment, snapshot, error } = useTeamSnapshot();
  const progress = useMemo(() => buildStageProgress(snapshot), [snapshot]);

  if (loading) {
    return <LoadingScreen message="Cargando tu progreso..." />;
  }

  if (!enrollment) {
    return <Redirect href="/mobile/join" />;
  }

  const hintsUnlocked = snapshot?.visibleHints.filter((hint) => !hint.isSolution).length ?? 0;
  const isFinalized = snapshot?.sessionState === "Finalized";

  return (
    <ScreenShell
      eyebrow="Tu camino"
      title="Avanza etapa por etapa"
      description="Cada nodo es una etapa de la misión. Completa la actual para desbloquear la siguiente."
    >
      <View style={shellStyles.card}>
        <ProgressBar
          completed={progress.completed}
          total={progress.total}
          caption={
            progress.total > 0
              ? `${progress.completed} de ${progress.total} etapas completadas`
              : `${progress.completed} etapas superadas`
          }
        />
        <View style={shellStyles.row}>
          <StatusChip label={`${hintsUnlocked} pistas activas`} tone="warn" />
          {isFinalized ? <StatusChip label="Misión finalizada" tone="success" /> : null}
        </View>
      </View>

      {error ? (
        <View style={shellStyles.card}>
          <StatusChip label="No pudimos sincronizar" tone="error" />
          <Text style={shellStyles.cardText}>{error}</Text>
        </View>
      ) : null}

      {progress.nodes.length ? (
        <View style={shellStyles.section}>
          <ProgressTrack nodes={progress.nodes} />
          {progress.total === 0 ? (
            <Text style={shellStyles.cardText}>
              El mapa completo de la misión se revela al avanzar y, definitivamente, al finalizar la
              sesión.
            </Text>
          ) : null}
        </View>
      ) : (
        <View style={shellStyles.card}>
          <StatusChip label="Sin etapa activa" tone="info" />
          <Text style={shellStyles.cardText}>
            Tu equipo aún no tiene una etapa jugable. Aparecerá aquí cuando el operador la habilite.
          </Text>
        </View>
      )}
    </ScreenShell>
  );
}
