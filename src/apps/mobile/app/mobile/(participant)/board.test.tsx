import { act, fireEvent, render, screen, waitFor } from "@testing-library/react-native";
import BoardPage from "./board";
import {
  createAuthorizedApiClient,
  SnapshotRefreshPolicies,
  type HintUnlockedPayload,
  type SessionStateChangedPayload,
  type SessionTeamSnapshot
} from "../../../src/lib/api-client";
import { loadStoredEnrollment } from "../../../src/lib/session-storage";
import { useSessionManagementConnection } from "../../../src/hooks/use-session-management-connection";
import { useSession } from "../../../src/providers/session-provider";

jest.mock("expo-router", () => ({
  Redirect: ({ href }: { href: string }) => `Redirect:${href}`,
  useRouter: () => ({ push: jest.fn(), replace: jest.fn() })
}));

jest.mock("../../../src/providers/session-provider", () => ({
  useSession: jest.fn()
}));

jest.mock("../../../src/lib/session-storage", () => ({
  loadStoredEnrollment: jest.fn()
}));

jest.mock("../../../src/hooks/use-session-management-connection", () => ({
  useSessionManagementConnection: jest.fn()
}));

jest.mock("../../../src/lib/config", () => ({
  getClientConfig: () => ({
    edgeProxyPublicBaseUrl: "https://edge.test",
    keycloakPublicBaseUrl: "https://keycloak.test",
    keycloakRealm: "umbral",
    keycloakClientId: "mobile",
    sessionHubUrl: "https://edge.test/session-hub/hubs/session"
  })
}));

jest.mock("../../../src/lib/api-client", () => {
  const actual = jest.requireActual("../../../src/lib/api-client");

  return {
    ...actual,
    createAuthorizedApiClient: jest.fn()
  };
});

type MockBoardApiClient = {
  getSessionTeamSnapshot: jest.Mock<Promise<SessionTeamSnapshot>, [string]>;
  submitEvidence: jest.Mock<Promise<{ validationOutcome: string }>, [{ sessionTeamId: string; qrHash: string }]>;
  submitTriviaAnswer: jest.Mock<
    Promise<{ validationOutcome: string }>,
    [{ sessionTeamId: string; selectedChoiceId: string }]
  >;
};

type ConnectionOptions = {
  accessToken: string;
  hubUrl: string;
  enabled?: boolean;
  onResync?: () => void;
};

type SignalRHandler = (payload: unknown) => void;

const signalRHandlers: Record<string, SignalRHandler> = {};
let latestConnectionOptions: ConnectionOptions | null = null;
let mockConnectionState: {
  kind: "connected" | "reconnecting" | "error" | "disconnected" | "connecting";
  detail: string;
  connection: typeof mockConnection | null;
};

const mockConnection = {
  on: jest.fn((eventName: string, handler: SignalRHandler) => {
    signalRHandlers[eventName] = handler;
  }),
  off: jest.fn((eventName: string, handler: SignalRHandler) => {
    if (signalRHandlers[eventName] === handler) {
      delete signalRHandlers[eventName];
    }
  })
};

function createMockApiClient(): MockBoardApiClient {
  return {
    getSessionTeamSnapshot: jest.fn(),
    submitEvidence: jest.fn().mockResolvedValue({ validationOutcome: "Accepted" }),
    submitTriviaAnswer: jest.fn().mockResolvedValue({ validationOutcome: "Accepted" })
  };
}

function createSnapshot(overrides: Partial<SessionTeamSnapshot> = {}): SessionTeamSnapshot {
  return {
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
      gameType: "Trivia",
      prompt: "Decode the message hidden in the seal.",
      choices: [
        { id: "choice-1", text: "Aurora" },
        { id: "choice-2", text: "Eclipse" },
        { id: "choice-3", text: "Nebula" }
      ]
    },
    visibleHints: [
      {
        hintId: "hint-1",
        missionStageId: "stage-1",
        content: "Look for the blue sigil.",
        isSolution: false,
        unlockedAtUtc: "2026-06-03T08:00:00Z",
        unlockReason: "Manual"
      }
    ],
    sync: {
      sequenceNumber: 1,
      lastUpdatedUtc: "2026-06-03T08:00:00Z",
      serverTimeUtc: "2026-06-03T08:00:00Z"
    },
    ...overrides
  };
}

function createTreasureHuntSnapshot(overrides: Partial<SessionTeamSnapshot> = {}): SessionTeamSnapshot {
  const snapshot = createSnapshot(overrides);

  return {
    ...snapshot,
    currentStage: snapshot.currentStage
      ? {
          ...snapshot.currentStage,
          gameType: "TreasureHunt",
          choices: []
        }
      : snapshot.currentStage
  };
}

function renderBoard(apiClient = createMockApiClient()) {
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
  (createAuthorizedApiClient as jest.Mock).mockReturnValue(apiClient);

  render(<BoardPage />);

  return apiClient;
}

beforeEach(() => {
  jest.clearAllMocks();
  latestConnectionOptions = null;
  Object.keys(signalRHandlers).forEach((eventName) => delete signalRHandlers[eventName]);
  mockConnectionState = {
    kind: "connected",
    detail: "Realtime participant stream connected.",
    connection: mockConnection
  };
  (useSessionManagementConnection as jest.Mock).mockImplementation((options: ConnectionOptions) => {
    latestConnectionOptions = options;
    return mockConnectionState;
  });
});

test("loads the Session Team snapshot on mount and renders current board data", async () => {
  const apiClient = createMockApiClient();
  apiClient.getSessionTeamSnapshot.mockResolvedValue(createSnapshot());

  renderBoard(apiClient);

  await waitFor(() => {
    expect(screen.getByText("Decode the seal")).toBeTruthy();
  });
  expect(apiClient.getSessionTeamSnapshot).toHaveBeenCalledWith("team-1");
  expect(screen.getByText("Equipo Alpha Team")).toBeTruthy();
  expect(screen.getByText("Look for the blue sigil.")).toBeTruthy();
  expect(screen.getByText("En juego")).toBeTruthy();
});

test("applies incremental SignalR hint unlocks to the visible hints list", async () => {
  const apiClient = createMockApiClient();
  apiClient.getSessionTeamSnapshot.mockResolvedValue(createSnapshot());

  renderBoard(apiClient);

  await waitFor(() => {
    expect(signalRHandlers.ReceiveHintUnlocked).toBeDefined();
  });
  await waitFor(() => {
    expect(screen.getByText("Look for the blue sigil.")).toBeTruthy();
  });

  const hintPayload: HintUnlockedPayload = {
    metadata: {
      liveSessionId: "live-session-1",
      sequenceNumber: 2,
      occurredAtUtc: "2026-06-03T08:01:00Z",
      refreshPolicy: SnapshotRefreshPolicies.applyIncremental,
      reason: "Hint released by operator"
    },
    sessionTeamId: "team-1",
    hint: {
      hintId: "hint-2",
      missionStageId: "stage-1",
      content: "The second symbol points north.",
      isSolution: false,
      unlockedAtUtc: "2026-06-03T08:01:00Z",
      unlockReason: "Manual"
    }
  };

  await act(async () => {
    signalRHandlers.ReceiveHintUnlocked(hintPayload);
  });

  expect(screen.getByText("The second symbol points north.")).toBeTruthy();
});

test("renders a static map only when the unlocked hint includes coordinates", async () => {
  const apiClient = createMockApiClient();
  apiClient.getSessionTeamSnapshot.mockResolvedValue(
    createSnapshot({
      visibleHints: [
        {
          hintId: "hint-1",
          missionStageId: "stage-1",
          content: "Meet beside the plaza fountain.",
          isSolution: false,
          latitude: 10.50001,
          longitude: -66.90001,
          unlockedAtUtc: "2026-06-03T08:00:00Z",
          unlockReason: "Manual"
        },
        {
          hintId: "hint-2",
          missionStageId: "stage-1",
          content: "Fallback text-only hint.",
          isSolution: false,
          unlockedAtUtc: "2026-06-03T08:02:00Z",
          unlockReason: "Rule"
        }
      ]
    })
  );

  renderBoard(apiClient);

  await waitFor(() => {
    expect(screen.getByText("Mapa de la pista")).toBeTruthy();
  });

  expect(screen.getByText(/Lat 10.50001 \| Lon -66.90001/)).toBeTruthy();
});

test("renders final mission resolutions with stage metadata, solutions and maps", async () => {
  const apiClient = createMockApiClient();
  apiClient.getSessionTeamSnapshot.mockResolvedValue(
    createSnapshot({
      sessionState: "Finalized",
      currentStage: undefined,
      progressState: "Completed",
      allStages: [
        {
          missionStageId: "stage-1",
          name: "Decode the seal",
          sessionStageOrder: 1,
          sourceOrder: 10,
          resolvedTimeBudgetMinutes: 15,
          difficulty: "Medium",
          gameType: "Trivia",
          prompt: "Decode the message hidden in the seal.",
          choices: [
            { id: "choice-1", text: "Aurora" },
            { id: "choice-2", text: "Eclipse" }
          ]
        },
        {
          missionStageId: "stage-2",
          name: "Find the archive gate",
          sessionStageOrder: 2,
          sourceOrder: 20,
          resolvedTimeBudgetMinutes: 10,
          difficulty: "Hard",
          gameType: "TreasureHunt",
          prompt: "Find the archive gate marker.",
          choices: []
        }
      ],
      visibleHints: [
        {
          hintId: "hint-1",
          missionStageId: "stage-1",
          content: "Look for the blue sigil.",
          isSolution: false,
          unlockedAtUtc: "2026-06-03T08:00:00Z",
          unlockReason: "Manual"
        },
        {
          hintId: "solution-1",
          missionStageId: "stage-1",
          content: "The answer is aurora.",
          isSolution: true,
          latitude: 10.50001,
          longitude: -66.90001,
          unlockedAtUtc: "2026-06-03T09:00:00Z",
          unlockReason: "Rule"
        }
      ]
    })
  );

  renderBoard(apiClient);

  await waitFor(() => {
    expect(screen.getByText("Resoluciones de la Misión")).toBeTruthy();
  });

  expect(screen.getByText("Decode the seal")).toBeTruthy();
  expect(screen.getByText("Etapa 1")).toBeTruthy();
  expect(screen.getByText("The answer is aurora.")).toBeTruthy();
  expect(screen.getByText("Solución")).toBeTruthy();
  expect(screen.getByText(/Lat 10.50001 \| Lon -66.90001/)).toBeTruthy();
  expect(screen.getByText("¡Sesión finalizada!")).toBeTruthy();
  expect(screen.queryByText("Eclipse")).toBeNull();
});

test("keeps final solutions out of live Visible hints before finalization", async () => {
  const apiClient = createMockApiClient();
  apiClient.getSessionTeamSnapshot.mockResolvedValue(
    createSnapshot({
      visibleHints: [
        {
          hintId: "hint-1",
          missionStageId: "stage-1",
          content: "Look for the blue sigil.",
          isSolution: false,
          unlockedAtUtc: "2026-06-03T08:00:00Z",
          unlockReason: "Manual"
        },
        {
          hintId: "solution-1",
          missionStageId: "stage-1",
          content: "The answer is aurora.",
          isSolution: true,
          unlockedAtUtc: "2026-06-03T09:00:00Z",
          unlockReason: "Rule"
        }
      ]
    })
  );

  renderBoard(apiClient);

  await waitFor(() => {
    expect(screen.getByText("Look for the blue sigil.")).toBeTruthy();
  });

  expect(screen.queryByText("Resoluciones de la Misión")).toBeNull();
  expect(screen.queryByText("The answer is aurora.")).toBeNull();
});

test("does not duplicate a hint when ReceiveHintUnlocked repeats an existing snapshot hint", async () => {
  const apiClient = createMockApiClient();
  apiClient.getSessionTeamSnapshot.mockResolvedValue(createSnapshot());

  renderBoard(apiClient);

  await waitFor(() => {
    expect(signalRHandlers.ReceiveHintUnlocked).toBeDefined();
  });
  await waitFor(() => {
    expect(screen.getByText("Look for the blue sigil.")).toBeTruthy();
  });

  const duplicatePayload: HintUnlockedPayload = {
    metadata: {
      liveSessionId: "live-session-1",
      sequenceNumber: 2,
      occurredAtUtc: "2026-06-03T08:01:00Z",
      refreshPolicy: SnapshotRefreshPolicies.applyIncremental,
      reason: "Hint release event delivered twice"
    },
    sessionTeamId: "team-1",
    hint: {
      hintId: "hint-1",
      missionStageId: "stage-1",
      content: "Duplicate delivery should not render.",
      isSolution: false,
      unlockedAtUtc: "2026-06-03T08:01:00Z",
      unlockReason: "Manual"
    }
  };

  await act(async () => {
    signalRHandlers.ReceiveHintUnlocked(duplicatePayload);
  });

  expect(screen.getAllByText("Look for the blue sigil.")).toHaveLength(1);
  expect(screen.queryByText("Duplicate delivery should not render.")).toBeNull();
});

test("renders timer and session state after realtime session state update", async () => {
  const apiClient = createMockApiClient();
  apiClient.getSessionTeamSnapshot.mockResolvedValue(createSnapshot());

  renderBoard(apiClient);

  await waitFor(() => {
    expect(signalRHandlers.ReceiveSessionStateChanged).toBeDefined();
  });
  await waitFor(() => {
    expect(screen.getByText("Decode the seal")).toBeTruthy();
  });

  const statePayload: SessionStateChangedPayload = {
    metadata: {
      liveSessionId: "live-session-1",
      sequenceNumber: 2,
      occurredAtUtc: "2026-06-03T08:02:00Z",
      refreshPolicy: SnapshotRefreshPolicies.applyIncremental,
      reason: "Session paused by operator"
    },
    previousState: "Running",
    currentState: "Paused",
    remainingSeconds: 125
  };

  await act(async () => {
    signalRHandlers.ReceiveSessionStateChanged(statePayload);
  });

  expect(screen.getByText("En pausa")).toBeTruthy();
  expect(screen.getByText("02:05")).toBeTruthy();
});

test("shows degraded connection state while SignalR reconnects", async () => {
  const apiClient = createMockApiClient();
  apiClient.getSessionTeamSnapshot.mockResolvedValue(createSnapshot());
  mockConnectionState = {
    kind: "reconnecting",
    detail: "Connection dropped. Waiting for SignalR reconnect.",
    connection: null
  };

  renderBoard(apiClient);

  await waitFor(() => {
    expect(screen.getByText("Reconectando")).toBeTruthy();
  });
});

test("resync callback fetches a fresh snapshot after SignalR reconnect", async () => {
  const apiClient = createMockApiClient();
  apiClient.getSessionTeamSnapshot
    .mockResolvedValueOnce(createSnapshot())
    .mockResolvedValueOnce(
      createSnapshot({
        currentStage: {
          missionStageId: "stage-2",
          name: "Open the archive gate",
          sessionStageOrder: 2,
          sourceOrder: 20,
          resolvedTimeBudgetMinutes: 10,
          difficulty: "Hard",
          gameType: "TreasureHunt",
          prompt: "Find the archive gate marker.",
          choices: []
        },
        sync: {
          sequenceNumber: 3,
          lastUpdatedUtc: "2026-06-03T08:03:00Z",
          serverTimeUtc: "2026-06-03T08:03:00Z"
        }
      })
    );

  renderBoard(apiClient);

  await waitFor(() => {
    expect(apiClient.getSessionTeamSnapshot).toHaveBeenCalledTimes(1);
  });

  await act(async () => {
    await latestConnectionOptions?.onResync?.();
  });

  await waitFor(() => {
    expect(apiClient.getSessionTeamSnapshot).toHaveBeenCalledTimes(2);
  });
  expect(screen.getByText("Open the archive gate")).toBeTruthy();
});

test("submits scanned QR evidence and renders accepted feedback", async () => {
  const apiClient = createMockApiClient();
  apiClient.getSessionTeamSnapshot.mockResolvedValue(createTreasureHuntSnapshot());
  apiClient.submitEvidence.mockResolvedValue({ validationOutcome: "Accepted" });

  renderBoard(apiClient);

  await waitFor(() => {
    expect(screen.getByText("Escanear QR")).toBeTruthy();
  });

  fireEvent.press(screen.getByText("Escanear QR"));

  await waitFor(() => {
    expect(screen.getByTestId("camera-view")).toBeTruthy();
  });

  await act(async () => {
    screen.getByTestId("camera-view").props.onBarcodeScanned({ data: " qr-hash-accepted " });
  });

  await waitFor(() => {
    expect(apiClient.submitEvidence).toHaveBeenCalledWith({
      sessionTeamId: "team-1",
      qrHash: "qr-hash-accepted"
    });
  });
  expect(screen.getByText(/Evidencia Aceptada/)).toBeTruthy();
});

test("submits scanned QR evidence and renders rejected feedback", async () => {
  const apiClient = createMockApiClient();
  apiClient.getSessionTeamSnapshot.mockResolvedValue(createTreasureHuntSnapshot());
  apiClient.submitEvidence.mockResolvedValue({ validationOutcome: "Rejected" });

  renderBoard(apiClient);

  await waitFor(() => {
    expect(screen.getByText("Escanear QR")).toBeTruthy();
  });

  fireEvent.press(screen.getByText("Escanear QR"));

  await waitFor(() => {
    expect(screen.getByTestId("camera-view")).toBeTruthy();
  });

  await act(async () => {
    screen.getByTestId("camera-view").props.onBarcodeScanned({ data: "wrong-hash" });
  });

  await waitFor(() => {
    expect(apiClient.submitEvidence).toHaveBeenCalledWith({
      sessionTeamId: "team-1",
      qrHash: "wrong-hash"
    });
  });
  expect(screen.getByText(/Código incorrecto, inténtalo de nuevo/)).toBeTruthy();
});

test("submits the selected Trivia choice and renders accepted feedback", async () => {
  const apiClient = createMockApiClient();
  apiClient.getSessionTeamSnapshot.mockResolvedValue(createSnapshot());
  apiClient.submitTriviaAnswer.mockResolvedValue({ validationOutcome: "Accepted" });

  renderBoard(apiClient);

  await waitFor(() => {
    expect(screen.getByText("Eclipse")).toBeTruthy();
  });

  fireEvent.press(screen.getByText("Eclipse"));
  fireEvent.press(screen.getByText("Enviar"));

  await waitFor(() => {
    expect(apiClient.submitTriviaAnswer).toHaveBeenCalledWith({
      sessionTeamId: "team-1",
      selectedChoiceId: "choice-2"
    });
  });
  expect(screen.getByText(/Respuesta correcta/)).toBeTruthy();
});

test("disables submit until a Trivia choice is selected", async () => {
  const apiClient = createMockApiClient();
  apiClient.getSessionTeamSnapshot.mockResolvedValue(createSnapshot());

  renderBoard(apiClient);

  await waitFor(() => {
    expect(screen.getByText("Aurora")).toBeTruthy();
  });

  fireEvent.press(screen.getByText("Enviar"));

  expect(apiClient.submitTriviaAnswer).not.toHaveBeenCalled();
});

test("submits the selected Trivia choice and renders rejected feedback", async () => {
  const apiClient = createMockApiClient();
  apiClient.getSessionTeamSnapshot.mockResolvedValue(createSnapshot());
  apiClient.submitTriviaAnswer.mockResolvedValue({ validationOutcome: "Rejected" });

  renderBoard(apiClient);

  await waitFor(() => {
    expect(screen.getByText("Nebula")).toBeTruthy();
  });

  fireEvent.press(screen.getByText("Nebula"));
  fireEvent.press(screen.getByText("Enviar"));

  await waitFor(() => {
    expect(apiClient.submitTriviaAnswer).toHaveBeenCalledWith({
      sessionTeamId: "team-1",
      selectedChoiceId: "choice-3"
    });
  });
  expect(screen.getByText(/Respuesta incorrecta, intenta de nuevo/)).toBeTruthy();
});
