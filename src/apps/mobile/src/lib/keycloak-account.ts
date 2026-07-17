import { getClientConfig } from "./config";

/**
 * Deep link into Keycloak's Account Console, on the screen that owns the password. Changing a
 * password means rebuilding a credentials UI that Keycloak already ships, so the app hands the
 * player over instead of reimplementing it.
 *
 * The path is verified by driving the real console, not inferred: the `account-console` client has
 * base URL `/realms/{realm}/account/` (Keycloak creates that client itself, even though this realm
 * is hand-written and never declares it), and its own "Signing in" nav link resolves to
 * `account-security/signing-in` as a plain path. The bundle does contain `createHashRouter`, so a
 * `#/account-security/signing-in` link looks right and even keeps the hash in the address bar --
 * it just silently renders Personal info instead. Only the path form lands on the password.
 *
 * Expect a login prompt on arrival, and say so before sending the player: mobile signs in through
 * ROPC straight against the token endpoint, so there is no SSO cookie in the system browser and
 * Keycloak has no session to recognise. That re-login is a consequence of keeping ROPC, not a bug.
 */
export function buildAccountConsoleUrl() {
  const config = getClientConfig();

  return (
    `${config.keycloakPublicBaseUrl}/realms/${config.keycloakRealm}` +
    "/account/account-security/signing-in"
  );
}
