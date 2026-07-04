import { StyleSheet, View } from "react-native";
import { colors } from "../theme/tokens";

/**
 * Umbral's friendly companion — a rounded "spark" character built only from the three
 * shape-language primitives (rounded square body, circle/pill eyes, half-disc mouth). No
 * pointy corners, no gradients, no emoji. Expression is carried by color + eyes + mouth so
 * the same component covers every player moment (lobby, celebration, wrong answer, empty state).
 * See docs/product/mobile-shape-language.md §1, §12.
 */
export type MascotMood = "happy" | "celebrate" | "waiting" | "sad";

type MascotProps = {
  mood?: MascotMood;
  size?: number;
};

const bodyColor: Record<MascotMood, string> = {
  happy: colors.brand.primary,
  celebrate: colors.brand.primary,
  waiting: colors.brand.secondary,
  sad: colors.state.error.textAlt
};

export function Mascot({ mood = "happy", size = 96 }: MascotProps) {
  const eye = size * 0.16;
  const pupil = size * 0.08;
  const eyeGap = size * 0.18;
  const isWaiting = mood === "waiting";
  const isSad = mood === "sad";

  return (
    <View style={{ width: size, height: size, alignItems: "center", justifyContent: "center" }}>
      {mood === "celebrate" ? (
        <>
          <View style={[styles.spark, { top: 0, left: size * 0.06, width: size * 0.1, height: size * 0.1, borderRadius: size * 0.05, backgroundColor: colors.brand.secondary }]} />
          <View style={[styles.spark, { top: size * 0.04, right: size * 0.04, width: size * 0.08, height: size * 0.08, borderRadius: size * 0.04, backgroundColor: colors.state.warn.textAlt }]} />
          <View style={[styles.spark, { bottom: size * 0.02, right: size * 0.16, width: size * 0.06, height: size * 0.06, borderRadius: size * 0.03, backgroundColor: colors.state.warn.textAlt }]} />
        </>
      ) : null}

      <View
        style={[
          styles.body,
          {
            width: size * 0.82,
            height: size * 0.82,
            borderRadius: size * 0.34,
            backgroundColor: bodyColor[mood]
          }
        ]}
      >
        <View style={[styles.eyeRow, { gap: eyeGap, marginTop: size * 0.06 }]}>
          {[0, 1].map((key) => (
            <View
              key={key}
              style={{
                width: eye,
                height: isWaiting ? eye * 0.42 : eye,
                borderRadius: eye,
                backgroundColor: colors.text.onBrand,
                alignItems: "center",
                justifyContent: "center"
              }}
            >
              {isWaiting ? null : (
                <View
                  style={{
                    width: pupil,
                    height: pupil,
                    borderRadius: pupil,
                    backgroundColor: colors.text.primary,
                    marginTop: isSad ? pupil * 0.4 : 0
                  }}
                />
              )}
            </View>
          ))}
        </View>

        <View
          style={{
            marginTop: size * 0.1,
            width: size * 0.3,
            height: size * 0.15,
            backgroundColor: colors.text.primary,
            borderTopLeftRadius: isSad ? size * 0.15 : 0,
            borderTopRightRadius: isSad ? size * 0.15 : 0,
            borderBottomLeftRadius: isSad ? 0 : size * 0.15,
            borderBottomRightRadius: isSad ? 0 : size * 0.15
          }}
        />
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  body: {
    alignItems: "center",
    justifyContent: "center"
  },
  eyeRow: {
    flexDirection: "row"
  },
  spark: {
    position: "absolute"
  }
});
