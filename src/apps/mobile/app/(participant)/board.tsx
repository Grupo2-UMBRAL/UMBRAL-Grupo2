import { CameraView } from "expo-camera";
import { Redirect } from "expo-router";
import { useCallback, useEffect, useMemo, useState } from "react";
import {
  ActivityIndicator,
  Modal,
  Pressable,
  StyleSheet,
  Text,
  TextInput,
  View
} from "react-native";
import { LoadingScreen } from "../../src/components/loading-screen";
import { ScreenShell, shellStyles } from "../../src/components/screen-shell";
import { StaticHintMap } from "../../src/components/static-hint-map";
import { StatusChip } from "../../src/components/status-chip";
import {
  ApiClientError,
  SnapshotRefreshPolicies,
  createAuthorizedApiClient,
  type HintUnlockedPayload,
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
import { useSessionOperationsConnection } from "../../src/hooks/use-session-operations-connection";
import { useSession } from "../../src/providers/session-provider";

type SubmissionFeedback =
  | { tone: "success"; title: string; detail: string }
  | { tone: "error"; title: string; detail: string }
  | null;

function resolveSessionTone(state: string | null) {
  switch (state) {
    case "Running":
    case "Active":
      return "success";
    case "Paused":
      return "warn";
    case "Canceled":
    case "Finalized":
      return "error";
    default:
      return "info";
  }
}

function resolveConnectionTone(kind: string) {
  switch (kind) {
    case "connected":
      return "success";
    case "reconnecting":
      return "warn";
    case "error":
      return "error";
    default:
      return "info";
  }
}

function readErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : "Participant board request failed.";
}

function formatCountdown(remainingSeconds: number | null) {
  if (remainingSeconds === null) {
    return null;
  }

  const safeSeconds = Math.max(0, remainingSeconds);
  const minutes = Math.floor(safeSeconds / 60);
  const seconds = safeSeconds % 60;

  return `${minutes.toString().padStart(2, "0")}:${seconds.toString().padStart(2, "0")}`;
}

function mergeVisibleHints(
  currentHints: VisibleHintSnapshot[],
  nextHint: VisibleHintSnapshot
) {
  const alreadyPresent = currentHints.some((hint) => hint.hintId === nextHint.hintId);
  if (alreadyPresent) {
    return currentHints;
  }

  return [...currentHints, nextHint].sort((left, right) =>
    left.unlockedAtUtc.localeCompare(right.unlockedAtUtc)
  );
}

function isStaticMapReady(hint: VisibleHintSnapshot) {
  return typeof hint.latitude === "number" && typeof hint.longitude === "number";
}

function groupHintsByStage(hints: VisibleHintSnapshot[]) {
  return hints.reduce<Record<string, VisibleHintSnapshot[]>>((groups, hint) => {
    const stageHints = groups[hint.missionStageId] ?? [];

    return {
      ...groups,
      [hint.missionStageId]: [...stageHints, hint]
    };
  }, {});
}

export default function BoardPage() {
  const { session } = useSession();
  const config = useMemo(() => getClientConfig(), []);
  const apiClient = useMemo(
    () => (session ? createAuthorizedApiClient(session.accessToken) : null),
    [session]
  );
  const [loadingEnrollment, setLoadingEnrollment] = useState(true);
  const [storedEnrollment, setStoredEnrollment] = useState<StoredEnrollment | null>(null);
  const [snapshot, setSnapshot] = useState<SessionTeamSnapshot | null>(null);
  const [snapshotError, setSnapshotError] = useState<string | null>(null);
  const [remainingSeconds, setRemainingSeconds] = useState<number | null>(null);
  const [scannerVisible, setScannerVisible] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [triviaAnswer, setTriviaAnswer] = useState("");
  const [feedback, setFeedback] = useState<SubmissionFeedback>(null);

  const refreshSnapshot = useCallback(async () => {
    if (!apiClient || !storedEnrollment) {
      return;
    }

    setSnapshotError(null);

    try {
      const nextSnapshot = await apiClient.getSessionTeamSnapshot(storedEnrollment.teamId);
      setSnapshot(nextSnapshot);
    } catch (error) {
      setSnapshotError(readErrorMessage(error));
    }
  }, [apiClient, storedEnrollment]);

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
    if (!storedEnrollment) {
      return;
    }

    void refreshSnapshot();
  }, [refreshSnapshot, storedEnrollment]);

  const connectionState = useSessionOperationsConnection({
    accessToken: session?.accessToken ?? "",
    hubUrl: config.sessionHubUrl,
    onResync: () => {
      void refreshSnapshot();
    }
  });

  useEffect(() => {
    const connection = connectionState.connection;
    if (!connection) {
      return;
    }

    const handleHintUnlocked = (payload: HintUnlockedPayload) => {
      setSnapshot((currentSnapshot) => {
        if (
          !currentSnapshot
          || payload.sessionTeamId !== currentSnapshot.sessionTeamId
          || payload.metadata.liveSessionId !== currentSnapshot.liveSessionId
        ) {
          return currentSnapshot;
        }

        if (payload.metadata.refreshPolicy === SnapshotRefreshPolicies.refreshSnapshot) {
          void refreshSnapshot();
          return currentSnapshot;
        }

        return {
          ...currentSnapshot,
          visibleHints: mergeVisibleHints(currentSnapshot.visibleHints, payload.hint),
          sync: {
            ...currentSnapshot.sync,
            sequenceNumber: Math.max(
              currentSnapshot.sync.sequenceNumber,
              payload.metadata.sequenceNumber
            ),
            lastUpdatedUtc: payload.metadata.occurredAtUtc
          }
        };
      });
    };

    const handleSessionStateChanged = (payload: SessionStateChangedPayload) => {
      setRemainingSeconds(payload.remainingSeconds ?? null);
      setSnapshot((currentSnapshot) => {
        if (
          !currentSnapshot
          || payload.metadata.liveSessionId !== currentSnapshot.liveSessionId
        ) {
          return currentSnapshot;
        }

        if (payload.metadata.refreshPolicy === SnapshotRefreshPolicies.refreshSnapshot) {
          void refreshSnapshot();
          return currentSnapshot;
        }

        return {
          ...currentSnapshot,
          sessionState: payload.currentState,
          sync: {
            ...currentSnapshot.sync,
            sequenceNumber: Math.max(
              currentSnapshot.sync.sequenceNumber,
              payload.metadata.sequenceNumber
            ),
            lastUpdatedUtc: payload.metadata.occurredAtUtc
          }
        };
      });
    };

    const handleTeamProgressChanged = (payload: TeamProgressChangedPayload) => {
      setSnapshot((currentSnapshot) => {
        if (
          !currentSnapshot
          || payload.sessionTeamId !== currentSnapshot.sessionTeamId
          || payload.metadata.liveSessionId !== currentSnapshot.liveSessionId
        ) {
          return currentSnapshot;
        }

        if (payload.metadata.refreshPolicy === SnapshotRefreshPolicies.refreshSnapshot) {
          void refreshSnapshot();
          return currentSnapshot;
        }

        return {
          ...currentSnapshot,
          currentStage: payload.currentStage,
          progressState: payload.progressState,
          sync: {
            ...currentSnapshot.sync,
            sequenceNumber: Math.max(
              currentSnapshot.sync.sequenceNumber,
              payload.metadata.sequenceNumber
            ),
            lastUpdatedUtc: payload.metadata.occurredAtUtc
          }
        };
      });
    };

    connection.on("ReceiveHintUnlocked", handleHintUnlocked);
    connection.on("ReceiveSessionStateChanged", handleSessionStateChanged);
    connection.on("ReceiveTeamProgressChanged", handleTeamProgressChanged);

    return () => {
      connection.off("ReceiveHintUnlocked", handleHintUnlocked);
      connection.off("ReceiveSessionStateChanged", handleSessionStateChanged);
      connection.off("ReceiveTeamProgressChanged", handleTeamProgressChanged);
    };
  }, [connectionState.connection, refreshSnapshot]);

  const currentStage = snapshot?.currentStage;
  const completedStages = currentStage ? Math.max(0, currentStage.sessionStageOrder - 1) : 0;
  const currentSessionState = snapshot?.sessionState ?? null;
  const isFinalized = currentSessionState === "Finalized";
  const actionBlocked = currentSessionState !== "Running" && currentSessionState !== "Active";
  const countdown = formatCountdown(remainingSeconds);
  const visibleGameplayHints = useMemo(
    () => snapshot?.visibleHints.filter((hint) => !hint.isSolution) ?? [],
    [snapshot?.visibleHints]
  );
  const resolutionHintsByStage = useMemo(
    () => groupHintsByStage(snapshot?.visibleHints ?? []),
    [snapshot?.visibleHints]
  );

  async function submitQrEvidence(qrHash: string) {
    if (!apiClient || !storedEnrollment) {
      return;
    }

    const normalizedHash = qrHash.trim();
    if (!normalizedHash) {
      return;
    }

    setSubmitting(true);
    setFeedback(null);

    try {
      const result = await apiClient.submitEvidence({
        sessionTeamId: storedEnrollment.teamId,
        qrHash: normalizedHash
      });

      setFeedback(
        result.validationOutcome === "Accepted"
          ? {
              tone: "success",
              title: "Evidencia Aceptada",
              detail: "Código correcto. Tu Session Team avanzó a la siguiente etapa."
            }
          : {
              tone: "error",
              title: "Evidencia Rechazada",
              detail: "Código incorrecto, inténtalo de nuevo."
            }
      );
      setScannerVisible(false);
      await refreshSnapshot();
    } catch (error) {
      const detail =
        error instanceof ApiClientError
          ? error.message
          : "No pudimos enviar la evidencia QR.";
      setFeedback({
        tone: "error",
        title: "Evidencia Rechazada",
        detail
      });
      setScannerVisible(false);
    } finally {
      setSubmitting(false);
    }
  }

  async function submitTriviaEvidence() {
    if (!apiClient || !storedEnrollment) {
      return;
    }

    const normalizedAnswer = triviaAnswer.trim();
    if (!normalizedAnswer) {
      return;
    }

    setSubmitting(true);
    setFeedback(null);

    try {
      const result = await apiClient.submitTriviaAnswer({
        sessionTeamId: storedEnrollment.teamId,
        answerText: normalizedAnswer
      });

      setFeedback(
        result.validationOutcome === "Accepted"
          ? {
              tone: "success",
              title: "Respuesta correcta",
              detail: "Validation Outcome aceptado. Sigue con la siguiente hoja."
            }
          : {
              tone: "error",
              title: "Respuesta incorrecta",
              detail: "Respuesta incorrecta, intenta de nuevo."
            }
      );
      if (result.validationOutcome === "Accepted") {
        setTriviaAnswer("");
      }
      await refreshSnapshot();
    } catch (error) {
      const detail =
        error instanceof ApiClientError
          ? error.message
          : "No pudimos enviar la respuesta Trivia.";
      setFeedback({
        tone: "error",
        title: "Respuesta incorrecta",
        detail
      });
    } finally {
      setSubmitting(false);
    }
  }

  if (!session) {
    return <LoadingScreen message="Checking participant session..." />;
  }

  if (loadingEnrollment) {
    return <LoadingScreen message="Checking Session Team enrollment..." />;
  }

  if (!storedEnrollment) {
    return <Redirect href="/join" />;
  }

  if (!snapshot) {
    return <LoadingScreen message="Loading Session Team snapshot..." />;
  }

  return (
    <ScreenShell
      eyebrow="Participant Stage View"
      title={`${snapshot.teamName} board`}
      description="Current stage, progress, Hint Release and Evidence Submission stay aligned with Session Operations and realtime updates."
    >
      <View style={shellStyles.card}>
        <View style={shellStyles.row}>
          <StatusChip
            label={currentSessionState ?? "Awaiting lifecycle event"}
            tone={resolveSessionTone(currentSessionState)}
          />
          <StatusChip label={connectionState.kind === "reconnecting" ? "Reconectando" : connectionState.kind} tone={resolveConnectionTone(connectionState.kind)} />
          <StatusChip label={snapshot.progressState} tone="info" />
        </View>
        <Text style={shellStyles.cardText}>{connectionState.detail}</Text>
        {snapshotError ? <Text style={styles.error}>{snapshotError}</Text> : null}
      </View>

      <View style={shellStyles.card}>
        <Text style={shellStyles.cardTitle}>Session progression</Text>
        <View style={shellStyles.row}>
          <StatusChip label={`${completedStages} completed`} tone="success" />
          <StatusChip
            label={currentStage ? `Current ${currentStage.sessionStageOrder}` : "No active stage"}
            tone="info"
          />
          {countdown ? <StatusChip label={countdown} tone="warn" /> : null}
        </View>
        {currentStage ? (
          <>
            <Text style={styles.stageTitle}>{currentStage.name}</Text>
            <Text style={shellStyles.cardText}>
              Stage order {currentStage.sessionStageOrder} | Difficulty {currentStage.difficulty} | Game type {currentStage.gameType}
            </Text>
          </>
        ) : (
          <Text style={shellStyles.cardText}>No current playable stage is visible for this Session Team yet.</Text>
        )}
      </View>

      {feedback ? (
        <View style={feedback.tone === "success" ? styles.feedbackSuccess : styles.feedbackError}>
          <Text style={styles.feedbackText}>{feedback.title}</Text>
          <Text style={shellStyles.cardText}>{feedback.detail}</Text>
        </View>
      ) : null}

      <View style={shellStyles.card}>
        <Text style={shellStyles.cardTitle}>Evidence Submission</Text>
        <Text style={shellStyles.cardText}>
          Session Operations accepts evidence only while Session State stays Running.
        </Text>
        {actionBlocked ? (
          <Text style={styles.warning}>Evidence CTA disabled by lifecycle guard.</Text>
        ) : null}
        {isFinalized ? (
          <View style={styles.finalizedEvidenceNotice}>
            <StatusChip label="Finalized" tone="error" />
            <Text style={shellStyles.cardText}>Evidence Submission closed.</Text>
          </View>
        ) : currentStage?.gameType === "TreasureHunt" ? (
          <Pressable
            disabled={actionBlocked || submitting}
            onPress={() => {
              setScannerVisible(true);
            }}
            style={({ pressed }) => [
              styles.primaryButton,
              (actionBlocked || submitting) && styles.disabledButton,
              pressed && styles.buttonPressed
            ]}
          >
            <Text style={styles.primaryButtonLabel}>
              {submitting ? "Enviando QR..." : "Escanear QR"}
            </Text>
          </Pressable>
        ) : (
          <>
            <TextInput
              editable={!actionBlocked && !submitting}
              onChangeText={setTriviaAnswer}
              placeholder="Escribe tu respuesta..."
              placeholderTextColor="#70868d"
              style={styles.triviaInput}
              value={triviaAnswer}
            />
            <Pressable
              disabled={actionBlocked || submitting || triviaAnswer.trim().length === 0}
              onPress={() => {
                void submitTriviaEvidence();
              }}
              style={({ pressed }) => [
                styles.primaryButton,
                (actionBlocked || submitting || triviaAnswer.trim().length === 0)
                  && styles.disabledButton,
                pressed && styles.buttonPressed
              ]}
            >
              <Text style={styles.primaryButtonLabel}>
                {submitting ? "Enviando..." : "Enviar"}
              </Text>
            </Pressable>
          </>
        )}
      </View>

      {isFinalized ? (
        <View style={styles.resolutionsSection}>
          <Text style={styles.resolutionsTitle}>Resoluciones de la Misión</Text>
          {(snapshot.allStages ?? []).length ? (
            snapshot.allStages?.map((stage) => {
              const stageHints = resolutionHintsByStage[stage.missionStageId] ?? [];

              return (
                <View key={stage.missionStageId} style={styles.resolutionStageCard}>
                  <View style={styles.resolutionStageHeader}>
                    <Text style={styles.resolutionStageTitle}>{stage.name}</Text>
                    <StatusChip label={`Stage ${stage.sessionStageOrder}`} tone="neutral" />
                  </View>
                  <View style={shellStyles.row}>
                    <StatusChip label={stage.difficulty} tone="info" />
                    <StatusChip label={stage.gameType} tone="success" />
                  </View>
                  <Text style={shellStyles.cardText}>{stage.prompt}</Text>
                  {stageHints.length ? (
                    stageHints.map((hint) => (
                      <View
                        key={hint.hintId}
                        style={hint.isSolution ? styles.solutionItem : styles.resolutionHintItem}
                      >
                        <View style={shellStyles.row}>
                          <StatusChip
                            label={hint.isSolution ? "Solution" : "Hint"}
                            tone={hint.isSolution ? "warn" : "info"}
                          />
                          <StatusChip label={hint.unlockReason} tone="neutral" />
                        </View>
                        <Text style={styles.resolutionHintText}>{hint.content}</Text>
                        {isStaticMapReady(hint) ? (
                          <StaticHintMap latitude={hint.latitude!} longitude={hint.longitude!} />
                        ) : null}
                      </View>
                    ))
                  ) : (
                    <Text style={shellStyles.cardText}>No Hints registered for this stage.</Text>
                  )}
                </View>
              );
            })
          ) : (
            <View style={shellStyles.card}>
              <StatusChip label="Awaiting resolutions" tone="warn" />
              <Text style={shellStyles.cardText}>Session snapshot has no final stage list yet.</Text>
            </View>
          )}
        </View>
      ) : (
        <View style={shellStyles.section}>
          <Text style={shellStyles.cardTitle}>Visible hints</Text>
          {visibleGameplayHints.length ? (
            visibleGameplayHints.map((hint) => (
              <View key={hint.hintId} style={styles.hintItem}>
                <View style={shellStyles.row}>
                  <StatusChip label="Hint" tone="info" />
                  <StatusChip label={hint.unlockReason} tone="neutral" />
                </View>
                <Text style={shellStyles.cardText}>{hint.content}</Text>
                {isStaticMapReady(hint) ? (
                  <StaticHintMap latitude={hint.latitude!} longitude={hint.longitude!} />
                ) : (
                  <Text style={shellStyles.cardText}>
                    This Hint has no coordinates, so the mobile client keeps the map hidden instead of rendering a broken state.
                  </Text>
                )}
              </View>
            ))
          ) : (
            <View style={shellStyles.card}>
              <StatusChip label="No hints yet" tone="warn" />
              <Text style={shellStyles.cardText}>
                Hint Release will surface here after Session Operations unlocks content for this Session Team.
              </Text>
            </View>
          )}
        </View>
      )}

      <Modal animationType="slide" transparent={false} visible={scannerVisible}>
        <View style={styles.scannerModal}>
          <CameraView
            onBarcodeScanned={({ data }) => {
              void submitQrEvidence(data);
            }}
            style={styles.cameraPreview}
          />
          <View style={styles.scannerOverlay}>
            <View style={styles.scannerFrame} />
            <View style={styles.scannerInstructions}>
              <Text style={styles.scannerTitle}>Scan stage QR</Text>
              <Text style={styles.scannerText}>
                Keep the code inside the frame. Session Operations will validate the hash for the current stage only.
              </Text>
              {submitting ? (
                <View style={styles.inlineStatus}>
                  <ActivityIndicator color="#f7fbfc" />
                  <Text style={styles.inlineStatusText}>Sending scanned evidence...</Text>
                </View>
              ) : null}
              <Pressable
                onPress={() => {
                  setScannerVisible(false);
                }}
                style={({ pressed }) => [
                  styles.scannerCancelButton,
                  pressed && styles.buttonPressed
                ]}
              >
                <Text style={styles.scannerCancelLabel}>Cancelar</Text>
              </Pressable>
            </View>
          </View>
        </View>
      </Modal>
    </ScreenShell>
  );
}

const styles = StyleSheet.create({
  primaryButton: {
    alignItems: "center",
    backgroundColor: "#2d6a4f",
    borderRadius: 18,
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
    color: "#f7fbfc",
    fontSize: 14,
    fontWeight: "700"
  },
  feedbackSuccess: {
    backgroundColor: "#d8f3dc",
    borderColor: "#74c69d",
    borderRadius: 16,
    borderWidth: 1,
    gap: 8,
    padding: 12
  },
  feedbackError: {
    backgroundColor: "#ffe5e5",
    borderColor: "#ef9a9a",
    borderRadius: 16,
    borderWidth: 1,
    gap: 8,
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
  finalizedEvidenceNotice: {
    backgroundColor: "#f2e7de",
    borderColor: "#d1ab89",
    borderRadius: 16,
    borderWidth: 1,
    gap: 10,
    padding: 12
  },
  resolutionsSection: {
    backgroundColor: "#eef5f1",
    borderColor: "#bed8c9",
    borderRadius: 18,
    borderWidth: 1,
    gap: 14,
    padding: 14
  },
  resolutionsTitle: {
    color: "#17313b",
    fontSize: 22,
    fontWeight: "900",
    lineHeight: 28
  },
  resolutionStageCard: {
    backgroundColor: "#f7fbfc",
    borderColor: "#c8d7dc",
    borderRadius: 18,
    borderWidth: 1,
    gap: 12,
    padding: 14
  },
  resolutionStageHeader: {
    alignItems: "flex-start",
    gap: 10
  },
  resolutionStageTitle: {
    color: "#17313b",
    fontSize: 19,
    fontWeight: "900",
    lineHeight: 25
  },
  resolutionHintItem: {
    backgroundColor: "#fffaf5",
    borderColor: "#eadcc8",
    borderRadius: 16,
    borderWidth: 1,
    gap: 10,
    padding: 12
  },
  solutionItem: {
    backgroundColor: "#fff1df",
    borderColor: "#d28b39",
    borderRadius: 16,
    borderWidth: 2,
    gap: 10,
    padding: 12
  },
  resolutionHintText: {
    color: "#17313b",
    fontSize: 15,
    fontWeight: "700",
    lineHeight: 21
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
  buttonPressed: {
    opacity: 0.85
  },
  error: {
    color: "#9e2f2f",
    fontSize: 14,
    lineHeight: 20
  },
  stageTitle: {
    color: "#17313b",
    fontSize: 24,
    fontWeight: "800",
    lineHeight: 30
  },
  hintItem: {
    backgroundColor: "#fffaf5",
    borderColor: "#eadcc8",
    borderRadius: 18,
    borderWidth: 1,
    gap: 10,
    padding: 14
  }
});
