import { fireEvent, render, screen, waitFor } from "@testing-library/react-native";
import JoinPage from "./join";
import {
  ApiClientError,
  createAuthorizedApiClient,
  type EnrollmentResult,
  type ParticipantEnrollmentStatus,
  type SessionTeamsResult
} from "../../../src/lib/api-client";
import {
  loadStoredEnrollment,
  saveStoredEnrollment
} from "../../../src/lib/session-storage";
import { useSession } from "../../../src/providers/session-provider";

const mockReplace = jest.fn();

jest.mock("expo-router", () => ({
  useRouter: () => ({
    replace: mockReplace
  })
}));

jest.mock("../../../src/providers/session-provider", () => ({
  useSession: jest.fn()
}));

jest.mock("../../../src/lib/session-storage", () => ({
  loadStoredEnrollment: jest.fn(),
  saveStoredEnrollment: jest.fn()
}));

jest.mock("../../../src/lib/api-client", () => {
  const actual = jest.requireActual("../../../src/lib/api-client");

  return {
    ...actual,
    createAuthorizedApiClient: jest.fn()
  };
});

type MockEnrollmentApiClient = {
  validateEnrollmentJoinCode: jest.Mock<Promise<ParticipantEnrollmentStatus>, [string]>;
  listEnrollmentTeams: jest.Mock<Promise<SessionTeamsResult>, [string]>;
  joinSessionTeam: jest.Mock<Promise<EnrollmentResult>, [{ joinCode: string; sessionTeamId: string }]>;
  createSessionTeam: jest.Mock<Promise<EnrollmentResult>, [{ joinCode: string; teamName: string }]>;
};

function createMockApiClient(): MockEnrollmentApiClient {
  return {
    validateEnrollmentJoinCode: jest.fn(),
    listEnrollmentTeams: jest.fn(),
    joinSessionTeam: jest.fn(),
    createSessionTeam: jest.fn()
  };
}

function openEnrollmentStatus(): ParticipantEnrollmentStatus {
  return {
    liveSessionId: "live-session-1",
    sessionState: "Scheduled",
    openedAtUtc: "2026-06-03T00:00:00Z",
    closedAtUtc: null,
    isOpen: true
  };
}

function closedEnrollmentStatus(): ParticipantEnrollmentStatus {
  return {
    ...openEnrollmentStatus(),
    isOpen: false,
    closedAtUtc: "2026-06-03T00:05:00Z"
  };
}

function renderJoinPage(apiClient = createMockApiClient()) {
  (useSession as jest.Mock).mockReturnValue({
    session: {
      accessToken: "participant-token",
      expiresAt: "2026-06-03T01:00:00Z",
      roles: ["Participant"],
      username: "participant",
      displayName: "Participant One"
    }
  });
  (createAuthorizedApiClient as jest.Mock).mockReturnValue(apiClient);

  render(<JoinPage />);

  return apiClient;
}

beforeEach(() => {
  jest.clearAllMocks();
  (loadStoredEnrollment as jest.Mock).mockResolvedValue(null);
  (saveStoredEnrollment as jest.Mock).mockResolvedValue(undefined);
});

test("redirects to board when stored enrollment already exists", async () => {
  (loadStoredEnrollment as jest.Mock).mockResolvedValue({
    joinCode: "ABC234",
    teamId: "team-1",
    teamName: "Alpha Team"
  });

  renderJoinPage();

  await waitFor(() => {
    expect(mockReplace).toHaveBeenCalledWith("/mobile/board");
  });
});

test("shows unavailable session error when join code is invalid", async () => {
  const apiClient = createMockApiClient();
  apiClient.validateEnrollmentJoinCode.mockRejectedValue(
    new ApiClientError("Join Code is invalid for this LiveSession.", 404, "join_code_invalid_for_live_session")
  );

  renderJoinPage(apiClient);

  fireEvent.changeText(screen.getByPlaceholderText("ABC234"), "bad999");

  await waitFor(() => {
    expect(screen.getByText("Código de sesión no válido o no encontrado.")).toBeTruthy();
  });
  expect(apiClient.listEnrollmentTeams).not.toHaveBeenCalled();
});

test("blocks team actions and shows warning when enrollment window is closed", async () => {
  const apiClient = createMockApiClient();
  apiClient.validateEnrollmentJoinCode.mockResolvedValue(closedEnrollmentStatus());

  renderJoinPage(apiClient);

  fireEvent.changeText(screen.getByPlaceholderText("ABC234"), "abc234");

  await waitFor(() => {
    expect(
      screen.getByText("Las inscripciones están cerradas. Pídele al operador que las abra para poder entrar.")
    ).toBeTruthy();
  });
  expect(screen.queryByText("Unirme al equipo")).toBeNull();
  expect(screen.queryByText("Crear equipo")).toBeNull();
  expect(apiClient.listEnrollmentTeams).not.toHaveBeenCalled();
});

test("joins existing team and stores enrollment context", async () => {
  const apiClient = createMockApiClient();
  apiClient.validateEnrollmentJoinCode.mockResolvedValue(openEnrollmentStatus());
  apiClient.listEnrollmentTeams.mockResolvedValue({
    liveSessionId: "live-session-1",
    isOpen: true,
    teams: [{ id: "team-1", name: "Alpha Team" }]
  });
  apiClient.joinSessionTeam.mockResolvedValue({
    liveSessionId: "live-session-1",
    sessionTeamId: "team-1",
    teamName: "Alpha Team",
    participantUserId: "participant",
    enrolledAtUtc: "2026-06-03T00:10:00Z"
  });

  renderJoinPage(apiClient);

  fireEvent.changeText(screen.getByPlaceholderText("ABC234"), "abc234");
  await waitFor(() => {
    expect(screen.getByText("Alpha Team")).toBeTruthy();
  });

  fireEvent.press(screen.getByText("Alpha Team"));
  fireEvent.press(screen.getByText("Unirme al equipo"));

  await waitFor(() => {
    expect(apiClient.joinSessionTeam).toHaveBeenCalledWith({
      joinCode: "ABC234",
      sessionTeamId: "team-1"
    });
  });
  expect(saveStoredEnrollment).toHaveBeenCalledWith({
    joinCode: "ABC234",
    teamId: "team-1",
    teamName: "Alpha Team"
  });
  expect(mockReplace).toHaveBeenCalledWith("/mobile/board");
});

test("creates team and stores enrollment context", async () => {
  const apiClient = createMockApiClient();
  apiClient.validateEnrollmentJoinCode.mockResolvedValue(openEnrollmentStatus());
  apiClient.listEnrollmentTeams.mockResolvedValue({
    liveSessionId: "live-session-1",
    isOpen: true,
    teams: []
  });
  apiClient.createSessionTeam.mockResolvedValue({
    liveSessionId: "live-session-1",
    sessionTeamId: "team-2",
    teamName: "New Team",
    participantUserId: "participant",
    registeredAtUtc: "2026-06-03T00:10:00Z"
  });

  renderJoinPage(apiClient);

  fireEvent.changeText(screen.getByPlaceholderText("ABC234"), "abc234");
  await waitFor(() => {
    expect(screen.getByText("Aún no hay equipos. ¡Crea el primero!")).toBeTruthy();
  });

  fireEvent.press(screen.getByText("Crear uno"));
  fireEvent.changeText(screen.getByPlaceholderText("Nombre del equipo"), "New Team");
  fireEvent.press(screen.getByText("Crear equipo"));

  await waitFor(() => {
    expect(apiClient.createSessionTeam).toHaveBeenCalledWith({
      joinCode: "ABC234",
      teamName: "New Team"
    });
  });
  expect(saveStoredEnrollment).toHaveBeenCalledWith({
    joinCode: "ABC234",
    teamId: "team-2",
    teamName: "New Team"
  });
  expect(mockReplace).toHaveBeenCalledWith("/mobile/board");
});
