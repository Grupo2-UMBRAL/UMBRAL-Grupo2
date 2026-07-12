import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'path'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  // Load env file from the current directory, and merge with process.env
  const loadedEnv = loadEnv(mode, process.cwd(), '')
  const envSource = { ...process.env, ...loadedEnv }

  // Expose only the specific environment variables required by UMBRAL to the client code
  const umbralEnv = {
    WEB_PORT: envSource.WEB_PORT,
    EDGE_PROXY_INTERNAL_BASE_URL: envSource.EDGE_PROXY_INTERNAL_BASE_URL,
    KEYCLOAK_INTERNAL_BASE_URL: envSource.KEYCLOAK_INTERNAL_BASE_URL,
    KEYCLOAK_REALM: envSource.KEYCLOAK_REALM,
    KEYCLOAK_WEB_CLIENT_ID: envSource.KEYCLOAK_WEB_CLIENT_ID,
    NEXT_PUBLIC_EDGE_PROXY_BASE_URL: envSource.NEXT_PUBLIC_EDGE_PROXY_BASE_URL,
    NEXT_PUBLIC_KEYCLOAK_BASE_URL: envSource.NEXT_PUBLIC_KEYCLOAK_BASE_URL,
    NEXT_PUBLIC_SESSION_OPERATIONS_HUB_PATH: envSource.NEXT_PUBLIC_SESSION_OPERATIONS_HUB_PATH,
    NEXT_PUBLIC_SCORING_MONITORING_HUB_PATH: envSource.NEXT_PUBLIC_SCORING_MONITORING_HUB_PATH,
    NEXT_PUBLIC_GOOGLE_MAPS_API_KEY: envSource.NEXT_PUBLIC_GOOGLE_MAPS_API_KEY,
  }

  return {
    plugins: [react()],
    resolve: {
      alias: {
        '@': path.resolve(__dirname, './src'),
      },
    },
    server: {
      port: 3000,
      host: true,
      allowedHosts: true,
      watch: {
        usePolling: true,
      },
    },
    define: {
      'process.env': umbralEnv,
    },
  }
})
