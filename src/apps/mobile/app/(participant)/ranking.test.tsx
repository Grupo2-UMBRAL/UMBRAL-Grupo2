import { act, render, screen, waitFor } from "@testing-library/react-native";
import RankingPage from "./ranking";
import { loadStoredEnrollment } from "../../src/lib/session-storage";
import { useSession } from "../../src/providers/session-provider";

jest.mock("../../src/providers/session-provider", () => ({
  useSession: jest.fn()
}));

jest.mock("../../src/lib/session-storage", () => ({
  loadStoredEnrollment: jest.fn()
}));

const signalRHandlers: Record<string, (payload: unknown) => void> = {};

const mockConnection = {
  on: jest.fn((eventName: string, handler: (payload: unknown) => void) => {
    signalRHandlers[eventName] = handler;
  }),
  onreconnecting: jest.fn(),
  onreconnected: jest.fn(),
  onclose: jest.fn(),
  start: jest.fn().mockResolvedValue(undefined),
  stop: jest.fn().mockResolvedValue(undefined)
};

const mockBuilder = {
  withUrl: jest.fn().mockReturnThis(),
  withAutomaticReconnect: jest.fn().mockReturnThis(),
  configureLogging: jest.fn().mockReturnThis(),
  build: jest.fn(() => mockConnection)
};

jest.mock("@microsoft/signalr", () => ({
  HubConnectionBuilder: jest.fn(() => mockBuilder),
  HttpTransportType: {
    WebSockets: 1,
    ServerSentEvents: 2
  },
  LogLevel: {
    Warning: 3
  }
}));

function createJsonResponse(body: unknown, status = 200) {
  const headers = new Headers({
    "content-type": "application/json"
  });

  return {
    ok: status >= 200 && status < 300,
    status,
    statusText: status === 200 ? "OK" : "Error",
    headers,
    json: async () => body,
    text: async () => JSON.stringify(body)
  } as Response;
}

beforeEach(() => {
  jest.clearAllMocks();
  Object.keys(signalRHandlers).forEach((eventName) => delete signalRHandlers[eventName]);

  (useSession as jest.Mock).mockReturnValue({
    session: {
      accessToken: "participant-token",
      expiresAt: "2026-06-03T12:00:00Z",
      roles: ["Participant"],
      username: "participant",
      displayName: "Participant One"
    }
  });
  (loadStoredEnrollment as jest.Mock).mockResolvedValue({
    joinCode: "ABC234",
    teamId: "team-1",
    teamName: "Alpha Team"
  });
});

test("loads ranking and highlights the participant team", async () => {
  const fetchMock = jest.fn()
    .mockResolvedValueOnce(
      createJsonResponse({
        liveSessionId: "live-session-1",
        sessionTeamId: "team-1",
        teamName: "Alpha Team",
        sessionState: "Running",
        progressState: "InProgress",
        currentStage: {
          missionStageId: "stage-1",
          name: "Decode the seal",
          sessionStageOrder: 1,
          sourceOrder: 10,
          resolvedTimeBudgetMinutes: 15,
          difficulty: "Medium",
          gameType: "Trivia"
        },
        visibleHints: [],
        sync: {
          sequenceNumber: 1,
          lastUpdatedUtc: "2026-06-03T08:00:00Z",
          serverTimeUtc: "2026-06-03T08:00:00Z"
        }
      })
    )
    .mockResolvedValueOnce(
      createJsonResponse({
        liveSessionId: "live-session-1",
        generatedAtUtc: "2026-06-03T08:00:00Z",
        items: [
          {
            rank: 1,
            sessionTeamId: "team-1",
            visibleScore: 200,
            resolutionTime: "00:02:00"
          }
        ]
      })
    );

  global.fetch = fetchMock as typeof fetch;

  render(<RankingPage />);

  await waitFor(() => {
    expect(screen.getByText("Alpha Team standings")).toBeTruthy();
  });

  expect(screen.getByText(/puesto #1 con 200 pts/i)).toBeTruthy();
  expect(screen.getByText("Tu equipo")).toBeTruthy();
});

test("applies realtime ranking updates from SignalR", async () => {
  const fetchMock = jest.fn()
    .mockResolvedValueOnce(
      createJsonResponse({
        liveSessionId: "live-session-1",
        sessionTeamId: "team-1",
        teamName: "Alpha Team",
        sessionState: "Running",
        progressState: "InProgress",
        currentStage: {
          missionStageId: "stage-1",
          name: "Decode the seal",
          sessionStageOrder: 1,
          sourceOrder: 10,
          resolvedTimeBudgetMinutes: 15,
          difficulty: "Medium",
          gameType: "Trivia"
        },
        visibleHints: [],
        sync: {
          sequenceNumber: 1,
          lastUpdatedUtc: "2026-06-03T08:00:00Z",
          serverTimeUtc: "2026-06-03T08:00:00Z"
        }
      })
    )
    .mockResolvedValueOnce(
      createJsonResponse({
        liveSessionId: "live-session-1",
        generatedAtUtc: "2026-06-03T08:00:00Z",
        items: []
      })
    );

  global.fetch = fetchMock as typeof fetch;

  render(<RankingPage />);

  await waitFor(() => {
    expect(signalRHandlers.ReceiveRankingUpdated).toBeDefined();
  });

  await act(async () => {
    signalRHandlers.ReceiveRankingUpdated({
      liveSessionId: "live-session-1",
      generatedAtUtc: "2026-06-03T08:01:00Z",
      items: [
        {
          rank: 2,
          sessionTeamId: "team-1",
          visibleScore: 100,
          resolutionTime: "00:03:30"
        }
      ]
    });
  });

  expect(screen.getByText(/puesto #2 con 100 pts/i)).toBeTruthy();
});
