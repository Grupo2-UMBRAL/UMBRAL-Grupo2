import { getClientConfig } from "@/lib/config";

/**
 * Builds the URL to Keycloak's built-in "account console" (self-service account
 * management) for the current realm — the same page mobile links to from the
 * participant profile.
 *
 * Unlike mobile (which authenticates via ROPC and has no browser SSO cookie),
 * the web console logs in through keycloak-js, so opening this in the same
 * browser lands the operator/admin on their account already signed in.
 */
export function buildAccountConsoleUrl() {
  const config = getClientConfig();

  return `${config.keycloakPublicBaseUrl}/realms/${config.keycloakRealm}/account`;
}
