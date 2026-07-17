/**
 * Web (SVG) port of the mobile "spark" mascot (src/apps/mobile/src/components/mascot.tsx).
 * Same shape-language geometry: rounded-square body, held magnifier, pill eye + half-disc
 * mouth. All coordinates are the mobile fractions * 100, drawn in a 0 0 100 100 viewBox.
 * See docs/product/mobile-shape-language.md.
 */

export type MascotMood = "happy" | "celebrate" | "waiting" | "sad";

type Props = {
  mood?: MascotMood;
  size?: number;
  className?: string;
};

// tokens (from src/apps/mobile/src/theme/tokens.ts)
const T = {
  primary: "#58CC02",
  primaryStrong: "#58A700",
  secondary: "#1CB0F6",
  secondaryRing: "#1899D6",
  errorAlt: "#FF4B4B",
  error: "#EA2B2B",
  ink: "#4B4B4B",
  card: "#FFFFFF",
  onBrand: "#FFFFFF",
  warn: "#FFC800",
};

const body: Record<MascotMood, string> = {
  happy: T.primary,
  celebrate: T.primary,
  waiting: T.secondary,
  sad: T.errorAlt,
};
const strong: Record<MascotMood, string> = {
  happy: T.primaryStrong,
  celebrate: T.primaryStrong,
  waiting: T.secondaryRing,
  sad: T.error,
};

export function MascotSvg({ mood = "happy", size = 120, className }: Props) {
  const isWaiting = mood === "waiting";
  const isSad = mood === "sad";

  // geometry (fractions * 100)
  const BODY = { cx: 56, cy: 46, s: 70, r: 29 };
  const EYE = { cx: 73, cy: 36 };
  const LENS = { cx: 33, cy: 36, r: 21 };
  const HAND = { cx: 24.1, cy: 55, r: 6.5 };
  const HANDLE = { cx: 18.6, cy: 66.8, w: 10, h: 26, angle: 25 };

  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 100 100"
      className={className}
      role="img"
      aria-label="Mascota UMBRAL"
      style={{ overflow: "visible" }}
    >
      {mood === "celebrate" && (
        <>
          <circle cx="86" cy="8" r="5" fill={T.secondary} />
          <circle cx="96" cy="22" r="3.5" fill={T.warn} />
          <circle cx="12" cy="12" r="3" fill={T.warn} />
        </>
      )}

      {/* body + eye + mouth lean together (rest tilt -8deg); magnifier stays upright */}
      <g transform={`rotate(-8 ${BODY.cx} ${BODY.cy})`}>
        <rect
          x={BODY.cx - BODY.s / 2}
          y={BODY.cy - BODY.s / 2}
          width={BODY.s}
          height={BODY.s}
          rx={BODY.r}
          fill={body[mood]}
        />

        {/* visible (right) eye */}
        {isWaiting ? (
          <rect x={EYE.cx - 7} y={EYE.cy - 2} width={14} height={4} rx={2} fill={T.onBrand} />
        ) : (
          <>
            <circle cx={EYE.cx} cy={EYE.cy} r={7} fill={T.onBrand} />
            <circle cx={EYE.cx} cy={EYE.cy + (isSad ? 1.4 : 0)} r={3.5} fill={T.ink} />
          </>
        )}

        {/* mouth: half-disc (happy) flips to frown when sad */}
        <path
          d={
            isSad
              ? "M44 62 h24 a12 12 0 0 0 -24 0 z"
              : "M44 54 h24 v0 a12 12 0 0 1 -24 0 z"
          }
          fill={T.ink}
        />
      </g>

      {/* handle, tangent to the rim */}
      <g transform={`rotate(${HANDLE.angle} ${HANDLE.cx} ${HANDLE.cy})`}>
        <rect
          x={HANDLE.cx - HANDLE.w / 2}
          y={HANDLE.cy - HANDLE.h / 2}
          width={HANDLE.w}
          height={HANDLE.h}
          rx={5}
          fill={strong[mood]}
        />
      </g>

      {/* hand: a circle on the rim */}
      <circle cx={HAND.cx} cy={HAND.cy} r={HAND.r} fill={strong[mood]} />

      {/* the glass: magnified winking eye lives here */}
      <circle cx={LENS.cx} cy={LENS.cy} r={LENS.r} fill={T.card} stroke={strong[mood]} strokeWidth={5} />
      {isWaiting ? (
        <rect x={LENS.cx - 9} y={LENS.cy - 2} width={18} height={4} rx={2} fill={T.ink} />
      ) : (
        <circle cx={LENS.cx} cy={LENS.cy + (isSad ? 1.5 : 0)} r={9} fill={T.ink} />
      )}
    </svg>
  );
}
