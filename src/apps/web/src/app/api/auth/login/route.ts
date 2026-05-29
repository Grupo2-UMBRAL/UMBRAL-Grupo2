import { NextResponse } from "next/server";
import { getServerConfig } from "@/lib/config";
import {
  createSessionFromAccessToken,
  applySessionCookie,
  clearSessionCookie,
  resolveDashboardPath
} from "@/lib/session";
import { sessionHasAnyRole } from "@/lib/session-cookie";

type LoginRequest = {
  username?: string;
  password?: string;
  next?: string;
};

function createRejectedLoginResponse(message: string, status: number) {
  const response = NextResponse.json({ message }, { status });
  clearSessionCookie(response);
  return response;
}

export async function POST(request: Request) {
  const body = (await request.json()) as LoginRequest;
  const username = body.username?.trim();
  const password = body.password?.trim();

  if (!username || !password) {
    return createRejectedLoginResponse("Username and password required.", 400);
  }

  const config = getServerConfig();
  const tokenEndpoint = `${config.keycloakInternalBaseUrl}/realms/${config.keycloakRealm}/protocol/openid-connect/token`;
  const tokenResponse = await fetch(tokenEndpoint, {
    method: "POST",
    headers: {
      "content-type": "application/x-www-form-urlencoded"
    },
    body: new URLSearchParams({
      grant_type: "password",
      client_id: config.keycloakClientId,
      username,
      password
    }),
    cache: "no-store"
  });

  if (!tokenResponse.ok) {
    return createRejectedLoginResponse("Credentials rejected by Keycloak.", 401);
  }

  const tokenPayload = (await tokenResponse.json()) as {
    access_token: string;
  };
  const session = createSessionFromAccessToken(tokenPayload.access_token);

  if (!sessionHasAnyRole(session, ["Administrator", "Operator"])) {
    return createRejectedLoginResponse(
      "Role rejected. Web shell only supports Administrator or Operator.",
      403
    );
  }

  const redirectTo =
    body.next && body.next.startsWith("/") && !body.next.startsWith("//")
      ? body.next
      : resolveDashboardPath(session);
  const response = NextResponse.json({ redirectTo });

  applySessionCookie(response, session);

  return response;
}
