import { createAuthorizedApiClient, createParticipantAccountClient } from "./api-client";

jest.mock("./config", () => ({
  getClientConfig: () => ({
    edgeProxyPublicBaseUrl: "https://edge.test",
    keycloakPublicBaseUrl: "https://keycloak.test",
    keycloakRealm: "umbral",
    keycloakClientId: "mobile",
    sessionHubUrl: "https://edge.test/session-hub"
  })
}));

function jsonResponse(body: unknown, status = 200) {
  return {
    ok: status >= 200 && status < 300,
    status,
    headers: new Headers({
      "content-type": "application/json"
    }),
    json: jest.fn().mockResolvedValue(body),
    text: jest.fn().mockResolvedValue(JSON.stringify(body))
  } as unknown as Response;
}

beforeEach(() => {
  global.fetch = jest.fn().mockResolvedValue(jsonResponse({})) as jest.Mock;
});

test("calls join endpoint with expected network payload", async () => {
  const apiClient = createAuthorizedApiClient("participant-token");

  await apiClient.joinSessionTeam({
    joinCode: "ABC234",
    sessionTeamId: "team-1"
  });

  expect(fetch).toHaveBeenCalledWith(
    "https://edge.test/session-management/api/session-management/session-enrollment/join",
    expect.objectContaining({
      method: "POST",
      body: JSON.stringify({
        joinCode: "ABC234",
        sessionTeamId: "team-1"
      })
    })
  );
  const [, init] = (fetch as jest.Mock).mock.calls[0] as [string, RequestInit];
  expect((init.headers as Headers).get("authorization")).toBe("Bearer participant-token");
  expect((init.headers as Headers).get("content-type")).toBe("application/json");
});

test("calls create team endpoint with expected network payload", async () => {
  const apiClient = createAuthorizedApiClient("participant-token");

  await apiClient.createSessionTeam({
    joinCode: "ABC234",
    teamName: "New Team"
  });

  expect(fetch).toHaveBeenCalledWith(
    "https://edge.test/session-management/api/session-management/session-enrollment/teams",
    expect.objectContaining({
      method: "POST",
      body: JSON.stringify({
        joinCode: "ABC234",
        teamName: "New Team"
      })
    })
  );
  const [, init] = (fetch as jest.Mock).mock.calls[0] as [string, RequestInit];
  expect((init.headers as Headers).get("authorization")).toBe("Bearer participant-token");
  expect((init.headers as Headers).get("content-type")).toBe("application/json");
});

test("calls session team snapshot endpoint with participant token", async () => {
  const apiClient = createAuthorizedApiClient("participant-token");

  await apiClient.getSessionTeamSnapshot("team-1");

  expect(fetch).toHaveBeenCalledWith(
    "https://edge.test/session-management/api/session-management/session-teams/team-1/snapshot",
    expect.objectContaining({})
  );
  const [, init] = (fetch as jest.Mock).mock.calls[0] as [string, RequestInit];
  expect((init.headers as Headers).get("authorization")).toBe("Bearer participant-token");
});

test("calls evidence submission endpoint with QR hash payload", async () => {
  const apiClient = createAuthorizedApiClient("participant-token");

  await apiClient.submitEvidence({
    sessionTeamId: "team-1",
    qrHash: "qr-hash-123"
  });

  expect(fetch).toHaveBeenCalledWith(
    "https://edge.test/session-management/api/session-management/session-teams/team-1/submissions",
    expect.objectContaining({
      method: "POST",
      body: JSON.stringify({
        qrHash: "qr-hash-123"
      })
    })
  );
  const [, init] = (fetch as jest.Mock).mock.calls[0] as [string, RequestInit];
  expect((init.headers as Headers).get("authorization")).toBe("Bearer participant-token");
  expect((init.headers as Headers).get("content-type")).toBe("application/json");
});

// The prefix is the whole point of the shared factory: the same token reaches a second service, and
// a client that silently kept /session-management would 404 against user-management.
test("participant account client targets user-management with the participant token", async () => {
  const accountClient = createParticipantAccountClient("participant-token");

  await accountClient.getMyProfile();

  expect(fetch).toHaveBeenCalledWith(
    "https://edge.test/user-management/api/participants/me",
    expect.objectContaining({})
  );
  const [, init] = (fetch as jest.Mock).mock.calls[0] as [string, RequestInit];
  expect((init.headers as Headers).get("authorization")).toBe("Bearer participant-token");
});

test("calls username change endpoint with a trimmed username payload", async () => {
  const accountClient = createParticipantAccountClient("participant-token");

  await accountClient.changeMyUsername("  pao.rojas  ");

  expect(fetch).toHaveBeenCalledWith(
    "https://edge.test/user-management/api/participants/me/username",
    expect.objectContaining({
      method: "PATCH",
      body: JSON.stringify({ username: "pao.rojas" })
    })
  );
  const [, init] = (fetch as jest.Mock).mock.calls[0] as [string, RequestInit];
  expect((init.headers as Headers).get("content-type")).toBe("application/json");
});

test("calls deactivate endpoint with no body", async () => {
  const accountClient = createParticipantAccountClient("participant-token");

  await accountClient.deactivateMyAccount();

  expect(fetch).toHaveBeenCalledWith(
    "https://edge.test/user-management/api/participants/me/deactivate",
    expect.objectContaining({ method: "POST" })
  );
});

// The screen maps participant_* codes to Spanish copy, so the code has to survive the transport.
test("surfaces the failure code from a rejected username change", async () => {
  global.fetch = jest.fn().mockResolvedValue(
    jsonResponse({ code: "participant_username_taken", detail: "Username already belongs to another User." }, 409)
  ) as jest.Mock;
  const accountClient = createParticipantAccountClient("participant-token");

  await expect(accountClient.changeMyUsername("taken")).rejects.toMatchObject({
    status: 409,
    code: "participant_username_taken"
  });
});

test("calls trivia submission endpoint with selected choice id payload", async () => {
  const apiClient = createAuthorizedApiClient("participant-token");

  await apiClient.submitTriviaAnswer({
    sessionTeamId: "team-1",
    selectedChoiceId: "choice-2"
  });

  expect(fetch).toHaveBeenCalledWith(
    "https://edge.test/session-management/api/session-management/session-teams/team-1/trivia-submissions",
    expect.objectContaining({
      method: "POST",
      body: JSON.stringify({
        selectedChoiceId: "choice-2"
      })
    })
  );
  const [, init] = (fetch as jest.Mock).mock.calls[0] as [string, RequestInit];
  expect((init.headers as Headers).get("authorization")).toBe("Bearer participant-token");
  expect((init.headers as Headers).get("content-type")).toBe("application/json");
});
