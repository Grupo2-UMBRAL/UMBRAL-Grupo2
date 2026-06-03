import { CameraView, useCameraPermissions, type BarcodeScanningResult } from "expo-camera";
import { Redirect } from "expo-router";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { ActivityIndicator, Modal, Pressable, StyleSheet, Text, TextInput, View } from "react-native";
import { LoadingScreen } from "../../src/components/loading-screen";
import { ScreenShell, shellStyles } from "../../src/components/screen-shell";
import { StatusChip } from "../../src/components/status-chip";
import { useSessionOperationsConnection } from "../../src/hooks/use-session-operations-connection";
import {
  createAuthorizedApiClient,
  SnapshotRefreshPolicies,
  type HintUnlockedPayload,
  type RealtimeEventMetadata,
  type SessionStateChangedPayload,
  type SessionTeamSnapshot,
  type TeamProgressChangedPayload,
  type VisibleHintSnapshot
} from "../../src/lib/api-client";
import { getClientConfig } from "../../src/lib/config";
import {
  loadStoredEnrollment,
  type StoredEnrollment
} from "../../src/lib/session-storage";
import { useSession } from "../../src/providers/session-provider";

type SnapshotStatus = "idle" | "loading" | "fresh" | "resyncing" | "stale" | "error";

type QRSubmissionStatus =
  | "idle"
  | "requestingPermission"
  | "scanning"
  | "submitting"
  | "accepted"
  | "rejected"
  | "error";

const GameTypes = {
  treasureHunt: "treasurehunt",
  trivia: "trivia"
} as const;

type ApiClient = ReturnType<typeof createAuthorizedApiClient>;

function formatClock(totalSeconds: number | null) {
  if (totalSeconds === null) {
    return "--:--";
  }

  const safeSeconds = Math.max(0, totalSeconds);
  const minutes = Math.floor(safeSeconds / 60);
  const seconds = safeSeconds % 60;

  return `${minutes.toString().padStart(2, "0")}:${seconds.toString().padStart(2, "0")}`;
}

function resolveStreamStatus(kind: string) {
  switch (kind) {
    case "connected":
      return { label: "Sincronizado", tone: "success" as const };
    case "connecting":
    case "reconnecting":
      return { label: "Reconectando", tone: "warn" as const };
    case "disconnected":
    case "error":
      return { label: "Sin conexión", tone: "error" as const };
    default:
      return { label: "Buscando red", tone: "info" as const };
  }
}

function resolveSnapshotStatus(status: SnapshotStatus) {
  switch (status) {
    case "fresh":
      return { label: "Datos frescos", tone: "success" as const };
    case "loading":
    case "resyncing":
      return { label: "Actualizando snapshot", tone: "warn" as const };
    case "stale":
      return { label: "Datos desactualizados", tone: "warn" as const };
    case "error":
      return { label: "Snapshot no disponible", tone: "error" as const };
    default:
      return { label: "Esperando snapshot", tone: "info" as const };
  }
}

function readErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : "Session Team snapshot request failed.";
}

function metadataMatchesSnapshot(
  metadata: RealtimeEventMetadata,
  snapshot: SessionTeamSnapshot | null
) {
  return !snapshot || metadata.liveSessionId === snapshot.liveSessionId;
}

function isNewer(metadata: RealtimeEventMetadata, currentSequence: number) {
  return metadata.sequenceNumber > currentSequence;
}

function applyMetadataSync(
  snapshot: SessionTeamSnapshot,
  metadata: RealtimeEventMetadata
): SessionTeamSnapshot {
  return {
    ...snapshot,
    sync: {
      sequenceNumber: metadata.sequenceNumber,
      lastUpdatedUtc: metadata.occurredAtUtc,
      serverTimeUtc: metadata.occurredAtUtc
    }
  };
}

function formatDateTime(value?: string) {
  if (!value) {
    return "pending";
  }

  return new Date(value).toLocaleString();
}

function normalizeGameType(gameType?: string) {
  return gameType?.replace(/\s+/g, "").toLowerCase() ?? "";
}

export default function BoardPage() {
  const { session } = useSession();
  const config = useMemo(() => getClientConfig(), []);
  const apiClient = useMemo(
    () => (session ? createAuthorizedApiClient(session.accessToken) : null),
    [session]
  );
  const [cameraPermission, requestCameraPermission] = useCameraPermissions();
  const [loadingEnrollment, setLoadingEnrollment] = useState(true);
  const [storedEnrollment, setStoredEnrollment] = useState<StoredEnrollment | null>(null);
  const [snapshot, setSnapshot] = useState<SessionTeamSnapshot | null>(null);
  const [visibleHints, setVisibleHints] = useState<VisibleHintSnapshot[]>([]);
  const [snapshotStatus, setSnapshotStatus] = useState<SnapshotStatus>("idle");
  const [snapshotError, setSnapshotError] = useState<string | null>(null);
  const [remainingSeconds, setRemainingSeconds] = useState<number | null>(null);
  const [scannerVisible, setScannerVisible] = useState(false);
  const [scannerLocked, setScannerLocked] = useState(false);
  const [submissionStatus, setSubmissionStatus] = useState<QRSubmissionStatus>("idle");
  const [submissionMessage, setSubmissionMessage] = useState<string | null>(null);
  const [triviaAnswerText, setTriviaAnswerText] = useState("");
  const apiClientRef = useRef<ApiClient | null>(null);
  const enrollmentRef = useRef<StoredEnrollment | null>(null);
  const snapshotRef = useRef<SessionTeamSnapshot | null>(null);
  const sequenceRef = useRef(0);
  const scannerLockedRef = useRef(false);

  useEffect(() => {
    apiClientRef.current = apiClient;
  }, [apiClient]);

  useEffect(() => {
    enrollmentRef.current = storedEnrollment;
  }, [storedEnrollment]);

  useEffect(() => {
    snapshotRef.current = snapshot;
  }, [snapshot]);

  const refreshSnapshot = useCallback(async (mode: "initial" | "resync" = "resync") => {
    const client = apiClientRef.current;
    const enrollment = enrollmentRef.current;

    if (!client || !enrollment) {
      return;
    }

    setSnapshotStatus(mode === "initial" ? "loading" : "resyncing");
    setSnapshotError(null);

    try {
      const nextSnapshot = await client.getSessionTeamSnapshot(enrollment.teamId);

      if (nextSnapshot.sync.sequenceNumber >= sequenceRef.current) {
        sequenceRef.current = nextSnapshot.sync.sequenceNumber;
        setSnapshot(nextSnapshot);
        setVisibleHints(nextSnapshot.visibleHints);
      }

      setSnapshotStatus("fresh");
    } catch (error) {
      setSnapshotStatus(mode === "initial" ? "error" : "stale");
      setSnapshotError(readErrorMessage(error));
    }
  }, []);

  const connectionState = useSessionOperationsConnection({
    accessToken: session?.accessToken ?? "",
    hubUrl: config.sessionHubUrl,
    enabled: Boolean(session),
    onResync: () => {
      void refreshSnapshot("resync");
    }
  });

  const submitEvidenceHash = useCallback(async (qrHash: string) => {
    const client = apiClientRef.current;
    const enrollment = enrollmentRef.current;

    if (!client || !enrollment) {
      setSubmissionStatus("error");
      setSubmissionMessage("No hay sesión participante lista para enviar evidencia.");
      scannerLockedRef.current = false;
      setScannerLocked(false);
      return;
    }

    setSubmissionStatus("submitting");
    setSubmissionMessage("Enviando evidencia...");

    try {
      const result = await client.submitEvidence({
        sessionTeamId: enrollment.teamId,
        qrHash
      });

      if (result.validationOutcome.toLowerCase() === "accepted") {
        setSubmissionStatus("accepted");
        setSubmissionMessage("Evidencia Aceptada. El tablero avanzará con el evento realtime.");
        return;
      }

      setSubmissionStatus("rejected");
      setSubmissionMessage("Código incorrecto, inténtalo de nuevo.");
    } catch (error) {
      setSubmissionStatus("error");
      setSubmissionMessage(readErrorMessage(error));
    } finally {
      scannerLockedRef.current = false;
      setScannerLocked(false);
    }
  }, []);

  const submitTriviaAnswer = useCallback(async () => {
    const client = apiClientRef.current;
    const enrollment = enrollmentRef.current;
    const answerText = triviaAnswerText.trim();

    if (!client || !enrollment) {
      setSubmissionStatus("error");
      setSubmissionMessage("No hay sesion participante lista para enviar evidencia.");
      return;
    }

    if (!snapshotRef.current?.currentStage) {
      setSubmissionStatus("error");
      setSubmissionMessage("No hay etapa activa para enviar evidencia.");
      return;
    }

    if (!answerText) {
      setSubmissionStatus("error");
      setSubmissionMessage("Escribe una respuesta antes de enviar.");
      return;
    }

    setSubmissionStatus("submitting");
    setSubmissionMessage("Enviando respuesta...");

    try {
      const result = await client.submitTriviaAnswer({
        sessionTeamId: enrollment.teamId,
        answerText
      });

      if (result.validationOutcome.toLowerCase() === "accepted") {
        setSubmissionStatus("accepted");
        setTriviaAnswerText("");
        setSubmissionMessage("Respuesta correcta. El tablero avanzara con el evento realtime.");
        return;
      }

      setSubmissionStatus("rejected");
      setSubmissionMessage("Respuesta incorrecta, intenta de nuevo.");
    } catch (error) {
      setSubmissionStatus("error");
      setSubmissionMessage(readErrorMessage(error));
    }
  }, [triviaAnswerText]);

  const openQrScanner = useCallback(async () => {
    if (!snapshotRef.current?.currentStage) {
      setSubmissionStatus("error");
      setSubmissionMessage("No hay etapa activa para enviar evidencia.");
      return;
    }

    if (submissionStatus === "submitting") {
      return;
    }

    if (!cameraPermission?.granted) {
      setSubmissionStatus("requestingPermission");
      setSubmissionMessage("Solicitando permiso de cámara...");
      const nextPermission = await requestCameraPermission();

      if (!nextPermission.granted) {
        setSubmissionStatus("error");
        setSubmissionMessage("Permiso de cámara requerido para escanear el QR.");
        return;
      }
    }

    scannerLockedRef.current = false;
    setScannerLocked(false);
    setScannerVisible(true);
    setSubmissionStatus("scanning");
    setSubmissionMessage("Apunta la cámara al QR de la etapa actual.");
  }, [cameraPermission?.granted, requestCameraPermission, submissionStatus]);

  const closeQrScanner = useCallback(() => {
    setScannerVisible(false);
    scannerLockedRef.current = false;
    setScannerLocked(false);
    setSubmissionStatus((current) => (current === "scanning" ? "idle" : current));
  }, []);

  const handleBarcodeScanned = useCallback((result: BarcodeScanningResult) => {
    const qrHash = result.data?.trim();
    if (scannerLockedRef.current || !qrHash) {
      return;
    }

    scannerLockedRef.current = true;
    setScannerLocked(true);
    setScannerVisible(false);
    void submitEvidenceHash(qrHash);
  }, [submitEvidenceHash]);
  useEffect(() => {
    let active = true;

    async function hydrateEnrollment() {
      const enrollment = await loadStoredEnrollment();
      if (!active) {
        return;
      }

      setStoredEnrollment(enrollment);
      setLoadingEnrollment(false);
    }

    void hydrateEnrollment();

    return () => {
      active = false;
    };
  }, []);

  useEffect(() => {
    if (!storedEnrollment || !apiClient) {
      return;
    }

    void refreshSnapshot("initial");
  }, [apiClient, refreshSnapshot, storedEnrollment]);

  useEffect(() => {
    if (remainingSeconds === null || remainingSeconds <= 0) {
      return;
    }

    const intervalId = setInterval(() => {
      setRemainingSeconds((current) => {
        if (current === null) {
          return current;
        }

        return Math.max(0, current - 1);
      });
    }, 1000);

    return () => clearInterval(intervalId);
  }, [remainingSeconds]);

  useEffect(() => {
    const connection = connectionState.connection;
    if (!connection || connectionState.kind !== "connected") {
      return;
    }

    function shouldRefresh(metadata: RealtimeEventMetadata) {
      return metadata.refreshPolicy === SnapshotRefreshPolicies.refreshSnapshot;
    }

    function handleSessionStateChanged(payload: SessionStateChangedPayload) {
      const currentSnapshot = snapshotRef.current;
      if (!metadataMatchesSnapshot(payload.metadata, currentSnapshot)) {
        return;
      }

      if (shouldRefresh(payload.metadata)) {
        void refreshSnapshot("resync");
        return;
      }

      if (!isNewer(payload.metadata, sequenceRef.current)) {
        return;
      }

      sequenceRef.current = payload.metadata.sequenceNumber;

      if (typeof payload.remainingSeconds === "number") {
        setRemainingSeconds(payload.remainingSeconds);
      }

      setSnapshot((current) => {
        if (!current) {
          return current;
        }

        return {
          ...applyMetadataSync(current, payload.metadata),
          sessionState: payload.currentState
        };
      });
      setSnapshotStatus("fresh");
    }

    function handleTeamProgressChanged(payload: TeamProgressChangedPayload) {
      const enrollment = enrollmentRef.current;
      const currentSnapshot = snapshotRef.current;
      if (!enrollment || payload.sessionTeamId !== enrollment.teamId) {
        return;
      }

      if (!metadataMatchesSnapshot(payload.metadata, currentSnapshot)) {
        return;
      }

      if (shouldRefresh(payload.metadata)) {
        void refreshSnapshot("resync");
        return;
      }

      if (!isNewer(payload.metadata, sequenceRef.current)) {
        return;
      }

      sequenceRef.current = payload.metadata.sequenceNumber;
      setSnapshot((current) => {
        if (!current) {
          return current;
        }

        return {
          ...applyMetadataSync(current, payload.metadata),
          currentStage: payload.currentStage ?? current.currentStage,
          progressState: payload.progressState
        };
      });
      setSnapshotStatus("fresh");
    }

    function handleHintUnlocked(payload: HintUnlockedPayload) {
      const enrollment = enrollmentRef.current;
      const currentSnapshot = snapshotRef.current;
      if (!enrollment || payload.sessionTeamId !== enrollment.teamId) {
        return;
      }

      if (!metadataMatchesSnapshot(payload.metadata, currentSnapshot)) {
        return;
      }

      if (shouldRefresh(payload.metadata)) {
        void refreshSnapshot("resync");
        return;
      }

      if (!isNewer(payload.metadata, sequenceRef.current)) {
        return;
      }

      sequenceRef.current = payload.metadata.sequenceNumber;
      setVisibleHints((current) => {
        if (current.some((hint) => hint.hintId === payload.hint.hintId)) {
          return current;
        }

        return [...current, payload.hint];
      });
      setSnapshot((current) => {
        if (!current) {
          return current;
        }

        return applyMetadataSync(current, payload.metadata);
      });
      setSnapshotStatus("fresh");
    }

    connection.on("ReceiveSessionStateChanged", handleSessionStateChanged);
    connection.on("ReceiveTeamProgressChanged", handleTeamProgressChanged);
    connection.on("ReceiveHintUnlocked", handleHintUnlocked);

    return () => {
      connection.off("ReceiveSessionStateChanged", handleSessionStateChanged);
      connection.off("ReceiveTeamProgressChanged", handleTeamProgressChanged);
      connection.off("ReceiveHintUnlocked", handleHintUnlocked);
    };
  }, [connectionState.connection, connectionState.kind, refreshSnapshot]);

  if (!session) {
    return <LoadingScreen message="Checking participant session..." />;
  }

  if (loadingEnrollment) {
    return <LoadingScreen message="Checking Session Team enrollment..." />;
  }

  if (!storedEnrollment) {
    return <Redirect href="/join" />;
  }

  if (snapshotStatus === "loading" && !snapshot) {
    return <LoadingScreen message="Loading Session Team snapshot..." />;
  }

  const streamStatus = resolveStreamStatus(connectionState.kind);
  const dataStatus = resolveSnapshotStatus(snapshotStatus);
  const currentStage = snapshot?.currentStage;
  const currentGameType = normalizeGameType(currentStage?.gameType);
  const currentStageIsTreasureHunt = currentGameType === GameTypes.treasureHunt;
  const currentStageIsTrivia = currentGameType === GameTypes.trivia;
  const currentStageHints = currentStage
    ? visibleHints.filter((hint) => hint.missionStageId === currentStage.missionStageId)
    : [];
  const evidenceIsBusy = submissionStatus === "requestingPermission" || submissionStatus === "submitting";
  const canScanEvidence = Boolean(currentStage) && currentStageIsTreasureHunt && !evidenceIsBusy;
  const canSubmitTrivia = Boolean(currentStage)
    && currentStageIsTrivia
    && triviaAnswerText.trim().length > 0
    && !evidenceIsBusy;

  return (
    <ScreenShell
      eyebrow="Participant Stage View"
      title={snapshot ? `${snapshot.teamName} board` : "Session Team board"}
      description="Live board consumes Session Operations snapshot and SignalR events without redefining backend contracts."
    >
      <View style={shellStyles.card}>
        <Text style={shellStyles.cardTitle}>Realtime sync</Text>
        <View style={shellStyles.row}>
          <StatusChip label={streamStatus.label} tone={streamStatus.tone} />
          <StatusChip label={dataStatus.label} tone={dataStatus.tone} />
        </View>
        <Text style={shellStyles.cardText}>{connectionState.detail}</Text>
        {snapshotError ? <Text style={styles.error}>{snapshotError}</Text> : null}
        <Pressable
          onPress={() => {
            void refreshSnapshot("resync");
          }}
          style={({ pressed }) => [styles.secondaryButton, pressed && styles.buttonPressed]}
        >
          <Text style={styles.secondaryButtonLabel}>Refresh snapshot</Text>
        </Pressable>
      </View>

      <View style={shellStyles.card}>
        <Text style={shellStyles.cardTitle}>Session Team</Text>
        <StatusChip label={snapshot?.sessionState ?? "Snapshot pending"} tone="info" />
        <Text style={shellStyles.cardText}>Join Code: {storedEnrollment.joinCode}</Text>
        <Text style={shellStyles.cardText}>Team: {snapshot?.teamName ?? storedEnrollment.teamName}</Text>
        <Text style={shellStyles.cardText}>Progress: {snapshot?.progressState ?? "pending"}</Text>
        <Text style={shellStyles.mono}>TeamId: {storedEnrollment.teamId}</Text>
      </View>

      <View style={shellStyles.card}>
        <View style={shellStyles.row}>
          <Text style={shellStyles.cardTitle}>Session clock</Text>
          <StatusChip label={remainingSeconds === null ? "Waiting event" : "Running"} tone="warn" />
        </View>
        <Text style={styles.clock}>{formatClock(remainingSeconds)}</Text>
        <Text style={shellStyles.cardText}>
          Clock updates from `ReceiveSessionStateChanged.remainingSeconds` when backend emits it.
        </Text>
      </View>

      <View style={shellStyles.card}>
        <Text style={shellStyles.cardTitle}>Current playable stage</Text>
        {currentStage ? (
          <>
            <StatusChip label={currentStage.difficulty} tone="success" />
            <Text style={styles.stageTitle}>{currentStage.name}</Text>
            <Text style={shellStyles.cardText}>Game type: {currentStage.gameType}</Text>
            <Text style={shellStyles.cardText}>
              Stage order: {currentStage.sessionStageOrder} · Source order: {currentStage.sourceOrder}
            </Text>
            <Text style={shellStyles.cardText}>
              Time budget: {currentStage.resolvedTimeBudgetMinutes} min
            </Text>
            <Text style={shellStyles.mono}>MissionStageId: {currentStage.missionStageId}</Text>
          </>
        ) : (
          <>
            <StatusChip label="No active stage" tone="warn" />
            <Text style={shellStyles.cardText}>
              Snapshot has no current stage yet. Board stays read-only until Session Progression moves this Session Team.
            </Text>
          </>
        )}
      </View>

      <View style={shellStyles.card}>
        <Text style={shellStyles.cardTitle}>Evidence submission</Text>
        {currentStageIsTreasureHunt ? (
          <>
        <Text style={shellStyles.cardText}>
          Escanea el QR físico de Treasure Hunt para enviar el hash de evidencia al backend de Session Operations.
        </Text>
        <Pressable
          disabled={!canScanEvidence}
          onPress={() => {
            void openQrScanner();
          }}
          style={({ pressed }) => [
            styles.primaryButton,
            !canScanEvidence && styles.disabledButton,
            pressed && canScanEvidence && styles.buttonPressed
          ]}
        >
          <Text style={styles.primaryButtonLabel}>Escanear QR</Text>
        </Pressable>
          </>
        ) : null}
        {currentStageIsTrivia ? (
          <>
            <Text style={shellStyles.cardText}>
              Envia tu respuesta de Trivia para validacion automatica de Session Operations.
            </Text>
            <TextInput
              editable={!evidenceIsBusy}
              onChangeText={setTriviaAnswerText}
              onSubmitEditing={() => {
                if (canSubmitTrivia) {
                  void submitTriviaAnswer();
                }
              }}
              placeholder="Escribe tu respuesta..."
              placeholderTextColor="#68777d"
              returnKeyType="send"
              style={styles.triviaInput}
              value={triviaAnswerText}
            />
            <Pressable
              disabled={!canSubmitTrivia}
              onPress={() => {
                void submitTriviaAnswer();
              }}
              style={({ pressed }) => [
                styles.primaryButton,
                !canSubmitTrivia && styles.disabledButton,
                pressed && canSubmitTrivia && styles.buttonPressed
              ]}
            >
              <Text style={styles.primaryButtonLabel}>Enviar</Text>
            </Pressable>
          </>
        ) : null}
        {submissionStatus === "submitting" ? (
          <View style={styles.inlineStatus}>
            <ActivityIndicator color="#17313b" />
            <Text style={styles.inlineStatusText}>{submissionMessage ?? "Enviando evidencia..."}</Text>
          </View>
        ) : null}
        {submissionStatus === "accepted" ? (
          <View style={styles.feedbackSuccess}>
            <Text style={styles.feedbackText}>✓ {submissionMessage ?? "Evidencia Aceptada"}</Text>
          </View>
        ) : null}
        {submissionStatus === "rejected" || submissionStatus === "error" ? (
          <View style={styles.feedbackError}>
            <Text style={styles.feedbackText}>⚠ {submissionMessage ?? "Código incorrecto, inténtalo de nuevo"}</Text>
          </View>
        ) : null}
        {submissionStatus === "scanning" || submissionStatus === "requestingPermission" ? (
          <Text style={shellStyles.cardText}>{submissionMessage}</Text>
        ) : null}
        {!currentStage ? (
          <Text style={styles.warning}>Sin etapa activa: el envío de evidencia permanece bloqueado.</Text>
        ) : null}
      </View>

      <Modal
        animationType="slide"
        onRequestClose={closeQrScanner}
        presentationStyle="fullScreen"
        visible={scannerVisible}
      >
        <View style={styles.scannerModal}>
          <CameraView
            barcodeScannerSettings={{ barcodeTypes: ["qr"] }}
            facing="back"
            onBarcodeScanned={scannerLocked ? undefined : handleBarcodeScanned}
            style={styles.cameraPreview}
          />
          <View style={styles.scannerOverlay}>
            <View style={styles.scannerFrame} />
            <View style={styles.scannerInstructions}>
              <Text style={styles.scannerTitle}>Escanea el QR</Text>
              <Text style={styles.scannerText}>Mantén el código dentro del marco. Se enviará automáticamente.</Text>
              <Pressable
                onPress={closeQrScanner}
                style={({ pressed }) => [styles.scannerCancelButton, pressed && styles.buttonPressed]}
              >
                <Text style={styles.scannerCancelLabel}>Cancelar</Text>
              </Pressable>
            </View>
          </View>
        </View>
      </Modal>
      <View style={shellStyles.card}>
        <Text style={shellStyles.cardTitle}>Visible hints</Text>
        {currentStageHints.length > 0 ? (
          <View style={shellStyles.list}>
            {currentStageHints.map((hint) => (
              <View key={hint.hintId} style={styles.hintItem}>
                <View style={shellStyles.row}>
                  <StatusChip label={hint.isSolution ? "Solution" : "Hint"} tone={hint.isSolution ? "warn" : "info"} />
                  <StatusChip label={hint.unlockReason} tone="neutral" />
                </View>
                <Text style={shellStyles.cardText}>{hint.content}</Text>
                <Text style={shellStyles.mono}>Unlocked: {formatDateTime(hint.unlockedAtUtc)}</Text>
              </View>
            ))}
          </View>
        ) : (
          <Text style={shellStyles.cardText}>
            No visible hints for current stage. New `ReceiveHintUnlocked` events append here.
          </Text>
        )}
      </View>

      <View style={shellStyles.card}>
        <Text style={shellStyles.cardTitle}>Sync metadata</Text>
        <Text style={shellStyles.cardText}>
          Sequence: {snapshot?.sync.sequenceNumber ?? sequenceRef.current}
        </Text>
        <Text style={shellStyles.cardText}>
          Last updated: {formatDateTime(snapshot?.sync.lastUpdatedUtc)}
        </Text>
        <Text style={shellStyles.cardText}>
          Server time: {formatDateTime(snapshot?.sync.serverTimeUtc)}
        </Text>
      </View>
    </ScreenShell>
  );
}

const styles = StyleSheet.create({
  primaryButton: {
    backgroundColor: "#2d6a4f",
    borderRadius: 18,
    alignItems: "center",
    paddingHorizontal: 16,
    paddingVertical: 14
  },
  primaryButtonLabel: {
    color: "#f7fbfc",
    fontSize: 15,
    fontWeight: "800"
  },
  disabledButton: {
    backgroundColor: "#9aa6a1"
  },
  inlineStatus: {
    alignItems: "center",
    flexDirection: "row",
    gap: 10
  },
  inlineStatusText: {
    color: "#17313b",
    fontSize: 14,
    fontWeight: "700"
  },
  feedbackSuccess: {
    backgroundColor: "#d8f3dc",
    borderColor: "#74c69d",
    borderRadius: 16,
    borderWidth: 1,
    padding: 12
  },
  feedbackError: {
    backgroundColor: "#ffe5e5",
    borderColor: "#ef9a9a",
    borderRadius: 16,
    borderWidth: 1,
    padding: 12
  },
  feedbackText: {
    color: "#17313b",
    fontSize: 14,
    fontWeight: "700",
    lineHeight: 20
  },
  warning: {
    color: "#9e6f00",
    fontSize: 14,
    lineHeight: 20
  },
  triviaInput: {
    backgroundColor: "#f7fbfc",
    borderColor: "#b8c8cc",
    borderRadius: 16,
    borderWidth: 1,
    color: "#17313b",
    fontSize: 16,
    minHeight: 48,
    paddingHorizontal: 14,
    paddingVertical: 12
  },
  scannerModal: {
    backgroundColor: "#000",
    flex: 1
  },
  cameraPreview: {
    flex: 1
  },
  scannerOverlay: {
    ...StyleSheet.absoluteFillObject,
    justifyContent: "space-between",
    padding: 24
  },
  scannerFrame: {
    alignSelf: "center",
    borderColor: "#f7fbfc",
    borderRadius: 24,
    borderWidth: 3,
    height: 260,
    marginTop: 110,
    width: 260
  },
  scannerInstructions: {
    backgroundColor: "rgba(23, 49, 59, 0.9)",
    borderRadius: 24,
    gap: 10,
    padding: 18
  },
  scannerTitle: {
    color: "#f7fbfc",
    fontSize: 22,
    fontWeight: "800"
  },
  scannerText: {
    color: "#dce8ea",
    fontSize: 14,
    lineHeight: 20
  },
  scannerCancelButton: {
    alignItems: "center",
    backgroundColor: "#f7fbfc",
    borderRadius: 16,
    paddingVertical: 12
  },
  scannerCancelLabel: {
    color: "#17313b",
    fontSize: 15,
    fontWeight: "800"
  },
  secondaryButton: {
    backgroundColor: "#17313b",
    borderRadius: 18,
    alignItems: "center",
    paddingHorizontal: 16,
    paddingVertical: 14
  },
  secondaryButtonLabel: {
    color: "#f7fbfc",
    fontSize: 15,
    fontWeight: "700"
  },
  buttonPressed: {
    opacity: 0.85
  },
  error: {
    color: "#9e2f2f",
    fontSize: 14,
    lineHeight: 20
  },
  clock: {
    color: "#17313b",
    fontSize: 48,
    fontWeight: "800",
    letterSpacing: 1.5
  },
  stageTitle: {
    color: "#17313b",
    fontSize: 24,
    fontWeight: "800",
    lineHeight: 30
  },
  hintItem: {
    backgroundColor: "#f6efe6",
    borderColor: "#eadcc8",
    borderRadius: 18,
    borderWidth: 1,
    gap: 10,
    padding: 14
  }
});
