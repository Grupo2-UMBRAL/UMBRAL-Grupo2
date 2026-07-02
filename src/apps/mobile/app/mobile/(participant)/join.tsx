import { useRouter } from "expo-router";
import { useEffect, useMemo, useState } from "react";
import { Pressable, StyleSheet, Text, TextInput, View } from "react-native";
import { ScreenShell, shellStyles } from "../../../src/components/screen-shell";
import { StatusChip } from "../../../src/components/status-chip";
import {
  ApiClientError,
  createAuthorizedApiClient,
  type ParticipantEnrollmentStatus,
  type SessionTeam
} from "../../../src/lib/api-client";
import {
  loadStoredEnrollment,
  saveStoredEnrollment
} from "../../../src/lib/session-storage";
import { useSession } from "../../../src/providers/session-provider";

type ValidationState = "idle" | "checking" | "open" | "closed" | "invalid" | "error";
type EnrollmentMode = "joinExisting" | "createTeam";
type SubmissionState = "idle" | "submitting" | "success";

function normalizeJoinCode(value: string) {
  return value.replace(/\s/g, "").toUpperCase();
}

function readEnrollmentError(error: unknown) {
  if (error instanceof ApiClientError) {
    if (error.status === 404) {
      return "Session Join Code inválido o no registrado.";
    }

    if (error.status === 409) {
      return error.message || "La ventana de asignación está cerrada o el equipo no está disponible.";
    }

    if (error.status >= 500) {
      return "Session Operations no respondió correctamente. Intenta de nuevo.";
    }

    return error.message;
  }

  return "No se pudo conectar con Session Operations.";
}

export default function JoinPage() {
  const router = useRouter();
  const { session } = useSession();
  const [joinCode, setJoinCode] = useState("");
  const [teamName, setTeamName] = useState("");
  const [teams, setTeams] = useState<SessionTeam[]>([]);
  const [selectedTeamId, setSelectedTeamId] = useState<string | null>(null);
  const [enrollmentStatus, setEnrollmentStatus] = useState<ParticipantEnrollmentStatus | null>(null);
  const [validationState, setValidationState] = useState<ValidationState>("idle");
  const [enrollmentMode, setEnrollmentMode] = useState<EnrollmentMode>("joinExisting");
  const [submissionState, setSubmissionState] = useState<SubmissionState>("idle");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const apiClient = useMemo(
    () => (session ? createAuthorizedApiClient(session.accessToken) : null),
    [session]
  );

  useEffect(() => {
    let active = true;

    async function redirectStoredEnrollment() {
      const storedEnrollment = await loadStoredEnrollment();
      if (!active || !storedEnrollment) {
        return;
      }

      router.replace("/mobile/board");
    }

    void redirectStoredEnrollment();

    return () => {
      active = false;
    };
  }, [router]);

  useEffect(() => {
    if (!apiClient) {
      return;
    }

    const currentApiClient = apiClient;
    const normalizedJoinCode = normalizeJoinCode(joinCode);
    setEnrollmentStatus(null);
    setTeams([]);
    setSelectedTeamId(null);
    setSubmissionState("idle");

    if (normalizedJoinCode.length < 4) {
      setValidationState("idle");
      setErrorMessage(null);
      return;
    }

    let active = true;
    const timeout = setTimeout(() => {
      async function validateJoinCode() {
        setValidationState("checking");
        setErrorMessage(null);

        try {
          const status = await currentApiClient.validateEnrollmentJoinCode(normalizedJoinCode);
          if (!active) {
            return;
          }

          setEnrollmentStatus(status);

          if (!status.isOpen) {
            setValidationState("closed");
            return;
          }

          const teamsResult = await currentApiClient.listEnrollmentTeams(normalizedJoinCode);
          if (!active) {
            return;
          }

          setTeams(teamsResult.teams);
          setValidationState("open");
        } catch (error) {
          if (!active) {
            return;
          }

          const message = readEnrollmentError(error);
          setErrorMessage(message);
          setValidationState(error instanceof ApiClientError && error.status === 404 ? "invalid" : "error");
        }
      }

      void validateJoinCode();
    }, 450);

    return () => {
      active = false;
      clearTimeout(timeout);
    };
  }, [apiClient, joinCode]);

  if (!session || !apiClient) {
    return null;
  }

  const normalizedJoinCode = normalizeJoinCode(joinCode);
  const selectedTeam = teams.find((team) => team.id === selectedTeamId) ?? null;
  const canUseEnrollmentActions = validationState === "open" && submissionState !== "submitting";
  const canJoinExisting = canUseEnrollmentActions && Boolean(selectedTeam);
  const canCreateTeam = canUseEnrollmentActions && teamName.trim().length >= 3;

  async function persistEnrollment(teamId: string, nextTeamName: string) {
    await saveStoredEnrollment({
      joinCode: normalizedJoinCode,
      teamId,
      teamName: nextTeamName
    });
    setSubmissionState("success");
    router.replace("/mobile/board");
  }

  async function handleJoinExistingTeam() {
    if (!apiClient || !selectedTeam) {
      return;
    }

    setSubmissionState("submitting");
    setErrorMessage(null);

    try {
      const result = await apiClient.joinSessionTeam({
        joinCode: normalizedJoinCode,
        sessionTeamId: selectedTeam.id
      });
      await persistEnrollment(result.sessionTeamId, result.teamName);
    } catch (error) {
      setSubmissionState("idle");
      setErrorMessage(readEnrollmentError(error));
    }
  }

  async function handleCreateTeam() {
    if (!apiClient) {
      return;
    }

    const requestedTeamName = teamName.trim();
    if (requestedTeamName.length < 3) {
      setErrorMessage("Nombre de Session Team debe tener al menos 3 caracteres.");
      return;
    }

    setSubmissionState("submitting");
    setErrorMessage(null);

    try {
      const result = await apiClient.createSessionTeam({
        joinCode: normalizedJoinCode,
        teamName: requestedTeamName
      });
      await persistEnrollment(result.sessionTeamId, result.teamName);
    } catch (error) {
      setSubmissionState("idle");
      setErrorMessage(readEnrollmentError(error));
    }
  }

  return (
    <ScreenShell
      eyebrow="Session Enrollment"
      title="Join a LiveSession with your team."
      description="Enter the Session Join Code, wait for Team Assignment Window validation, then join or create a Session Team."
    >
      <View style={shellStyles.card}>
        <Text style={shellStyles.cardTitle}>Session Join Code</Text>
        <TextInput
          autoCapitalize="characters"
          autoCorrect={false}
          onChangeText={(value) => setJoinCode(normalizeJoinCode(value))}
          placeholder="ABC234"
          style={styles.input}
          value={joinCode}
        />
        <View style={shellStyles.row}>
          <StatusChip label={validationState} tone={validationState === "open" ? "success" : validationState === "closed" ? "warn" : validationState === "invalid" || validationState === "error" ? "error" : "info"} />
          {enrollmentStatus ? (
            <StatusChip label={enrollmentStatus.sessionState} tone="info" />
          ) : null}
        </View>
        {validationState === "checking" ? (
          <Text style={shellStyles.cardText}>Validando Join Code...</Text>
        ) : null}
        {validationState === "closed" ? (
          <Text style={styles.warning}>
            Team Assignment Window está cerrada. No puedes crear ni unirte a equipos ahora.
          </Text>
        ) : null}
        {errorMessage ? <Text style={styles.error}>{errorMessage}</Text> : null}
      </View>

      {validationState === "open" ? (
        <View style={shellStyles.card}>
          <Text style={shellStyles.cardTitle}>Choose Session Team</Text>
          <View style={shellStyles.row}>
            <Pressable
              onPress={() => setEnrollmentMode("joinExisting")}
              style={({ pressed }) => [
                styles.modeButton,
                enrollmentMode === "joinExisting" && styles.modeButtonActive,
                pressed && styles.buttonPressed
              ]}
            >
              <Text
                style={[
                  styles.modeButtonLabel,
                  enrollmentMode === "joinExisting" && styles.modeButtonLabelActive
                ]}
              >
                Join existing
              </Text>
            </Pressable>
            <Pressable
              onPress={() => setEnrollmentMode("createTeam")}
              style={({ pressed }) => [
                styles.modeButton,
                enrollmentMode === "createTeam" && styles.modeButtonActive,
                pressed && styles.buttonPressed
              ]}
            >
              <Text
                style={[
                  styles.modeButtonLabel,
                  enrollmentMode === "createTeam" && styles.modeButtonLabelActive
                ]}
              >
                Create team
              </Text>
            </Pressable>
          </View>

          {enrollmentMode === "joinExisting" ? (
            <View style={shellStyles.list}>
              {teams.length === 0 ? (
                <Text style={shellStyles.cardText}>No Session Teams yet. Create the first one.</Text>
              ) : null}
              {teams.map((team) => (
                <Pressable
                  key={team.id}
                  onPress={() => setSelectedTeamId(team.id)}
                  style={({ pressed }) => [
                    styles.teamCard,
                    selectedTeamId === team.id && styles.teamCardSelected,
                    pressed && styles.buttonPressed
                  ]}
                >
                  <Text style={styles.teamName}>{team.name}</Text>
                </Pressable>
              ))}
              <Pressable
                disabled={!canJoinExisting}
                onPress={() => {
                  void handleJoinExistingTeam();
                }}
                style={({ pressed }) => [
                  styles.primaryButton,
                  !canJoinExisting && styles.buttonDisabled,
                  pressed && canJoinExisting && styles.buttonPressed
                ]}
              >
                <Text style={styles.primaryButtonLabel}>
                  {submissionState === "submitting" ? "Joining..." : "Join selected team"}
                </Text>
              </Pressable>
            </View>
          ) : (
            <View style={shellStyles.list}>
              <TextInput
                onChangeText={setTeamName}
                placeholder="Session Team name"
                style={styles.input}
                value={teamName}
              />
              <Pressable
                disabled={!canCreateTeam}
                onPress={() => {
                  void handleCreateTeam();
                }}
                style={({ pressed }) => [
                  styles.primaryButton,
                  !canCreateTeam && styles.buttonDisabled,
                  pressed && canCreateTeam && styles.buttonPressed
                ]}
              >
                <Text style={styles.primaryButtonLabel}>
                  {submissionState === "submitting" ? "Creating..." : "Create Session Team"}
                </Text>
              </Pressable>
            </View>
          )}
        </View>
      ) : null}
    </ScreenShell>
  );
}

const styles = StyleSheet.create({
  input: {
    backgroundColor: "#ffffff",
    borderRadius: 16,
    borderWidth: 1,
    borderColor: "#E5E5E5",
    color: "#4B4B4B",
    fontSize: 16,
    paddingHorizontal: 16,
    paddingVertical: 14
  },
  primaryButton: {
    backgroundColor: "#1CB0F6",
    borderRadius: 18,
    alignItems: "center",
    paddingHorizontal: 16,
    paddingVertical: 14
  },
  primaryButtonLabel: {
    color: "#FFFFFF",
    fontSize: 15,
    fontWeight: "700"
  },
  modeButton: {
    backgroundColor: "#ffffff",
    borderRadius: 999,
    borderWidth: 1,
    borderColor: "#E5E5E5",
    paddingHorizontal: 14,
    paddingVertical: 10
  },
  modeButtonActive: {
    backgroundColor: "#4B4B4B",
    borderColor: "#4B4B4B"
  },
  modeButtonLabel: {
    color: "#777777",
    fontSize: 14,
    fontWeight: "700"
  },
  modeButtonLabelActive: {
    color: "#FFFFFF"
  },
  teamCard: {
    backgroundColor: "#ffffff",
    borderRadius: 18,
    borderWidth: 1,
    borderColor: "#E5E5E5",
    padding: 16
  },
  teamCardSelected: {
    borderColor: "#1CB0F6",
    backgroundColor: "#DDF4FF"
  },
  teamName: {
    color: "#4B4B4B",
    fontSize: 16,
    fontWeight: "700"
  },
  warning: {
    color: "#8C6E00",
    fontSize: 14,
    lineHeight: 20
  },
  error: {
    color: "#EA2B2B",
    fontSize: 14,
    lineHeight: 20
  },
  buttonDisabled: {
    opacity: 0.55
  },
  buttonPressed: {
    opacity: 0.85
  }
});

