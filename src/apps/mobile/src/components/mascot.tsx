import { useEffect, useRef } from "react";
import { Animated, Easing, StyleSheet, View } from "react-native";
import { colors } from "../theme/tokens";

/**
 * Umbral's companion: the rounded "spark" holding a magnifier up to one eye, built only from the
 * shape-language primitives (rounded square body, circle lens + hand, pill eyes, half-disc mouth).
 * No pointy corners, no gradients, no emoji. Expression is carried by color + eyes + mouth so the
 * same component covers every player moment (lobby, celebration, wrong answer, empty state).
 * See docs/product/mobile-shape-language.md §1, §3, §12.
 *
 * Everything is positioned BY CENTRE, in fractions of `size`, so the face cannot drift: a single
 * `HALF_GAP` moves the eye pair, and the lens sits opposite the visible eye by construction.
 *
 * The wink animates HEIGHT with a fixed borderRadius (circle → pill), never `scaleY`: squashing a
 * circle yields an oval, and §12 forbids ovals.
 */
export type MascotMood = "happy" | "celebrate" | "waiting" | "sad" | "curious";

type MascotProps = {
  mood?: MascotMood;
  size?: number;
  /**
   * Cuando el padre pasa este valor, el guiño deja de ser un loop propio y ocurre UNA vez en cada
   * cambio del número (y al montar). Sirve para clavar el guiño al inicio de cada página del
   * tutorial en vez de dispararlo al azar. Sin este prop, la mascota guiña en loop como siempre.
   */
  winkSignal?: number;
};

const bodyColor: Record<MascotMood, string> = {
  happy: colors.brand.primary,
  celebrate: colors.brand.primary,
  waiting: colors.brand.secondary,
  sad: colors.state.error.textAlt,
  curious: colors.state.warn.textAlt
};

/** Flat darker tint — the only depth cue §3 allows. */
const strongColor: Record<MascotMood, string> = {
  happy: colors.brand.primaryStrong,
  celebrate: colors.brand.primaryStrong,
  waiting: colors.brand.secondaryRing,
  sad: colors.state.error.text,
  curious: colors.state.warn.text
};

const BODY_CX = 0.56;
const BODY_CY = 0.46;
const BODY_SIDE = 0.7;

/** Half the distance between the two eyes. The lens occupies the left one. */
const HALF_GAP = 0.17;
const EYE_CY = 0.36;

/**
 * The lens sits a little further left than the left eye's slot, so it clears the body and reads as
 * a held tool rather than a monocle. Deliberate, bounded asymmetry — pushing it makes the face
 * look like it is staring off-frame.
 */
const LENS_EXTRA_LEFT = 0.04;
const LENS_CX = BODY_CX - HALF_GAP - LENS_EXTRA_LEFT;
const EYE_CX = BODY_CX + HALF_GAP;
const LENS_D = 0.42;

/** Resting lean, and how far it leans into the wink. Beyond -18° the head clips its own box. */
const REST_TILT = -8;
const WINK_TILT = -18;

/** Cabeza ladeada al lado contrario: el gesto clásico de duda. No guiña; sostiene la mirada. */
const CURIOUS_TILT = 12;

/** Handle angle from vertical. At 45° the rotated bounding box overflowed to the left. */
const HANDLE_ANGLE = 25;

export function Mascot({ mood = "happy", size = 96, winkSignal }: MascotProps) {
  const isWaiting = mood === "waiting";
  const isSad = mood === "sad";
  const isCurious = mood === "curious";
  // `waiting` already has both eyes shut, and a cheerful wink on top of `sad` contradicts whatever
  // the screen is saying (a failed answer, a forbidden route).
  const winks = mood === "happy" || mood === "celebrate";
  const wink = useRef(new Animated.Value(0)).current;

  useEffect(() => {
    if (!winks) {
      wink.setValue(0);
      return;
    }

    // Un solo guiño, misma coreografía que el loop pero sin el compás de espera. Reutilizado por el
    // modo señal (guiña al montar y en cada cambio de `winkSignal`) y como cuerpo del loop.
    const beat = () =>
      Animated.sequence([
        Animated.timing(wink, { toValue: 1, duration: 220, easing: Easing.out(Easing.quad), useNativeDriver: false }),
        Animated.delay(200),
        Animated.timing(wink, { toValue: 0, duration: 300, easing: Easing.inOut(Easing.quad), useNativeDriver: false })
      ]);

    // Modo señal: guiña YA en cada cambio del valor (inicio de página del tutorial) y luego sigue
    // guiñando cada 3s hasta el próximo cambio. El cleanup del efecto corta el loop viejo al cambiar.
    if (winkSignal !== undefined) {
      const anim = Animated.sequence([
        beat(),
        Animated.loop(Animated.sequence([Animated.delay(3000), beat()]))
      ]);
      anim.start();
      return () => anim.stop();
    }

    // Generous timings on purpose: the same value drives the eyelid AND the body, and a 90ms close
    // that is right for an eye turns the lean into a jolt.
    const loop = Animated.loop(Animated.sequence([Animated.delay(2400), beat()]));

    loop.start();
    return () => loop.stop();
  }, [winks, wink, winkSignal]);

  /** Box centred on (cx, cy), in fractions of `size`. */
  const at = (cx: number, cy: number, w: number, h: number) => ({
    position: "absolute" as const,
    left: (cx - w / 2) * size,
    top: (cy - h / 2) * size,
    width: w * size,
    height: h * size
  });

  const radians = (HANDLE_ANGLE * Math.PI) / 180;
  const lensR = LENS_D / 2;
  const handleLen = 0.26;
  const reach = lensR + handleLen / 2;

  const lensEye = size * 0.18;
  const lensEyeHeight = winks
    ? wink.interpolate({ inputRange: [0, 1], outputRange: [lensEye, lensEye * 0.16] })
    : isWaiting
      ? size * 0.05
      : lensEye;

  // The lean hangs off the SAME value as the wink, so they cannot drift apart. With no wink the
  // value stays at 0 and this resolves to the resting lean.
  const tilt = isCurious
    ? `${CURIOUS_TILT}deg`
    : wink.interpolate({
        inputRange: [0, 1],
        outputRange: [`${REST_TILT}deg`, `${WINK_TILT}deg`]
      });

  return (
    <View style={{ width: size, height: size }}>
      {mood === "celebrate" ? (
        <>
          <View style={[styles.spark, { ...at(0.86, 0.08, 0.1, 0.1), borderRadius: size, backgroundColor: colors.brand.secondary }]} />
          <View style={[styles.spark, { ...at(0.96, 0.22, 0.07, 0.07), borderRadius: size, backgroundColor: colors.state.warn.textAlt }]} />
        </>
      ) : null}

      {/* Body + eye + mouth lean together; the magnifier stays upright and he leans into it. */}
      <Animated.View style={[StyleSheet.absoluteFill, { transform: [{ rotate: tilt }] }]}>
        <View
          style={{
            ...at(BODY_CX, BODY_CY, BODY_SIDE, BODY_SIDE),
            borderRadius: size * 0.29,
            backgroundColor: bodyColor[mood]
          }}
        />

        <View
          style={{
            ...at(EYE_CX, EYE_CY, 0.14, isWaiting ? 0.06 : 0.14),
            borderRadius: size,
            backgroundColor: colors.text.onBrand,
            alignItems: "center",
            justifyContent: "center"
          }}
        >
          {isWaiting ? null : (
            <View
              style={{
                width: size * 0.07,
                height: size * 0.07,
                borderRadius: size,
                backgroundColor: colors.text.primary,
                marginTop: isSad ? size * 0.028 : 0,
                // Curious mira de reojo hacia la lupa: pupila corrida al lado, no al frente.
                marginLeft: isCurious ? -size * 0.035 : 0
              }}
            />
          )}
        </View>

        <View
          style={{
            // Curious: boca cerrada = barra fina con radio pleno (pastilla). Sad frunce (radio
            // arriba), el resto sonríe (radio abajo, media luna).
            ...at(BODY_CX, BODY_CY + 0.14, isCurious ? 0.2 : 0.24, isCurious ? 0.045 : 0.12),
            backgroundColor: colors.text.primary,
            borderTopLeftRadius: isCurious ? size : isSad ? size * 0.12 : 0,
            borderTopRightRadius: isCurious ? size : isSad ? size * 0.12 : 0,
            borderBottomLeftRadius: isCurious ? size : isSad ? 0 : size * 0.12,
            borderBottomRightRadius: isCurious ? size : isSad ? 0 : size * 0.12
          }}
        />
      </Animated.View>

      {/* Handle, tangent to the rim. */}
      <View
        style={{
          ...at(LENS_CX - reach * Math.sin(radians), EYE_CY + reach * Math.cos(radians), 0.1, handleLen),
          borderRadius: size * 0.05,
          backgroundColor: strongColor[mood],
          transform: [{ rotate: `${HANDLE_ANGLE}deg` }]
        }}
      />

      {/* Hand: a circle on the rim, no fingers (§12). */}
      <View
        style={{
          ...at(LENS_CX - lensR * Math.sin(radians), EYE_CY + lensR * Math.cos(radians), 0.13, 0.13),
          borderRadius: size,
          backgroundColor: strongColor[mood]
        }}
      />

      {/* The glass: the left eye lives in here, magnified, and it is the one that winks. */}
      <View
        style={{
          ...at(LENS_CX, EYE_CY, LENS_D, LENS_D),
          borderRadius: size,
          borderWidth: Math.max(3, size * 0.05),
          borderColor: strongColor[mood],
          backgroundColor: colors.surface.card,
          alignItems: "center",
          justifyContent: "center"
        }}
      >
        <Animated.View
          style={{
            width: lensEye,
            height: lensEyeHeight,
            borderRadius: lensEye,
            backgroundColor: colors.text.primary,
            marginTop: isSad ? size * 0.03 : 0
          }}
        />
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  spark: {
    position: "absolute"
  }
});
