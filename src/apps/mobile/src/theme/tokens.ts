/**
 * Design tokens for the UMBRAL mobile client.
 * Source of truth: docs/product/mobile-shape-language.md
 */

export const colors = {
  surface: {
    base: "#f6efe6",
    card: "#fffaf5",
    cardBorder: "#eadcc8",
    raised: "#f2f6f7",
    raisedBorder: "#c8d7dc",
    mapFrame: "#dcecef"
  },
  text: {
    primary: "#17313b",
    secondary: "#4d5e64",
    tertiary: "#40616d",
    muted: "#6b7a72",
    mutedAlt: "#8a978f",
    onBrand: "#f7fbfc"
  },
  brand: {
    primary: "#2d6a4f",
    primaryTint: "#d8f3dc",
    primaryTintAlt: "#eef5f1",
    primaryStrong: "#1b4332",
    primaryRing: "#74c69d",
    secondary: "#1e6f8c",
    secondaryTint: "#d8ecf5",
    secondaryTintAlt: "#eaf6fa",
    secondaryRing: "#9cd0e2"
  },
  state: {
    success: { fill: "#dff2dd", fillAlt: "#d8f3dc", text: "#25613a", textAlt: "#2d6a4f" },
    info: { fill: "#d8ecf5", fillAlt: "#eef7fb", text: "#175f78", textAlt: "#1e6f8c" },
    warn: {
      fill: "#f9e8c7",
      fillAlt: "#fff1df",
      fillMuted: "#f2e7de",
      text: "#8a5d14",
      textAlt: "#d28b39",
      borderMuted: "#d1ab89"
    },
    error: { fill: "#f7d9d9", fillAlt: "#ffe5e5", border: "#ef9a9a", text: "#9e2f2f", textAlt: "#d94f30" },
    neutral: { fill: "#efe7dc", text: "#5f5f55" },
    locked: { fill: "#e4ebe1", ring: "#cdd8c9", text: "#8a978f", cardFill: "#f3f1ec" },
    disabled: { fill: "#9aa6a1", text: "#f7fbfc" }
  },
  overlay: {
    scrim: "#000000",
    text: "#dce8ea"
  }
} as const;

export const radius = {
  sm: 12,
  md: 18,
  lg: 24,
  control: 20,
  pill: 999
} as const;

export const stroke = {
  default: 1,
  emphasis: 2,
  heavy: 3
} as const;

export const space = {
  xs: 4,
  sm: 8,
  md: 12,
  lg: 16,
  xl: 20,
  xxl: 24
} as const;

/**
 * `undefined` falls back to the RN/Expo platform system font. Swap to "Nunito_800ExtraBold" /
 * "Nunito_700Bold" / "Nunito_600SemiBold" once `@expo-google-fonts/nunito` is installed and
 * loaded via `useFonts` — see docs/product/mobile-shape-language.md §10.
 */
export const fontFamily = {
  display: undefined,
  body: undefined
} as const;

export const typography = {
  title: { fontSize: 32, lineHeight: 38, fontWeight: "800" },
  cardTitle: { fontSize: 18, lineHeight: 24, fontWeight: "700" },
  body: { fontSize: 16, lineHeight: 24, fontWeight: "600" },
  caption: { fontSize: 13, lineHeight: 19, fontWeight: "700" },
  eyebrow: { fontSize: 12, fontWeight: "700", letterSpacing: 1.2, textTransform: "uppercase" }
} as const;
