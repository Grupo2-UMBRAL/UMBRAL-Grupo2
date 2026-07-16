import { useMemo } from "react";
import { type Ecc } from "../lib/qr-code";
import { buildQrSvgMarkup } from "../lib/qr-code-svg";

type QrCodeProps = {
  value: string;
  size?: number;
  ecc?: Ecc;
};

export function QrCode({ value, size = 160, ecc = "M" }: QrCodeProps) {
  const markup = useMemo(() => {
    try {
      return buildQrSvgMarkup(value, { ecc });
    } catch {
      return null;
    }
  }, [value, ecc]);

  if (!value) {
    return null;
  }
  if (!markup) {
    return <span className="text-muted text-xs">Valor demasiado largo para QR.</span>;
  }

  return (
    <span
      aria-label={`Código QR de ${value}`}
      className="qr-code-preview"
      style={{ display: "inline-block", width: size, height: size, lineHeight: 0 }}
      dangerouslySetInnerHTML={{ __html: markup }}
    />
  );
}
