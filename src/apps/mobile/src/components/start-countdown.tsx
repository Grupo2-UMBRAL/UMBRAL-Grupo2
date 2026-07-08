import { useEffect, useRef, useState } from "react";
import { Animated, Dimensions, Easing, StyleSheet, Text } from "react-native";
import { colors } from "../theme/tokens";

/**
 * Full-screen transition played once when the operator starts the session: a green sweep wipes in,
 * a 3·2·1 countdown pops, then the panel sweeps out to reveal the game board. Purely presentational;
 * the board mounts underneath and is shown the moment `onDone` fires.
 */
type StartCountdownProps = {
  onDone: () => void;
};

const { width: SCREEN_WIDTH } = Dimensions.get("window");
const STEP_MS = 700;

export function StartCountdown({ onDone }: StartCountdownProps) {
  const sweep = useRef(new Animated.Value(0)).current; // 0 = off-screen right, 1 = covering, 2 = off-screen left
  const pop = useRef(new Animated.Value(0)).current;
  const [count, setCount] = useState<number | null>(null);
  const doneRef = useRef(onDone);
  doneRef.current = onDone;

  // Sweep in, then hand off to the countdown.
  useEffect(() => {
    Animated.timing(sweep, {
      toValue: 1,
      duration: 480,
      easing: Easing.out(Easing.cubic),
      useNativeDriver: true
    }).start(() => setCount(3));
  }, [sweep]);

  // Drive 3 -> 2 -> 1, then sweep out.
  useEffect(() => {
    if (count === null) {
      return;
    }

    pop.setValue(0);
    Animated.spring(pop, { toValue: 1, friction: 5, tension: 120, useNativeDriver: true }).start();

    const timer = setTimeout(() => {
      if (count > 1) {
        setCount(count - 1);
        return;
      }
      Animated.timing(sweep, {
        toValue: 2,
        duration: 420,
        easing: Easing.in(Easing.cubic),
        useNativeDriver: true
      }).start(() => doneRef.current());
    }, STEP_MS);

    return () => clearTimeout(timer);
  }, [count, pop, sweep]);

  const translateX = sweep.interpolate({
    inputRange: [0, 1, 2],
    outputRange: [SCREEN_WIDTH, 0, -SCREEN_WIDTH]
  });
  const numberScale = pop.interpolate({ inputRange: [0, 1], outputRange: [0.3, 1] });
  const numberOpacity = pop.interpolate({ inputRange: [0, 0.4, 1], outputRange: [0, 0.7, 1] });

  return (
    <Animated.View pointerEvents="none" style={[styles.panel, { transform: [{ translateX }] }]}>
      {count !== null ? (
        <Animated.Text
          style={[styles.number, { opacity: numberOpacity, transform: [{ scale: numberScale }] }]}
        >
          {count}
        </Animated.Text>
      ) : (
        <Text style={styles.ready}>¡Prepárate!</Text>
      )}
    </Animated.View>
  );
}

const styles = StyleSheet.create({
  panel: {
    alignItems: "center",
    backgroundColor: colors.brand.primary,
    bottom: 0,
    justifyContent: "center",
    left: 0,
    position: "absolute",
    right: 0,
    top: 0,
    zIndex: 50
  },
  number: {
    color: colors.text.onBrand,
    fontSize: 160,
    fontWeight: "900",
    textShadowColor: colors.brand.primaryStrong,
    textShadowOffset: { width: 0, height: 4 },
    textShadowRadius: 0
  },
  ready: {
    color: colors.text.onBrand,
    fontSize: 32,
    fontWeight: "900",
    letterSpacing: 1
  }
});
