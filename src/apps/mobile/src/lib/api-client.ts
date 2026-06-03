import { getClientConfig } from "./config";

function normalizePath(path: string) {
  return path.startsWith("/") ? path : `/${path}`;
}

function createAuthorizationHeaderPreview(accessToken: string) {
  return `Bearer ${accessToken.slice(0, 18)}...`;
}

async function readResponseBody(response: Response) {
  const contentType = response.headers.get("content-type") ?? "";
  if (contentType.includes("application/json")) {
    return response.json();
  }

  return response.text();
}

function readErrorMessage(payload: unknown, fallback: string) {
  if (!payload || typeof payload !== "object") {
    return fallback;
  }

  if ("detail" in payload && typeof payload.detail === "string") {
    return payload.detail;
  }

  if ("message" in payload && typeof payload.message === "string") {
    return payload.message;
  }

  if ("title" in payload && typeof payload.title === "string") {
    return payload.title;
  }

  return fallback;
}

function readErrorCode(payload: unknown) {
  if (!payload || typeof payload !== "object") {
    return undefined;
  }

  if ("code" in payload && typeof payload.code === "string") {
    return payload.code;
  }

  if ("errorCode" in payload && typeof payload.errorCode === "string") {
    return payload.errorCode;
  }

  return undefined;
}

export class ApiClientError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly code?: string
  ) {
    super(message);
    this.name = "ApiClientError";
  }
}

export type ApiProbeResult = {
  ok: boolean;
  status: number;
  body: string;
  url: string;
  authorizationHeaderPreview: string;
};

export type ParticipantEnrollmentStatus = {
  liveSessionId: string;
  sessionState: string;
  openedAtUtc: string | null;
  closedAtUtc: string | null;
  isOpen: boolean;
};

export type SessionTeam = {
  id: string;
  name: string;
};

export type SessionTeamsResult = {
  liveSessionId: string;
  isOpen: boolean;
  teams: SessionTeam[];
};

export type RegisterTeamInput = {
  joinCode: string;
  teamName: string;
};

export type JoinSessionTeamInput = {
  joinCode: string;
  sessionTeamId: string;
};

export type EnrollmentResult = {
  liveSessionId: string;
  sessionTeamId: string;
  teamName: string;
  participantUserId: string;
  registeredAtUtc?: string;
  enrolledAtUtc?: string;
};

export function createAuthorizedApiClient(accessToken: string) {
  const config = getClientConfig();

  async function request(path: string, init: RequestInit = {}) {
    const url = `${config.edgeProxyPublicBaseUrl}${normalizePath(path)}`;
    const headers = new Headers(init.headers);
    headers.set("authorization", `Bearer ${accessToken}`);

    if (init.body && !headers.has("content-type")) {
      headers.set("content-type", "application/json");
    }

    const response = await fetch(url, {
      ...init,
      headers
    });

    return {
      url,
      response
    };
  }

  async function requestJson<T>(path: string, init: RequestInit = {}) {
    const { response } = await request(path, init);
    const body = await readResponseBody(response);

    if (!response.ok) {
      throw new ApiClientError(
        readErrorMessage(body, "Session Operations request failed."),
        response.status,
        readErrorCode(body)
      );
    }

    return body as T;
  }

  return {
    request,
    authorizationHeaderPreview: createAuthorizationHeaderPreview(accessToken),
    async getHealth(): Promise<ApiProbeResult> {
      const { url, response } = await request("/health");

      return {
        ok: response.ok,
        status: response.status,
        body: await response.text(),
        url,
        authorizationHeaderPreview: createAuthorizationHeaderPreview(accessToken)
      };
    },
    validateEnrollmentJoinCode(joinCode: string) {
      return requestJson<ParticipantEnrollmentStatus>(
        `/api/session-operations/session-enrollment/${encodeURIComponent(joinCode)}/validate`
      );
    },
    listEnrollmentTeams(joinCode: string) {
      return requestJson<SessionTeamsResult>(
        `/api/session-operations/session-enrollment/${encodeURIComponent(joinCode)}/teams`
      );
    },
    joinSessionTeam(input: JoinSessionTeamInput) {
      return requestJson<EnrollmentResult>("/api/session-operations/session-enrollment/join", {
        method: "POST",
        body: JSON.stringify({
          joinCode: input.joinCode,
          sessionTeamId: input.sessionTeamId
        })
      });
    },
    createSessionTeam(input: RegisterTeamInput) {
      return requestJson<EnrollmentResult>("/api/session-operations/session-enrollment/teams", {
        method: "POST",
        body: JSON.stringify({
          joinCode: input.joinCode,
          teamName: input.teamName
        })
      });
    }
  };
}
