/**
 * Design tokens for the UMBRAL mobile client.
 * Source of truth: docs/product/mobile-shape-language.md
 */

export const colors = {
  surface: {
    base: "#F7F7F7",
    card: "#FFFFFF",
    cardBorder: "#E5E5E5",
    raised: "#F7F7F7",
    raisedBorder: "#E5E5E5",
    mapFrame: "#DDF4FF"
  },
  text: {
    primary: "#4B4B4B",
    secondary: "#777777",
    tertiary: "#777777",
    muted: "#777777",
    mutedAlt: "#AFAFAF",
    onBrand: "#FFFFFF"
  },
  brand: {
    primary: "#58CC02",
    primaryTint: "#EAF7D6",
    primaryTintAlt: "#EAF7D6",
    primaryStrong: "#58A700",
    primaryRing: "#58A700",
    secondary: "#1CB0F6",
    secondaryTint: "#DDF4FF",
    secondaryTintAlt: "#DDF4FF",
    secondaryRing: "#1899D6"
  },
  state: {
    success: { fill: "#EAF7D6", fillAlt: "#EAF7D6", text: "#58A700", textAlt: "#58CC02" },
    info: { fill: "#DDF4FF", fillAlt: "#DDF4FF", text: "#1899D6", textAlt: "#1CB0F6" },
    warn: {
      fill: "#FFF4CC",
      fillAlt: "#FFF4CC",
      fillMuted: "#FFF4CC",
      text: "#8C6E00",
      textAlt: "#FFC800",
      borderMuted: "#FFC800"
    },
    error: { fill: "#FFE5E5", fillAlt: "#ffe5e5", border: "#FF4B4B", text: "#EA2B2B", textAlt: "#FF4B4B" },
    neutral: { fill: "#F7F7F7", text: "#777777" },
    locked: { fill: "#E5E5E5", ring: "#E5E5E5", text: "#AFAFAF", cardFill: "#F7F7F7" },
    disabled: { fill: "#E5E5E5", text: "#FFFFFF" }
  },
  overlay: {
    scrim: "#000000",
    text: "#E5E5E5"
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
 * Web already renders in Nunito via `app/+html.tsx` (Google Fonts link + default body font), so
 * these can stay `undefined` (system-font fallback) on native. To activate Nunito on device, swap
 * to "Nunito_800ExtraBold" / "Nunito_700Bold" / "Nunito_600SemiBold" once `@expo-google-fonts/nunito`
 * is installed and loaded via `useFonts` — see docs/product/mobile-shape-language.md §10.
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
