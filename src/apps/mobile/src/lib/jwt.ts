import { Buffer } from "buffer";
import { isUmbralRole, uniqueRoles, type UmbralRole } from "./roles";

type JwtPayload = {
  exp?: number;
  name?: string;
  preferred_username?: string;
  roles?: string[] | string;
  realm_access?: {
    roles?: string[];
  };
};

function decodeBase64UrlSegment(segment: string) {
  const normalized = segment.replace(/-/g, "+").replace(/_/g, "/");
  const padded = normalized.padEnd(Math.ceil(normalized.length / 4) * 4, "=");
  return Buffer.from(padded, "base64").toString("utf8");
}

export function decodeJwtPayload(token: string): JwtPayload {
  const [, payloadSegment] = token.split(".");
  if (!payloadSegment) {
    throw new Error("Invalid JWT payload.");
  }

  return JSON.parse(decodeBase64UrlSegment(payloadSegment)) as JwtPayload;
}

export function extractUmbralRoles(token: string): UmbralRole[] {
  const payload = decodeJwtPayload(token);
  const directRoles = Array.isArray(payload.roles)
    ? payload.roles
    : typeof payload.roles === "string"
      ? payload.roles.split(",").map((value) => value.trim())
      : [];
  const realmRoles = payload.realm_access?.roles ?? [];

  return uniqueRoles([...directRoles, ...realmRoles].filter(isUmbralRole));
}

export function readTokenExpiry(token: string) {
  const payload = decodeJwtPayload(token);
  if (!payload.exp) {
    throw new Error("JWT does not include exp.");
  }

  return new Date(payload.exp * 1000).toISOString();
}

export function readTokenIdentity(token: string) {
  const payload = decodeJwtPayload(token);

  return {
    username: payload.preferred_username ?? payload.name ?? "unknown",
    displayName: payload.name ?? payload.preferred_username ?? "UMBRAL user"
  };
}
