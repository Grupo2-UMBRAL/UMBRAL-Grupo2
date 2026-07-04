import { useEffect, useRef } from "react";
import { Animated, Easing, Modal, Pressable, StyleSheet, Text, View } from "react-native";
import { GameButton } from "./game-button";
import { Mascot } from "./mascot";
import { colors } from "../theme/tokens";

/**
 * Duolingo-style answer moment: a bottom sheet that slides up and either bounces (correct,
 * with a points badge) or shakes (wrong). Driven entirely by the `feedback` prop the screen
 * already sets — no new wire calls. Web-first, so animations run on the JS driver.
 */
export type AnswerFeedback = {
  tone: "success" | "error";
  title: string;
  detail: string;
  points?: number | null;
};

type AnswerFeedbackSheetProps = {
  feedback: AnswerFeedback | null;
  onContinue: () => void;
};

export function AnswerFeedbackSheet({ feedback, onContinue }: AnswerFeedbackSheetProps) {
  const translateY = useRef(new Animated.Value(280)).current;
  const shake = useRef(new Animated.Value(0)).current;
  const pop = useRef(new Animated.Value(0.6)).current;

  useEffect(() => {
    if (!feedback) {
      return;
    }

    translateY.setValue(280);
    shake.setValue(0);
    pop.setValue(0.6);

    Animated.spring(translateY, {
      toValue: 0,
      useNativeDriver: false,
      damping: 15,
      stiffness: 160,
      mass: 0.9
    }).start();

    if (feedback.tone === "success") {
      Animated.spring(pop, { toValue: 1, useNativeDriver: false, damping: 6, stiffness: 190 }).start();
    } else {
      Animated.sequence(
        [10, -10, 8, -8, 0].map((toValue) =>
          Animated.timing(shake, { toValue, duration: 55, useNativeDriver: false, easing: Easing.linear })
        )
      ).start();
    }
  }, [feedback, translateY, shake, pop]);

  if (!feedback) {
    return null;
  }

  const isSuccess = feedback.tone === "success";

  return (
    <Modal transparent visible animationType="fade" onRequestClose={onContinue}>
      <View style={styles.overlay}>
        <Pressable style={styles.backdrop} onPress={onContinue} />
        <Animated.View
          style={[
            styles.sheet,
            isSuccess ? styles.sheetSuccess : styles.sheetError,
            { transform: [{ translateY }, { translateX: shake }] }
          ]}
        >
          <Animated.View style={{ transform: [{ scale: isSuccess ? pop : 1 }] }}>
            <Mascot mood={isSuccess ? "celebrate" : "sad"} size={92} />
          </Animated.View>

          <View style={styles.copy}>
            <View style={styles.titleRow}>
              <Text style={[styles.title, { color: isSuccess ? colors.brand.primaryStrong : colors.state.error.text }]}>
                {feedback.title}
              </Text>
              {isSuccess && feedback.points ? (
                <View style={styles.pointsBadge}>
                  <Text style={styles.pointsText}>+{feedback.points}</Text>
                </View>
              ) : null}
            </View>
            <Text style={styles.detail}>{feedback.detail}</Text>
          </View>

          <GameButton label="Continuar" variant={isSuccess ? "primary" : "secondary"} onPress={onContinue} />
        </Animated.View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  overlay: {
    flex: 1,
    justifyContent: "flex-end"
  },
  backdrop: {
    ...StyleSheet.absoluteFillObject,
    backgroundColor: "rgba(0, 0, 0, 0.35)"
  },
  sheet: {
    alignItems: "center",
    borderTopLeftRadius: 28,
    borderTopRightRadius: 28,
    borderWidth: 2,
    gap: 14,
    paddingBottom: 32,
    paddingHorizontal: 24,
    paddingTop: 22
  },
  sheetSuccess: {
    backgroundColor: colors.state.success.fill,
    borderColor: colors.brand.primary
  },
  sheetError: {
    backgroundColor: colors.state.error.fill,
    borderColor: colors.state.error.border
  },
  copy: {
    alignItems: "center",
    gap: 6
  },
  titleRow: {
    alignItems: "center",
    flexDirection: "row",
    gap: 10
  },
  title: {
    fontSize: 26,
    fontWeight: "900",
    textAlign: "center"
  },
  pointsBadge: {
    backgroundColor: colors.brand.primary,
    borderRadius: 999,
    paddingHorizontal: 12,
    paddingVertical: 4
  },
  pointsText: {
    color: colors.text.onBrand,
    fontSize: 16,
    fontWeight: "900"
  },
  detail: {
    color: colors.text.primary,
    fontSize: 16,
    fontWeight: "600",
    lineHeight: 23,
    textAlign: "center"
  }
});
