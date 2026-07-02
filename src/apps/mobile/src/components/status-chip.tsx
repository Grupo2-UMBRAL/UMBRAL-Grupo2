import { StyleSheet, Text, View } from "react-native";
import { colors } from "../theme/tokens";

type StatusTone = "neutral" | "info" | "success" | "warn" | "error";

type StatusChipProps = {
  label: string;
  tone?: StatusTone;
};

const toneStyles = {
  neutral: { backgroundColor: colors.state.neutral.fill, color: colors.state.neutral.text },
  info: { backgroundColor: colors.state.info.fill, color: colors.state.info.text },
  success: { backgroundColor: colors.state.success.fill, color: colors.state.success.text },
  warn: { backgroundColor: colors.state.warn.fill, color: colors.state.warn.text },
  error: { backgroundColor: colors.state.error.fill, color: colors.state.error.text }
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
