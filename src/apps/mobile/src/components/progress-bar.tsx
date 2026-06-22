import { StyleSheet, Text, View } from "react-native";

type ProgressBarProps = {
  completed: number;
  total: number;
  caption?: string;
};

export function ProgressBar({ completed, total, caption }: ProgressBarProps) {
  const safeTotal = Math.max(total, 1);
  const ratio = Math.min(1, Math.max(0, completed / safeTotal));
  const percent = `${Math.round(ratio * 100)}%` as const;

  return (
    <View style={styles.container}>
      <View style={styles.track}>
        <View style={[styles.fill, { width: percent }]} />
      </View>
      <Text style={styles.caption}>
        {caption ?? (total > 0 ? `${completed}/${total} etapas` : `${completed} etapas superadas`)}
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    gap: 6
  },
  track: {
    backgroundColor: "#e4ebe1",
    borderRadius: 999,
    height: 14,
    overflow: "hidden"
  },
  fill: {
    backgroundColor: "#2d6a4f",
    borderRadius: 999,
    height: "100%"
  },
  caption: {
    color: "#4d5e64",
    fontSize: 13,
    fontWeight: "700"
  }
});
