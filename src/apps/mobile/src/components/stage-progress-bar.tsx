import { StyleSheet, Text, View } from "react-native";
import { colors } from "../theme/tokens";

/**
 * Duolingo-style linear stage progress: one rounded segment per mission stage, filled for
 * completed stages, highlighted for the current one, muted for the ones still locked. Needs the
 * mission's total stage count (from the snapshot); renders nothing without it.
 */
type StageProgressBarProps = {
  total: number;
  currentOrder: number;
};

export function StageProgressBar({ total, currentOrder }: StageProgressBarProps) {
  if (!total || total < 1) {
    return null;
  }

  const completed = Math.max(0, Math.min(total, currentOrder - 1));
  const safeCurrent = currentOrder > 0 ? Math.min(currentOrder, total) : 0;

  return (
    <View style={styles.container}>
      <View style={styles.segments}>
        {Array.from({ length: total }).map((_, index) => {
          const order = index + 1;
          const isDone = order <= completed;
          const isCurrent = order === currentOrder;

          return (
            <View
              key={order}
              style={[
                styles.segment,
                isDone && styles.done,
                isCurrent && styles.current,
                !isDone && !isCurrent && styles.locked
              ]}
            />
          );
        })}
      </View>
      <Text style={styles.caption}>
        {safeCurrent > 0 ? `Etapa ${safeCurrent} de ${total}` : `${total} etapas por delante`}
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    gap: 6
  },
  segments: {
    flexDirection: "row",
    gap: 5
  },
  segment: {
    borderRadius: 999,
    flex: 1,
    height: 12
  },
  done: {
    backgroundColor: colors.brand.primary
  },
  current: {
    backgroundColor: colors.brand.secondary
  },
  locked: {
    backgroundColor: colors.state.locked.fill
  },
  caption: {
    color: colors.text.secondary,
    fontSize: 13,
    fontWeight: "700"
  }
});
