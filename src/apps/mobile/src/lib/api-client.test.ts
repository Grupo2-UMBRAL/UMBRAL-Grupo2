import { createAuthorizedApiClient } from "./api-client";

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
    "https://edge.test/api/session-management/session-enrollment/join",
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
    "https://edge.test/api/session-management/session-enrollment/teams",
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
    "https://edge.test/api/session-management/session-teams/team-1/snapshot",
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
    "https://edge.test/api/session-management/session-teams/team-1/submissions",
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
