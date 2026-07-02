import { ActivityIndicator, Pressable, StyleSheet, Text, View } from "react-native";
import { colors } from "../theme/tokens";

type GameButtonVariant = "primary" | "secondary" | "ghost";

type GameButtonProps = {
  label: string;
  onPress: () => void;
  variant?: GameButtonVariant;
  disabled?: boolean;
  loading?: boolean;
  icon?: string;
};

const palette: Record<GameButtonVariant, { background: string; label: string; border: string }> = {
  primary: { background: colors.brand.primary, label: colors.text.onBrand, border: colors.brand.primary },
  secondary: { background: colors.brand.secondary, label: colors.text.onBrand, border: colors.brand.secondary },
  ghost: { background: "transparent", label: colors.text.primary, border: colors.surface.raisedBorder }
};

export function GameButton({
  label,
  onPress,
  variant = "primary",
  disabled = false,
  loading = false,
  icon
}: GameButtonProps) {
  const tone = palette[variant];
  const isBlocked = disabled || loading;

  return (
    <Pressable
      accessibilityRole="button"
      disabled={isBlocked}
      onPress={onPress}
      style={({ pressed }) => [
        styles.button,
        { backgroundColor: tone.background, borderColor: tone.border },
        isBlocked && styles.blocked,
        pressed && !isBlocked && styles.pressed
      ]}
    >
      <View style={styles.content}>
        {loading ? (
          <ActivityIndicator color={tone.label} />
        ) : icon ? (
          <Text style={[styles.icon, { color: tone.label }]}>{icon}</Text>
        ) : null}
        <Text style={[styles.label, { color: tone.label }]}>{label}</Text>
      </View>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  button: {
    alignItems: "center",
    borderRadius: 20,
    borderWidth: 1.5,
    justifyContent: "center",
    minHeight: 56,
    paddingHorizontal: 18,
    paddingVertical: 14
  },
  content: {
    alignItems: "center",
    flexDirection: "row",
    gap: 10
  },
  icon: {
    fontSize: 18
  },
  label: {
    fontSize: 16,
    fontWeight: "800",
    letterSpacing: 0.2
  },
  blocked: {
    opacity: 0.5
  },
  pressed: {
    opacity: 0.85
  }
});
