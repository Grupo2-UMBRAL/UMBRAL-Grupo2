import { fireEvent, render, screen, waitFor } from "@testing-library/react-native";
import { Pressable, Text } from "react-native";
import { loginWithPassword } from "../lib/auth";
import {
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
