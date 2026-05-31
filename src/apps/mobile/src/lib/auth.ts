import { extractUmbralRoles, readTokenExpiry, readTokenIdentity } from "./jwt";
import { getClientConfig } from "./config";
import type { UmbralMobileSession } from "./session-storage";

type LoginInput = {
  username: string;
  password: string;
};

type TokenResponse = {
  access_token: string;
};

export function createSessionFromAccessToken(accessToken: string): UmbralMobileSession {
  const roles = extractUmbralRoles(accessToken);
  const identity = readTokenIdentity(accessToken);

  return {
    accessToken,
    expiresAt: readTokenExpiry(accessToken),
    roles,
    username: identity.username,
    displayName: identity.displayName
  };
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
  const session = createSessionFromAccessToken(payload.access_token);

  if (!session.roles.includes("Participant")) {
    throw new Error("Role rejected. Mobile shell only supports Participant.");
  }

  return session;
}
