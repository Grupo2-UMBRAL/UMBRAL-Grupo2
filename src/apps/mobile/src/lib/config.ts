const defaults = {
  edgeProxyPublicBaseUrl: "http://localhost:7000",
  keycloakPublicBaseUrl: "http://localhost:7000/auth",
  sessionHubPath: "/session-hub/hubs/session",
  keycloakRealm: "umbral",
  keycloakClientId: "umbral-mobile"
};

export function getClientConfig() {
  const edgeProxyPublicBaseUrl =
    process.env.EXPO_PUBLIC_EDGE_PROXY_BASE_URL ?? defaults.edgeProxyPublicBaseUrl;
  const keycloakPublicBaseUrl =
    process.env.EXPO_PUBLIC_KEYCLOAK_BASE_URL ?? defaults.keycloakPublicBaseUrl;
  const sessionHubPath =
    process.env.EXPO_PUBLIC_SESSION_OPERATIONS_HUB_PATH ?? defaults.sessionHubPath;

  return {
    edgeProxyPublicBaseUrl,
    keycloakPublicBaseUrl,
    keycloakRealm: process.env.EXPO_PUBLIC_KEYCLOAK_REALM ?? defaults.keycloakRealm,
    keycloakClientId:
      process.env.EXPO_PUBLIC_KEYCLOAK_CLIENT_ID ?? defaults.keycloakClientId,
    sessionHubUrl: `${edgeProxyPublicBaseUrl}${sessionHubPath}`
  };
}
