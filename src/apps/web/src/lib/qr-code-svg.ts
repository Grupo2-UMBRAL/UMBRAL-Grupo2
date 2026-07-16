import { encodeQrMatrix, type Ecc } from "./qr-code";

type QrSvgOptions = {
  ecc?: Ecc;
  margin?: number;
};

export function buildQrSvgMarkup(value: string, options: QrSvgOptions = {}): string {
  const { ecc = "M", margin = 4 } = options;
  const matrix = encodeQrMatrix(value, ecc);
  const count = matrix.length;
  const dimension = count + margin * 2;

  let rectangles = "";
  for (let y = 0; y < count; y++) {
    for (let x = 0; x < count; x++) {
      if (matrix[y][x]) {
        rectangles += `<rect x="${x + margin}" y="${y + margin}" width="1" height="1"/>`;
      }
    }
  }

  return (
    `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${dimension} ${dimension}" ` +
    `shape-rendering="crispEdges" role="img">` +
    `<rect width="${dimension}" height="${dimension}" fill="#ffffff"/>` +
    `<g fill="#000000">${rectangles}</g>` +
    `</svg>`
  );
}

export function printQrCode(value: string, options: { title?: string; clue?: string } = {}): void {
  let markup: string;
  try {
    markup = buildQrSvgMarkup(value, { ecc: "M" });
  } catch {
    window.alert("El valor del QR es demasiado largo para generarlo.");
    return;
  }

  const title = options.title ?? "Código QR";
  const clue = options.clue ?? "";
  const printWindow = window.open("", "_blank", "width=520,height=640");
  if (!printWindow) {
    window.alert("Permite las ventanas emergentes para imprimir el QR.");
    return;
  }

  const escapeHtml = (text: string) =>
    text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");

  printWindow.document.write(
    `<!doctype html><html><head><meta charset="utf-8"><title>${escapeHtml(title)}</title>` +
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
    `<h1>${escapeHtml(title)}</h1>` +
    (clue ? `<p class="clue">${escapeHtml(clue)}</p>` : "") +
    `<div class="qr">${markup}</div>` +
    `<p class="hash">${escapeHtml(value)}</p>` +
    `<script>window.onload=function(){window.focus();window.print();};</script>` +
    `</body></html>`,
  );
  printWindow.document.close();
}
