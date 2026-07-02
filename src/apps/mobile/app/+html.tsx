import { ScrollViewStyleReset } from "expo-router/html";
import type { PropsWithChildren } from "react";

/**
 * Web-only document shell (ignored on native). Loads Nunito — Duolingo's
 * recommended substitute face — so the participant web build matches the
 * bright mobile design kit, and sets the soft Polar background as the page
 * default.
 */
export default function Root({ children }: PropsWithChildren) {
  return (
    <html lang="es">
      <head>
        <meta charSet="utf-8" />
        <meta httpEquiv="X-UA-Compatible" content="IE=edge" />
        <meta name="viewport" content="width=device-width, initial-scale=1, shrink-to-fit=no" />
        <link rel="preconnect" href="https://fonts.googleapis.com" />
        <link rel="preconnect" href="https://fonts.gstatic.com" crossOrigin="" />
        <link
          href="https://fonts.googleapis.com/css2?family=Nunito:wght@400;600;700;800&display=swap"
          rel="stylesheet"
        />
        <ScrollViewStyleReset />
        <style
          dangerouslySetInnerHTML={{
            __html: `html, body, #root { font-family: 'Nunito', 'Segoe UI', system-ui, -apple-system, sans-serif; background-color: #F7F7F7; }`
          }}
        />
      </head>
      <body>{children}</body>
    </html>
  );
}
