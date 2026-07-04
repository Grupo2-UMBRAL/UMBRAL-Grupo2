import { useRouter } from "expo-router";
import { useEffect, useMemo, useState } from "react";
import { Pressable, StyleSheet, Text, TextInput, View } from "react-native";
import { GameButton } from "../../../src/components/game-button";
import { ScreenShell, shellStyles } from "../../../src/components/screen-shell";
import { StatusChip } from "../../../src/components/status-chip";
import { colors } from "../../../src/theme/tokens";
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
      return "Código de sesión no válido o no encontrado.";
    }

    if (error.status === 409) {
      return error.message || "Las inscripciones están cerradas o el equipo no está disponible.";
    }

    if (error.status >= 500) {
      return "El servidor no respondió. Vuelve a intentarlo.";
    }

    return error.message;
  }

  return "No pudimos conectar con la sesión.";
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
      setErrorMessage("El nombre del equipo debe tener al menos 3 caracteres.");
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
      eyebrow="Únete a una sesión"
      title="Entra con tu equipo"
      description="Pídele el código a tu operador, escríbelo abajo y únete o crea tu equipo."
    >
      <View style={shellStyles.card}>
        <Text style={shellStyles.cardTitle}>Código de sesión</Text>
        <TextInput
          autoCapitalize="characters"
          autoCorrect={false}
          onChangeText={(value) => setJoinCode(normalizeJoinCode(value))}
          placeholder="ABC234"
          placeholderTextColor={colors.text.mutedAlt}
          style={styles.input}
          value={joinCode}
        />
        {validationState === "checking" ? (
          <Text style={shellStyles.cardText}>Comprobando código…</Text>
        ) : null}
        {validationState === "open" ? (
          <StatusChip label="¡Código válido! Elige tu equipo" tone="success" />
        ) : null}
        {validationState === "closed" ? (
          <Text style={styles.warning}>
            Las inscripciones están cerradas. Pídele al operador que las abra para poder entrar.
          </Text>
        ) : null}
        {errorMessage ? <Text style={styles.error}>{errorMessage}</Text> : null}
      </View>

      {validationState === "open" ? (
        <View style={shellStyles.card}>
          <Text style={shellStyles.cardTitle}>Tu equipo</Text>
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
                Unirme a uno
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
                Crear uno
              </Text>
            </Pressable>
          </View>

          {enrollmentMode === "joinExisting" ? (
            <View style={shellStyles.list}>
              {teams.length === 0 ? (
                <Text style={shellStyles.cardText}>Aún no hay equipos. ¡Crea el primero!</Text>
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
                  {selectedTeamId === team.id ? <Text style={styles.teamCheck}>✓</Text> : null}
                </Pressable>
              ))}
              <GameButton
                label={submissionState === "submitting" ? "Uniéndote..." : "Unirme al equipo"}
                icon="🚪"
                disabled={!canJoinExisting}
                loading={submissionState === "submitting"}
                onPress={() => void handleJoinExistingTeam()}
              />
            </View>
          ) : (
            <View style={shellStyles.list}>
              <TextInput
                onChangeText={setTeamName}
                placeholder="Nombre del equipo"
                placeholderTextColor={colors.text.mutedAlt}
                style={styles.input}
                value={teamName}
              />
              <GameButton
                label={submissionState === "submitting" ? "Creando..." : "Crear equipo"}
                icon="✨"
                disabled={!canCreateTeam}
                loading={submissionState === "submitting"}
                onPress={() => void handleCreateTeam()}
              />
            </View>
          )}
        </View>
      ) : null}
    </ScreenShell>
  );
}

const styles = StyleSheet.create({
  input: {
    backgroundColor: colors.surface.card,
    borderRadius: 16,
    borderWidth: 1,
    borderColor: colors.surface.cardBorder,
    color: colors.text.primary,
    fontSize: 16,
    paddingHorizontal: 16,
    paddingVertical: 14
  },
  modeButton: {
    backgroundColor: colors.surface.card,
    borderRadius: 999,
    borderWidth: 1,
    borderColor: colors.surface.cardBorder,
    paddingHorizontal: 16,
    paddingVertical: 10
  },
  modeButtonActive: {
    backgroundColor: colors.brand.secondary,
    borderColor: colors.brand.secondary
  },
  modeButtonLabel: {
    color: colors.text.secondary,
    fontSize: 14,
    fontWeight: "700"
  },
  modeButtonLabelActive: {
    color: colors.text.onBrand
  },
  teamCard: {
    alignItems: "center",
    backgroundColor: colors.surface.card,
    borderRadius: 18,
    borderWidth: 1,
    borderColor: colors.surface.cardBorder,
    flexDirection: "row",
    justifyContent: "space-between",
    padding: 16
  },
  teamCardSelected: {
    borderColor: colors.brand.secondary,
    borderWidth: 2,
    backgroundColor: colors.brand.secondaryTint
  },
  teamName: {
    color: colors.text.primary,
    fontSize: 16,
    fontWeight: "700"
  },
  teamCheck: {
    color: colors.brand.secondaryRing,
    fontSize: 18,
    fontWeight: "900"
  },
  warning: {
    color: colors.state.warn.text,
    fontSize: 14,
    lineHeight: 20
  },
  error: {
    color: colors.state.error.text,
    fontSize: 14,
    lineHeight: 20
  },
  buttonPressed: {
    opacity: 0.85
  }
});
