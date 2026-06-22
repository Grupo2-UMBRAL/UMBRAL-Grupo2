import { Redirect } from "expo-router";
import { useMemo } from "react";
import { Text, View } from "react-native";
import { HintCard } from "../../../src/components/hint-card";
import { LoadingScreen } from "../../../src/components/loading-screen";
import { ScreenShell, shellStyles } from "../../../src/components/screen-shell";
import { StatusChip } from "../../../src/components/status-chip";
import { useTeamSnapshot } from "../../../src/hooks/use-team-snapshot";

export default function ResolutionsPage() {
  const { loading, enrollment, snapshot, error } = useTeamSnapshot();

  const activeHints = useMemo(
    () => (snapshot?.visibleHints ?? []).filter((hint) => !hint.isSolution),
    [snapshot?.visibleHints]
  );
  const solutions = useMemo(
    () => (snapshot?.visibleHints ?? []).filter((hint) => hint.isSolution),
    [snapshot?.visibleHints]
  );

  if (loading) {
    return <LoadingScreen message="Cargando pistas..." />;
  }

  if (!enrollment) {
    return <Redirect href="/mobile/join" />;
  }

  const isFinalized = snapshot?.sessionState === "Finalized";

  return (
    <ScreenShell
      eyebrow="Pistas y soluciones"
      title="Lo que tu equipo ha desbloqueado"
      description="Aquí se acumulan las pistas liberadas durante el juego. Las soluciones se revelan al finalizar la sesión."
    >
      {error ? (
        <View style={shellStyles.card}>
          <StatusChip label="No pudimos sincronizar" tone="error" />
          <Text style={shellStyles.cardText}>{error}</Text>
        </View>
      ) : null}

      <View style={shellStyles.section}>
        <Text style={shellStyles.cardTitle}>Pistas activas</Text>
        {activeHints.length ? (
          activeHints.map((hint) => (
            <HintCard
              key={hint.hintId}
              content={hint.content}
              reason={hint.unlockReason}
              latitude={hint.latitude}
              longitude={hint.longitude}
            />
          ))
        ) : (
          <View style={shellStyles.card}>
            <StatusChip label="Sin pistas todavía" tone="warn" />
            <Text style={shellStyles.cardText}>
              Cuando el operador libere una pista para tu equipo, aparecerá aquí al instante.
            </Text>
          </View>
        )}
      </View>

      <View style={shellStyles.section}>
        <Text style={shellStyles.cardTitle}>Soluciones reveladas</Text>
        {isFinalized && solutions.length ? (
          solutions.map((solution) => (
            <HintCard
              key={solution.hintId}
              content={solution.content}
              reason={solution.unlockReason}
              isSolution
              latitude={solution.latitude}
              longitude={solution.longitude}
            />
          ))
        ) : (
          <View style={shellStyles.card}>
            <StatusChip label={isFinalized ? "Sin soluciones" : "Bloqueadas"} tone="neutral" />
            <Text style={shellStyles.cardText}>
              {isFinalized
                ? "Esta sesión no registró soluciones reveladas."
                : "Las soluciones se revelan automáticamente cuando la sesión finaliza."}
            </Text>
          </View>
        )}
      </View>
    </ScreenShell>
  );
}
