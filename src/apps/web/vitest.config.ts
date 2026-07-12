import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

// Unit tests only. The Google Maps SDK is always mocked — tests never hit the
// network or load the real maps/places libraries.
export default defineConfig({
  plugins: [react()],
  test: {
    environment: "jsdom",
    setupFiles: ["./vitest.setup.ts"],
    include: ["src/**/*.test.{ts,tsx}"],
  },
});
