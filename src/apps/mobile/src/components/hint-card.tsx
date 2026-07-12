import { StyleSheet, Text, View } from "react-native";
import { HintMap } from "./hint-map";
import { StatusChip } from "./status-chip";
import { colors } from "../theme/tokens";

type HintCardProps = {
  content: string;
  reason: string;
  isSolution?: boolean;
  latitude?: number;
  longitude?: number;
};

function hasCoordinates(latitude?: number, longitude?: number) {
  return typeof latitude === "number" && typeof longitude === "number";
}

export function HintCard({ content, reason, isSolution = false, latitude, longitude }: HintCardProps) {
  return (
    <View style={isSolution ? styles.solutionCard : styles.hintCard}>
      <View style={styles.header}>
        <StatusChip label={isSolution ? "Solución" : "Pista"} tone={isSolution ? "warn" : "info"} />
        <StatusChip label={reason} tone="neutral" />
      </View>
      <Text style={styles.content}>{content}</Text>
      {hasCoordinates(latitude, longitude) ? (
        <HintMap latitude={latitude as number} longitude={longitude as number} />
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  hintCard: {
    backgroundColor: colors.surface.card,
    borderColor: colors.surface.cardBorder,
    borderRadius: 18,
    borderWidth: 1,
    gap: 10,
    padding: 14
  },
  solutionCard: {
    backgroundColor: colors.state.warn.fillAlt,
    borderColor: colors.state.warn.textAlt,
    borderRadius: 18,
    borderWidth: 2,
    gap: 10,
    padding: 14
  },
  header: {
    flexDirection: "row",
    flexWrap: "wrap",
    gap: 10
  },
  content: {
    color: colors.text.primary,
    fontSize: 16,
    fontWeight: "600",
    lineHeight: 23
  }
});
