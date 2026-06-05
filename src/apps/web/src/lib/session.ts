import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { readTokenExpiry, readTokenIdentity, extractUmbralRoles } from "@/lib/jwt";
import {
  applySessionCookie,
  clearSessionCookie,
  parseSessionCookieValue,
  sessionCookieName,
  sessionHasAnyRole,
  type UmbralWebSession
} from "@/lib/session-cookie";
import { isWebShellRole, type WebShellRole } from "@/lib/roles";

export function createSessionFromAccessToken(accessToken: string): UmbralWebSession {
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

export async function getOptionalSession() {
  const cookieStore = await cookies();
  return parseSessionCookieValue(cookieStore.get(sessionCookieName)?.value);
}

export async function getRequiredSession(allowedRoles?: readonly WebShellRole[]) {
  const session = await getOptionalSession();
  if (!session) {
    redirect("/login");
  }

  if (allowedRoles && !sessionHasAnyRole(session, allowedRoles)) {
    redirect("/forbidden");
  }

  return session;
}

export function resolveDashboardPath(session: UmbralWebSession) {
  if (session.roles.includes("Administrator")) {
    return "/administrator";
  }

  if (session.roles.includes("Operator")) {
    return "/operator";
  }

  return "/forbidden";
}

export function resolveVisibleRoles(session: UmbralWebSession) {
  return session.roles.filter(isWebShellRole);
}

export { applySessionCookie, clearSessionCookie };
