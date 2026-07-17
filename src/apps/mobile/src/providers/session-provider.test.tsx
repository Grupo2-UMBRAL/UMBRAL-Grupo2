import { fireEvent, render, screen, waitFor } from "@testing-library/react-native";
import { Pressable, Text } from "react-native";
import { loginWithPassword } from "../lib/auth";
import {
  clearStoredEnrollment,
  clearStoredSession,
  loadStoredSession,
  resetOnboardingState,
  saveStoredSession,
  type UmbralMobileSession
} from "../lib/session-storage";
import { SessionProvider, useSession } from "./session-provider";

jest.mock("../lib/auth", () => ({
  loginWithPassword: jest.fn(),
  refreshSession: jest.fn()
}));

jest.mock("../lib/session-storage", () => ({
  clearStoredEnrollment: jest.fn(),
  clearStoredSession: jest.fn(),
  isSessionExpired: jest.fn(() => false),
  loadStoredSession: jest.fn(),
  resetOnboardingState: jest.fn(),
  saveStoredSession: jest.fn()
}));

const session: UmbralMobileSession = {
  accessToken: "participant-token",
  expiresAt: "2099-01-01T00:00:00Z",
  roles: ["Participant"],
  username: "participant",
  displayName: "Participante"
};

function SignInButton() {
  const { signIn } = useSession();

  return (
    <Pressable onPress={() => void signIn("participant", "password")}>
      <Text>Entrar</Text>
    </Pressable>
  );
}

function SessionHydrationState() {
  const { loading, session } = useSession();

  return <Text>{loading ? "Cargando" : session ? "Autenticado" : "Sin sesión"}</Text>;
}

function ReauthenticateButton() {
  const { requireReauthentication } = useSession();

  return (
    <Pressable onPress={() => void requireReauthentication()}>
      <Text>Reautenticar</Text>
    </Pressable>
  );
}

beforeEach(() => {
  jest.clearAllMocks();
  (loadStoredSession as jest.Mock).mockResolvedValue(null);
  (loginWithPassword as jest.Mock).mockResolvedValue(session);
  (saveStoredSession as jest.Mock).mockResolvedValue(undefined);
  (resetOnboardingState as jest.Mock).mockResolvedValue(undefined);
});

test("resets onboarding after each successful sign-in", async () => {
  render(
    <SessionProvider>
      <SignInButton />
    </SessionProvider>
  );

  fireEvent.press(screen.getByText("Entrar"));

  await waitFor(() => {
    expect(saveStoredSession).toHaveBeenCalledWith(session);
    expect(resetOnboardingState).toHaveBeenCalledTimes(1);
  });
});

test("finishes hydration as signed out when local session storage is unavailable", async () => {
  (loadStoredSession as jest.Mock).mockRejectedValue(new Error("Storage disabled"));

  render(
    <SessionProvider>
      <SessionHydrationState />
    </SessionProvider>
  );

  await waitFor(() => {
    expect(screen.getByText("Sin sesión")).toBeTruthy();
  });
});

test("keeps the Session Team context when authentication must be renewed", async () => {
  render(
    <SessionProvider>
      <ReauthenticateButton />
    </SessionProvider>
  );

  fireEvent.press(screen.getByText("Reautenticar"));

  await waitFor(() => {
    expect(clearStoredSession).toHaveBeenCalledTimes(1);
  });
  expect(clearStoredEnrollment).not.toHaveBeenCalled();
});
