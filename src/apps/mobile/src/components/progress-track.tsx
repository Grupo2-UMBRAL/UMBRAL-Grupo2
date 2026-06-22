import { StyleSheet, Text, View } from "react-native";

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
  completed: { dot: "#2d6a4f", ring: "#74c69d", label: "#17313b", glyph: "✓" },
  current: { dot: "#1e6f8c", ring: "#9cd0e2", label: "#17313b", glyph: "★" },
  locked: { dot: "#cdd8c9", ring: "#e4ebe1", label: "#8a978f", glyph: "🔒" }
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
                    { backgroundColor: node.state === "completed" ? "#74c69d" : "#e4ebe1" }
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
    color: "#f7fbfc",
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
    backgroundColor: "#fffaf5",
    borderColor: "#eadcc8",
    borderRadius: 18,
    borderWidth: 1,
    flex: 1,
    gap: 4,
    marginBottom: 14,
    padding: 14
  },
  currentCard: {
    borderColor: "#1e6f8c",
    borderWidth: 2,
    backgroundColor: "#eef7fb"
  },
  lockedCard: {
    backgroundColor: "#f3f1ec",
    borderColor: "#e4ebe1"
  },
  label: {
    fontSize: 16,
    fontWeight: "800"
  },
  sublabel: {
    color: "#6b7a72",
    fontSize: 13,
    lineHeight: 19
  }
});
