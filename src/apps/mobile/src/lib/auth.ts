import { extractUmbralRoles, readTokenExpiry, readTokenIdentity } from "./jwt";
import { getClientConfig } from "./config";
import type { UmbralMobileSession } from "./session-storage";

type LoginInput = {
  username: string;
  password: string;
};

type TokenResponse = {
  access_token: string;
  refresh_token?: string;
  refresh_expires_in?: number;
};

function createSessionFromTokenResponse(payload: TokenResponse): UmbralMobileSession {
  const accessToken = payload.access_token;
  const roles = extractUmbralRoles(accessToken);
  const identity = readTokenIdentity(accessToken);

  return {
    accessToken,
    expiresAt: readTokenExpiry(accessToken),
    refreshToken: payload.refresh_token,
    refreshExpiresAt:
      typeof payload.refresh_expires_in === "number" && payload.refresh_expires_in > 0
        ? new Date(Date.now() + payload.refresh_expires_in * 1000).toISOString()
        : undefined,
    roles,
    username: identity.username,
    displayName: identity.displayName
  };
}

/**
 * Exchanges the stored refresh token for a fresh access token so a player is not kicked out
 * mid-game when the short-lived access token expires. Keycloak rotates the refresh token, so the
 * returned session carries the new pair.
 */
export async function refreshSession(refreshToken: string): Promise<UmbralMobileSession> {
  const config = getClientConfig();
  const tokenEndpoint =
    `${config.keycloakPublicBaseUrl}/realms/${config.keycloakRealm}` +
    "/protocol/openid-connect/token";
  const response = await fetch(tokenEndpoint, {
    method: "POST",
    headers: {
      "content-type": "application/x-www-form-urlencoded"
    },
    body: new URLSearchParams({
      grant_type: "refresh_token",
      client_id: config.keycloakClientId,
      refresh_token: refreshToken
    }).toString()
  });

  if (!response.ok) {
    throw new Error("Keycloak rejected the refresh token.");
  }

  const payload = (await response.json()) as TokenResponse;
  return createSessionFromTokenResponse(payload);
}

export async function loginWithPassword({ username, password }: LoginInput) {
  const normalizedUsername = username.trim();
  const normalizedPassword = password.trim();

  if (!normalizedUsername || !normalizedPassword) {
    throw new Error("Username and password required.");
  }

  const config = getClientConfig();
  const tokenEndpoint =
    `${config.keycloakPublicBaseUrl}/realms/${config.keycloakRealm}` +
    "/protocol/openid-connect/token";
  const response = await fetch(tokenEndpoint, {
    method: "POST",
    headers: {
      "content-type": "application/x-www-form-urlencoded"
    },
    body: new URLSearchParams({
      grant_type: "password",
      client_id: config.keycloakClientId,
      username: normalizedUsername,
      password: normalizedPassword
    }).toString()
  });

  if (!response.ok) {
    throw new Error("Credentials rejected by Keycloak.");
  }

  const payload = (await response.json()) as TokenResponse;
  const session = createSessionFromTokenResponse(payload);

  if (!session.roles.includes("Participant")) {
    throw new Error("Role rejected. Mobile shell only supports Participant.");
  }

  return session;
}
