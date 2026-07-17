import { useEffect, useRef, useState } from "react";
import { Animated, Easing, StyleSheet, Text, View } from "react-native";
import { Mascot } from "./mascot";
import { colors, radius } from "../theme/tokens";

/**
 * La animación de entrada de la app: la mascota es la heroína (entra con un pop elástico y guiña) y
 * el logo UMBRAL se revela debajo — letras escalonadas + un subrayado que barre de izquierda a
 * derecha, como si la lupa escaneara el nombre. Al terminar la secuencia llama `onDone`.
 *
 * `useNativeDriver: false` por la convención del resto de animaciones del cliente (onboarding,
 * answer-feedback): el web-build las corre igual y evita mezclar drivers.
 */
const WORD = "UMBRAL";

export function EntrySplash({ onDone }: { onDone: () => void }) {
  const pop = useRef(new Animated.Value(0)).current;
  const sweep = useRef(new Animated.Value(0)).current;
  const letters = useRef(WORD.split("").map(() => new Animated.Value(0))).current;
  const [winkSignal, setWinkSignal] = useState(0);

  // `onDone` en un ref: la secuencia lo captura una vez y no queremos re-lanzarla si el padre
  // pasa una función nueva en cada render.
  const done = useRef(onDone);
  done.current = onDone;

  useEffect(() => {
    // La mascota entra con un pop elástico...
    Animated.timing(pop, {
      toValue: 1,
      duration: 640,
      delay: 140,
      easing: Easing.out(Easing.back(1.7)),
      useNativeDriver: false
    }).start();

    // ...aterriza y guiña.
    const winkTimer = setTimeout(() => setWinkSignal((n) => n + 1), 720);

    // El wordmark se escribe letra a letra, luego la "lupa" barre el subrayado y, tras un respiro,
    // cede el paso a la app.
    const sequence = Animated.sequence([
      Animated.delay(720),
      Animated.stagger(
        70,
        letters.map((v) =>
          Animated.timing(v, { toValue: 1, duration: 360, easing: Easing.out(Easing.cubic), useNativeDriver: false })
        )
      ),
      Animated.timing(sweep, { toValue: 1, duration: 520, easing: Easing.inOut(Easing.cubic), useNativeDriver: false }),
      Animated.delay(480)
    ]);

    sequence.start(({ finished }) => {
      if (finished) {
        done.current();
      }
    });

    return () => {
      clearTimeout(winkTimer);
      sequence.stop();
    };
  }, [pop, sweep, letters]);

  const mascotScale = pop.interpolate({ inputRange: [0, 1], outputRange: [0.2, 1] });
  const mascotY = pop.interpolate({ inputRange: [0, 1], outputRange: [24, 0] });

  return (
    <View style={styles.stage}>
      <Animated.View style={{ opacity: pop, transform: [{ scale: mascotScale }, { translateY: mascotY }] }}>
        <View style={styles.halo}>
          <Mascot mood="happy" size={168} winkSignal={winkSignal} />
        </View>
      </Animated.View>

      <View style={styles.wordRow}>
        {WORD.split("").map((ch, i) => (
          <Animated.Text
            key={`${ch}-${i}`}
            style={[
              styles.word,
              {
                opacity: letters[i],
                transform: [{ translateY: letters[i].interpolate({ inputRange: [0, 1], outputRange: [16, 0] }) }]
              }
            ]}
          >
            {ch}
          </Animated.Text>
        ))}
      </View>

      <View style={styles.sweepTrack}>
        <Animated.View
          style={[styles.sweepBar, { width: sweep.interpolate({ inputRange: [0, 1], outputRange: ["0%", "100%"] }) }]}
        />
      </View>

      <Animated.Text style={[styles.tagline, { opacity: sweep }]}>Tu próxima aventura</Animated.Text>
    </View>
  );
}

const styles = StyleSheet.create({
  stage: {
    alignItems: "center",
    backgroundColor: colors.surface.base,
    flex: 1,
    gap: 28,
    justifyContent: "center",
    padding: 24
  },
  halo: {
    alignItems: "center",
    backgroundColor: colors.brand.primaryTint,
    borderRadius: radius.pill,
    height: 244,
    justifyContent: "center",
    width: 244
  },
  wordRow: {
    flexDirection: "row"
  },
  word: {
    color: colors.text.primary,
    fontSize: 44,
    fontWeight: "900",
    letterSpacing: 4
  },
  sweepTrack: {
    backgroundColor: colors.surface.cardBorder,
    borderRadius: radius.pill,
    height: 5,
    overflow: "hidden",
    width: 180
  },
  sweepBar: {
    backgroundColor: colors.brand.primary,
    borderRadius: radius.pill,
    height: 5
  },
  tagline: {
    color: colors.text.secondary,
    fontSize: 15,
    fontWeight: "700",
    letterSpacing: 0.4
  }
});
