import { StyleSheet, Text, View } from "react-native";
import { colors } from "../theme/tokens";

export type StageNodeState = "completed" | "current" | "locked";

export type StageNode = {
  key: string;
  label: string;
  sublabel?: string;
  state: StageNodeState;
};

type ProgressTrackProps = {
  nodes: StageNode[];
};

const nodeTheme: Record<StageNodeState, { dot: string; ring: string; label: string; glyph: string }> = {
  completed: { dot: colors.brand.primary, ring: colors.brand.primaryRing, label: colors.text.primary, glyph: "✓" },
  current: { dot: colors.brand.secondary, ring: colors.brand.secondaryRing, label: colors.text.primary, glyph: "★" },
  locked: { dot: colors.state.locked.ring, ring: colors.state.locked.fill, label: colors.state.locked.text, glyph: "🔒" }
};

export function ProgressTrack({ nodes }: ProgressTrackProps) {
  return (
    <View style={styles.container}>
      {nodes.map((node, index) => {
        const theme = nodeTheme[node.state];
        const isLast = index === nodes.length - 1;

        return (
          <View key={node.key} style={styles.row}>
            <View style={styles.rail}>
              <View
                style={[styles.dot, { backgroundColor: theme.dot, borderColor: theme.ring }]}
              >
                <Text style={styles.glyph}>{theme.glyph}</Text>
              </View>
              {isLast ? null : (
                <View
                  style={[
                    styles.connector,
                    { backgroundColor: node.state === "completed" ? colors.brand.primaryRing : colors.state.locked.fill }
                  ]}
                />
              )}
            </View>
            <View
              style={[
                styles.card,
                node.state === "current" && styles.currentCard,
                node.state === "locked" && styles.lockedCard
              ]}
            >
              <Text style={[styles.label, { color: theme.label }]}>{node.label}</Text>
              {node.sublabel ? <Text style={styles.sublabel}>{node.sublabel}</Text> : null}
            </View>
          </View>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    gap: 0
  },
  row: {
    flexDirection: "row",
    gap: 14
  },
  rail: {
    alignItems: "center",
    width: 40
  },
  dot: {
    alignItems: "center",
    borderRadius: 20,
    borderWidth: 3,
    height: 40,
    justifyContent: "center",
    width: 40
  },
  glyph: {
    color: colors.text.onBrand,
    fontSize: 16,
    fontWeight: "800"
  },
  connector: {
    borderRadius: 2,
    flex: 1,
    minHeight: 26,
    width: 4
  },
  card: {
    backgroundColor: colors.surface.card,
    borderColor: colors.surface.cardBorder,
    borderRadius: 18,
    borderWidth: 1,
    flex: 1,
    gap: 4,
    marginBottom: 14,
    padding: 14
  },
  currentCard: {
    borderColor: colors.brand.secondary,
    borderWidth: 2,
    backgroundColor: colors.state.info.fillAlt
  },
  lockedCard: {
    backgroundColor: colors.state.locked.cardFill,
    borderColor: colors.state.locked.fill
  },
  label: {
    fontSize: 16,
    fontWeight: "800"
  },
  sublabel: {
    color: colors.text.muted,
    fontSize: 13,
    lineHeight: 19
  }
});
