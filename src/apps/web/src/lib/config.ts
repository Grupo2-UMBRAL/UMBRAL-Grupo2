const defaults = {
  webPort: "3000",
  edgeProxyPublicBaseUrl: "http://localhost:7000",
  keycloakPublicBaseUrl: "http://localhost:7000/auth",
  sessionHubPath: "/session-hub/hubs/session",
  keycloakRealm: "umbral",
  keycloakClientId: "umbral-web"
};

export function getServerConfig() {
  const edgeProxyPublicBaseUrl =
    process.env.NEXT_PUBLIC_EDGE_PROXY_BASE_URL ?? defaults.edgeProxyPublicBaseUrl;
  const keycloakPublicBaseUrl =
    process.env.NEXT_PUBLIC_KEYCLOAK_BASE_URL ?? defaults.keycloakPublicBaseUrl;

  return {
    webPort: process.env.WEB_PORT ?? defaults.webPort,
    edgeProxyPublicBaseUrl,
    edgeProxyInternalBaseUrl: process.env.EDGE_PROXY_INTERNAL_BASE_URL ?? edgeProxyPublicBaseUrl,
    keycloakPublicBaseUrl,
    keycloakInternalBaseUrl: process.env.KEYCLOAK_INTERNAL_BASE_URL ?? keycloakPublicBaseUrl,
    keycloakRealm: process.env.KEYCLOAK_REALM ?? defaults.keycloakRealm,
    keycloakClientId: process.env.KEYCLOAK_WEB_CLIENT_ID ?? defaults.keycloakClientId,
    sessionHubPath: process.env.NEXT_PUBLIC_SESSION_OPERATIONS_HUB_PATH ?? defaults.sessionHubPath
  };
}

export function getClientConfig() {
  const config = getServerConfig();

  return {
    edgeProxyPublicBaseUrl: config.edgeProxyPublicBaseUrl,
    keycloakPublicBaseUrl: config.keycloakPublicBaseUrl,
    keycloakRealm: config.keycloakRealm,
    sessionHubUrl: `${config.edgeProxyPublicBaseUrl}${config.sessionHubPath}`
  };
}
