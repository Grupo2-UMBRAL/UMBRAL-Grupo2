import { StyleSheet, Text, View } from "react-native";

type StatusTone = "neutral" | "info" | "success" | "warn" | "error";

type StatusChipProps = {
  label: string;
  tone?: StatusTone;
};

const toneStyles = {
  neutral: { backgroundColor: "#efe7dc", color: "#5f5f55" },
  info: { backgroundColor: "#d8ecf5", color: "#175f78" },
  success: { backgroundColor: "#dff2dd", color: "#25613a" },
  warn: { backgroundColor: "#f9e8c7", color: "#8a5d14" },
  error: { backgroundColor: "#f7d9d9", color: "#9e2f2f" }
} satisfies Record<StatusTone, { backgroundColor: string; color: string }>;

export function StatusChip({ label, tone = "neutral" }: StatusChipProps) {
  const palette = toneStyles[tone];

  return (
    <View style={[styles.container, { backgroundColor: palette.backgroundColor }]}>
      <Text style={[styles.label, { color: palette.color }]}>{label}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    borderRadius: 999,
    paddingHorizontal: 12,
    paddingVertical: 8,
    alignSelf: "flex-start"
  },
  label: {
    fontSize: 12,
    fontWeight: "700"
  }
});
