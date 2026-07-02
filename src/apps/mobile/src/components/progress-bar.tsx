import { StyleSheet, Text, View } from "react-native";
import { colors } from "../theme/tokens";

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
    backgroundColor: colors.state.locked.fill,
    borderRadius: 999,
    height: 14,
    overflow: "hidden"
  },
  fill: {
    backgroundColor: colors.brand.primary,
    borderRadius: 999,
    height: "100%"
  },
  caption: {
    color: colors.text.secondary,
    fontSize: 13,
    fontWeight: "700"
  }
});
