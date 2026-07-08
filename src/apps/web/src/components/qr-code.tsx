import { useMemo } from "react";
import { encodeQrMatrix, type Ecc } from "../lib/qr-code";

type QrSvgOptions = {
  ecc?: Ecc;
  margin?: number; // quiet-zone modules
};

/**
 * Builds a self-contained SVG string for `value`. Shared by the inline preview
 * and the print window so both render byte-for-byte the same code.
 */
export function buildQrSvgMarkup(value: string, options: QrSvgOptions = {}): string {
  const { ecc = "M", margin = 4 } = options;
  const matrix = encodeQrMatrix(value, ecc);
  const count = matrix.length;
  const dim = count + margin * 2;

  let rects = "";
  for (let y = 0; y < count; y++) {
    for (let x = 0; x < count; x++) {
      if (matrix[y][x]) {
        rects += `<rect x="${x + margin}" y="${y + margin}" width="1" height="1"/>`;
      }
    }
  }

  return (
    `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${dim} ${dim}" ` +
    `shape-rendering="crispEdges" role="img">` +
    `<rect width="${dim}" height="${dim}" fill="#ffffff"/>` +
    `<g fill="#000000">${rects}</g>` +
    `</svg>`
  );
}

type QrCodeProps = {
  value: string;
  size?: number;
  ecc?: Ecc;
};

/** Inline QR preview. */
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

/**
 * Opens a print window with a single QR + its label/clue, sized for physical
 * placement. Used by both the admin mission editor and the operator panel.
 */
export function printQrCode(value: string, opts: { title?: string; clue?: string } = {}): void {
  let markup: string;
  try {
    markup = buildQrSvgMarkup(value, { ecc: "M" });
  } catch {
    window.alert("El valor del QR es demasiado largo para generarlo.");
    return;
  }

  const title = opts.title ?? "Código QR";
  const clue = opts.clue ?? "";
  const win = window.open("", "_blank", "width=520,height=640");
  if (!win) {
    window.alert("Permite las ventanas emergentes para imprimir el QR.");
    return;
  }

  const esc = (s: string) =>
    s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");

  win.document.write(
    `<!doctype html><html><head><meta charset="utf-8"><title>${esc(title)}</title>` +
    `<style>` +
    `*{margin:0;padding:0;box-sizing:border-box}` +
    `body{font-family:system-ui,sans-serif;display:flex;flex-direction:column;` +
    `align-items:center;justify-content:center;gap:16px;padding:32px;text-align:center}` +
    `.qr{width:320px;height:320px}` +
    `.qr svg{width:100%;height:100%}` +
    `h1{font-size:18px;font-weight:600}` +
    `.clue{font-size:14px;color:#333;max-width:360px}` +
    `.hash{font-family:ui-monospace,monospace;font-size:12px;color:#666;word-break:break-all;max-width:360px}` +
    `@media print{@page{margin:12mm}}` +
    `</style></head><body>` +
    `<h1>${esc(title)}</h1>` +
    (clue ? `<p class="clue">${esc(clue)}</p>` : "") +
    `<div class="qr">${markup}</div>` +
    `<p class="hash">${esc(value)}</p>` +
    `<script>window.onload=function(){window.focus();window.print();};</script>` +
    `</body></html>`,
  );
  win.document.close();
}
