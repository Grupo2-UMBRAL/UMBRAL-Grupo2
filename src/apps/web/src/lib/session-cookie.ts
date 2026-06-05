import type { NextResponse } from "next/server";
import { type UmbralRole } from "@/lib/roles";

export const sessionCookieName = "umbral_web_session";

export type UmbralWebSession = {
  accessToken: string;
  expiresAt: string;
  username: string;
  displayName: string;
  roles: UmbralRole[];
};

export function parseSessionCookieValue(cookieValue?: string) {
  if (!cookieValue) {
    return null;
  }

  try {
    const session = JSON.parse(cookieValue) as UmbralWebSession;
    if (
      !session.accessToken ||
      !session.expiresAt ||
      !Array.isArray(session.roles) ||
      session.roles.length === 0)
    {
      return null;
    }

    if (new Date(session.expiresAt).getTime() <= Date.now()) {
      return null;
    }

    return session;
  } catch {
    return null;
  }
}

export function sessionHasAnyRole(
  session: UmbralWebSession,
  allowedRoles: readonly UmbralRole[])
{
  return session.roles.some((role) => allowedRoles.includes(role));
}

export function resolvePathRoles(pathname: string): readonly UmbralRole[] {
  if (pathname.startsWith("/administrator")) {
    return ["Administrator"];
  }

  if (pathname.startsWith("/operator")) {
    return ["Operator", "Administrator"];
  }

  return [];
}

export function applySessionCookie(response: NextResponse, session: UmbralWebSession) {
  response.cookies.set(sessionCookieName, JSON.stringify(session), {
    httpOnly: true,
    sameSite: "lax",
    secure: false,
    path: "/",
    expires: new Date(session.expiresAt)
  });
}

export function clearSessionCookie(response: NextResponse, expiresAt?: string) {
  response.cookies.set(sessionCookieName, "", {
    httpOnly: true,
    sameSite: "lax",
    secure: false,
    path: "/",
    expires: expiresAt ? new Date(expiresAt) : new Date(0)
  });
}
