import { CameraView } from "expo-camera";
import { Redirect, useRouter } from "expo-router";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  ActivityIndicator,
  Modal,
  Pressable,
  StyleSheet,
  Text,
  View
} from "react-native";
import { AnswerFeedbackSheet, type AnswerFeedback } from "../../../src/components/answer-feedback";
import { GameButton } from "../../../src/components/game-button";
import { LoadingScreen } from "../../../src/components/loading-screen";
import { Mascot } from "../../../src/components/mascot";
import { ScreenShell, shellStyles } from "../../../src/components/screen-shell";
import { SessionLobby } from "../../../src/components/session-lobby";
import { StartCountdown } from "../../../src/components/start-countdown";
import { StageProgressBar } from "../../../src/components/stage-progress-bar";
import { StaticHintMap } from "../../../src/components/static-hint-map";
import { StatusChip } from "../../../src/components/status-chip";
import { colors } from "../../../src/theme/tokens";
import {
  ApiClientError,
  SnapshotRefreshPolicies,
  createAuthorizedApiClient,
  type HintUnlockedPayload,
  type SessionStateChangedPayload,
  type SessionTeamSnapshot,
  type TeamProgressChangedPayload,
  type VisibleHintSnapshot
} from "../../../src/lib/api-client";
import { getClientConfig } from "../../../src/lib/config";
import {
  basePointsForDifficulty,
  formatDifficulty,
  formatGameType,
  formatUnlockReason,
  isTreasureHunt
} from "../../../src/lib/stage-progress";
import {
  loadStoredEnrollment,
  type StoredEnrollment
} from "../../../src/lib/session-storage";
import { useSessionManagementConnection } from "../../../src/hooks/use-session-management-connection";
import { useSession } from "../../../src/providers/session-provider";

type ConnectionTone = "neutral" | "info" | "success" | "warn" | "error";

type TeamScore = { score: number; rank: number };

function resolveSessionState(state: string | null): { label: string; tone: ConnectionTone } {
  switch (state) {
    case "Running":
    case "Active":
      return { label: "En juego", tone: "success" };
    case "Paused":
      return { label: "En pausa", tone: "warn" };
    case "Finalized":
      return { label: "Finalizada", tone: "neutral" };
    case "Canceled":
      return { label: "Cancelada", tone: "error" };
    default:
      return { label: "Por iniciar", tone: "info" };
  }
}

function resolveConnection(kind: string): { label: string; tone: ConnectionTone } {
  switch (kind) {
    case "connected":
      return { label: "En línea", tone: "success" };
    case "reconnecting":
      return { label: "Reconectando", tone: "warn" };
    case "error":
      return { label: "Sin conexión", tone: "error" };
    case "connecting":
      return { label: "Conectando", tone: "info" };
    default:
      return { label: "Desconectado", tone: "neutral" };
  }
}

function toneColor(tone: ConnectionTone) {
  switch (tone) {
    case "success":
      return colors.brand.primary;
    case "warn":
      return colors.state.warn.textAlt;
    case "error":
      return colors.state.error.textAlt;
    case "info":
      return colors.brand.secondary;
    default:
      return colors.text.mutedAlt;
  }
}

function readErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : "No pudimos actualizar tu tablero.";
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
  const router = useRouter();
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
  const [selectedChoiceId, setSelectedChoiceId] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<AnswerFeedback | null>(null);
  const [teamScore, setTeamScore] = useState<TeamScore | null>(null);

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

  const refreshScore = useCallback(async (liveSessionId: string) => {
    if (!session || !storedEnrollment || typeof fetch !== "function") {
      return;
    }

    try {
      const response = await fetch(
        `${config.edgeProxyPublicBaseUrl}/scoring-monitoring/api/scoring-monitoring/sessions/${liveSessionId}/ranking`,
        { headers: { Authorization: `Bearer ${session.accessToken}` } }
      );
      if (!response.ok) {
        return;
      }

      const payload = (await response.json()) as {
        items?: { sessionTeamId: string; visibleScore: number; rank: number }[];
      };
      const own = payload.items?.find((item) => item.sessionTeamId === storedEnrollment.teamId);
      setTeamScore(own ? { score: own.visibleScore, rank: own.rank } : null);
    } catch {
      // Score is best-effort; a failed ranking fetch never blocks the board.
    }
  }, [config.edgeProxyPublicBaseUrl, session, storedEnrollment]);

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

  const connectionState = useSessionManagementConnection({
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

        if (payload.currentStage?.missionStageId !== currentSnapshot.currentStage?.missionStageId) {
          setSelectedChoiceId(null);
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
  const triviaChoices = currentStage?.choices ?? [];
  const stagePoints = currentStage ? basePointsForDifficulty(currentStage.difficulty) : null;
  const treasureHuntStage = isTreasureHunt(currentStage?.gameType);
  const currentSessionState = snapshot?.sessionState ?? null;
  const isFinalized = currentSessionState === "Finalized";
  const isCanceled = currentSessionState === "Canceled";
  const isPlaying =
    currentSessionState === "Running" || currentSessionState === "Active" || currentSessionState === "Paused";
  const isLobby = !isPlaying && !isFinalized && !isCanceled;
  const actionBlocked = currentSessionState !== "Running" && currentSessionState !== "Active";
  const countdown = formatCountdown(remainingSeconds);
  const sessionMeta = resolveSessionState(currentSessionState);
  const connectionMeta = resolveConnection(connectionState.kind);
  const totalStages = snapshot?.totalStages ?? 0;
  const memberCount = snapshot?.memberCount ?? 1;
  // Only show live gameplay hints for the stage the team is currently on; snapshot.visibleHints
  // accumulates hints from every stage, so without this filter the previous stage's hint lingers
  // after advancing to the next game.
  const currentMissionStageId = snapshot?.currentStage?.missionStageId;
  const visibleGameplayHints = useMemo(
    () =>
      snapshot?.visibleHints.filter(
        (hint) => !hint.isSolution && hint.missionStageId === currentMissionStageId
      ) ?? [],
    [snapshot?.visibleHints, currentMissionStageId]
  );
  const resolutionHintsByStage = useMemo(
    () => groupHintsByStage(snapshot?.visibleHints ?? []),
    [snapshot?.visibleHints]
  );

  // While waiting in the lobby there is no realtime "player joined" event, so poll the snapshot to
  // show the roster fill up (and to auto-leave when the operator starts the session).
  useEffect(() => {
    if (!isLobby || !storedEnrollment) {
      return;
    }

    const intervalId = setInterval(() => void refreshSnapshot(), 4000);
    return () => clearInterval(intervalId);
  }, [isLobby, storedEnrollment, refreshSnapshot]);

  // Keep the team score fresh: on load, on stage advance, and when the session ends.
  useEffect(() => {
    const liveSessionId = snapshot?.liveSessionId;
    if (!liveSessionId) {
      return;
    }

    void refreshScore(liveSessionId);
  }, [refreshScore, snapshot?.liveSessionId, snapshot?.sessionState, snapshot?.currentStage?.sessionStageOrder]);

  // Play the green sweep + 3·2·1 countdown once, only on the genuine lobby -> playing transition
  // (the moment the operator starts). Sessions already Active on load skip straight to the board.
  const [showStartTransition, setShowStartTransition] = useState(false);
  const wasLobbyRef = useRef(isLobby);
  useEffect(() => {
    if (wasLobbyRef.current && !isLobby && isPlaying) {
      setShowStartTransition(true);
    }
    wasLobbyRef.current = isLobby;
  }, [isLobby, isPlaying]);

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
              detail: "¡Código correcto! Avanzas a la siguiente etapa.",
              points: stagePoints
            }
          : {
              tone: "error",
              title: "¡Uy, casi!",
              detail: "Código incorrecto, inténtalo de nuevo."
            }
      );
      setScannerVisible(false);
      await refreshSnapshot();
    } catch (error) {
      const detail =
        error instanceof ApiClientError
          ? error.message
          : "No pudimos enviar el código. Revisa tu conexión.";
      setFeedback({
        tone: "error",
        title: "No se pudo enviar",
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

    if (!selectedChoiceId) {
      return;
    }

    setSubmitting(true);
    setFeedback(null);

    try {
      const result = await apiClient.submitTriviaAnswer({
        sessionTeamId: storedEnrollment.teamId,
        selectedChoiceId
      });

      setFeedback(
        result.validationOutcome === "Accepted"
          ? {
              tone: "success",
              title: "Respuesta correcta",
              detail: "¡Bien hecho! Vas por buen camino.",
              points: stagePoints
            }
          : {
              tone: "error",
              title: "¡Uy, casi!",
              detail: "Respuesta incorrecta, intenta de nuevo."
            }
      );
      if (result.validationOutcome === "Accepted") {
        setSelectedChoiceId(null);
      }
      await refreshSnapshot();
    } catch (error) {
      const detail =
        error instanceof ApiClientError
          ? error.message
          : "No pudimos enviar tu respuesta. Revisa tu conexión.";
      setFeedback({
        tone: "error",
        title: "No se pudo enviar",
        detail
      });
    } finally {
      setSubmitting(false);
    }
  }

  if (!session) {
    return <LoadingScreen message="Verificando tu sesión..." />;
  }

  if (loadingEnrollment) {
    return <LoadingScreen message="Buscando tu equipo..." />;
  }

  if (!storedEnrollment) {
    return <Redirect href="/mobile/join" />;
  }

  if (!snapshot) {
    return <LoadingScreen message="Cargando tu tablero..." />;
  }

  return (
    <>
    <ScreenShell
      eyebrow="Tablero"
      title={`Equipo ${snapshot.teamName}`}
      description="Resuelve la etapa actual, revisa tus pistas y envía tu evidencia en tiempo real."
    >
      {isLobby ? (
        <SessionLobby
          teamName={snapshot.teamName}
          memberCount={memberCount}
          connectionLabel={connectionMeta.label}
          connectionTone={connectionMeta.tone}
        />
      ) : isFinalized ? (
        <>
          <View style={shellStyles.card}>
            <View style={styles.headerRow}>
              <StatusChip label={sessionMeta.label} tone={sessionMeta.tone} />
              <View style={styles.connRow}>
                <View style={[styles.connDot, { backgroundColor: toneColor(connectionMeta.tone) }]} />
                <Text style={styles.connLabel}>{connectionMeta.label}</Text>
              </View>
            </View>
          </View>

          <View style={styles.resultHero}>
            <Mascot mood="celebrate" size={96} />
            <Text style={styles.resultTitle}>¡Sesión finalizada!</Text>
            {teamScore ? (
              <>
                <Text style={styles.resultPlace}>Terminaste en el puesto #{teamScore.rank}</Text>
                <Text style={styles.resultScore}>{teamScore.score}</Text>
                <Text style={styles.resultScoreLabel}>puntos conseguidos</Text>
              </>
            ) : (
              <Text style={styles.resultPlace}>Revisa la clasificación para ver tu puntaje final.</Text>
            )}
            <GameButton
              label="Ver clasificación"
              icon="🏆"
              onPress={() => router.push("/mobile/ranking")}
            />
          </View>

          <View style={styles.resolutionsSection}>
            <Text style={styles.resolutionsTitle}>Resoluciones de la Misión</Text>
            {(snapshot.allStages ?? []).length ? (
              snapshot.allStages?.map((stage) => {
                const stageHints = resolutionHintsByStage[stage.missionStageId] ?? [];

                return (
                  <View key={stage.missionStageId} style={styles.resolutionStageCard}>
                    <View style={styles.resolutionStageHeader}>
                      <Text style={styles.resolutionStageTitle}>{stage.name}</Text>
                      <StatusChip label={`Etapa ${stage.sessionStageOrder}`} tone="neutral" />
                    </View>
                    <View style={shellStyles.row}>
                      <StatusChip label={formatDifficulty(stage.difficulty)} tone="info" />
                      <StatusChip label={formatGameType(stage.gameType)} tone="success" />
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
                              label={hint.isSolution ? "Solución" : "Pista"}
                              tone={hint.isSolution ? "warn" : "info"}
                            />
                            <StatusChip label={formatUnlockReason(hint.unlockReason)} tone="neutral" />
                          </View>
                          <Text style={styles.resolutionHintText}>{hint.content}</Text>
                          {isStaticMapReady(hint) ? (
                            <StaticHintMap latitude={hint.latitude!} longitude={hint.longitude!} />
                          ) : null}
                        </View>
                      ))
                    ) : (
                      <Text style={shellStyles.cardText}>No se registraron pistas en esta etapa.</Text>
                    )}
                  </View>
                );
              })
            ) : (
              <View style={shellStyles.card}>
                <StatusChip label="Preparando resultados" tone="warn" />
                <Text style={shellStyles.cardText}>Todavía no hay un resumen de etapas.</Text>
              </View>
            )}
          </View>
        </>
      ) : isCanceled ? (
        <View style={styles.resultHero}>
          <Mascot mood="sad" size={96} />
          <Text style={styles.resultTitle}>Sesión cancelada</Text>
          <Text style={styles.resultPlace}>El operador canceló esta sesión. Puedes volver al inicio.</Text>
          <GameButton label="Ir al inicio" variant="secondary" icon="🏠" onPress={() => router.push("/mobile/home")} />
        </View>
      ) : (
        <>
          <View style={shellStyles.card}>
            <View style={styles.headerRow}>
              <View style={styles.headerChips}>
                <StatusChip label={sessionMeta.label} tone={sessionMeta.tone} />
                {teamScore ? <StatusChip label={`${teamScore.score} pts`} tone="success" /> : null}
                {countdown ? <StatusChip label={countdown} tone="warn" /> : null}
              </View>
              <View style={styles.connRow}>
                <View style={[styles.connDot, { backgroundColor: toneColor(connectionMeta.tone) }]} />
                <Text style={styles.connLabel}>{connectionMeta.label}</Text>
              </View>
            </View>
            {totalStages > 0 ? (
              <StageProgressBar total={totalStages} currentOrder={currentStage?.sessionStageOrder ?? 0} />
            ) : null}
            {snapshotError ? <Text style={styles.error}>{snapshotError}</Text> : null}
          </View>

          <View style={shellStyles.card}>
            {currentStage ? (
              <>
                <View style={shellStyles.row}>
                  <StatusChip label={formatGameType(currentStage.gameType)} tone="info" />
                  {currentStage.difficulty ? (
                    <StatusChip label={formatDifficulty(currentStage.difficulty)} tone="neutral" />
                  ) : null}
                  {stagePoints ? <StatusChip label={`${stagePoints} pts`} tone="success" /> : null}
                </View>
                <Text style={styles.stageTitle}>{currentStage.name}</Text>
                {currentStage.prompt ? (
                  <View style={styles.promptCard}>
                    <Text style={styles.promptLabel}>
                      {treasureHuntStage ? "🧭 Tu misión" : "🧩 Pregunta"}
                    </Text>
                    <Text style={styles.promptText}>{currentStage.prompt}</Text>
                  </View>
                ) : null}
              </>
            ) : (
              <Text style={shellStyles.cardText}>
                El operador aún no habilita una etapa jugable para tu equipo.
              </Text>
            )}
          </View>

          {currentStage ? (
            <View style={shellStyles.card}>
              <Text style={shellStyles.cardTitle}>
                {treasureHuntStage ? "Escanea el QR del tesoro" : "Tu respuesta"}
              </Text>
              <Text style={shellStyles.cardText}>
                {treasureHuntStage
                  ? "Encuentra el punto de la pista y escanea su código QR para validar la etapa."
                  : "Selecciona la respuesta correcta. La validación es inmediata."}
              </Text>
              {actionBlocked ? (
                <Text style={styles.warning}>La sesión está en pausa. Espera a que el operador la reanude.</Text>
              ) : treasureHuntStage ? (
                <GameButton
                  label={submitting ? "Enviando QR..." : "Escanear QR"}
                  icon="📷"
                  disabled={actionBlocked || submitting}
                  loading={submitting}
                  onPress={() => setScannerVisible(true)}
                />
              ) : (
                <>
                  <Text style={styles.choicePrompt}>Selecciona la respuesta correcta</Text>
                  <View style={styles.choiceGrid}>
                    {triviaChoices.map((choice) => {
                      const isSelected = selectedChoiceId === choice.id;

                      return (
                        <Pressable
                          accessibilityRole="button"
                          accessibilityState={{ selected: isSelected }}
                          disabled={actionBlocked || submitting}
                          key={choice.id}
                          onPress={() => {
                            setSelectedChoiceId(choice.id);
                          }}
                          style={({ pressed }) => [
                            styles.choiceButton,
                            isSelected && styles.choiceButtonSelected,
                            (actionBlocked || submitting) && styles.choiceButtonDisabled,
                            pressed && styles.buttonPressed
                          ]}
                        >
                          <Text
                            style={[
                              styles.choiceButtonLabel,
                              isSelected && styles.choiceButtonLabelSelected
                            ]}
                          >
                            {choice.text}
                          </Text>
                        </Pressable>
                      );
                    })}
                  </View>
                  <GameButton
                    label={submitting ? "Enviando..." : "Enviar"}
                    icon="✓"
                    disabled={actionBlocked || submitting || !selectedChoiceId}
                    loading={submitting}
                    onPress={() => void submitTriviaEvidence()}
                  />
                </>
              )}
            </View>
          ) : null}

          <View style={shellStyles.section}>
            <Text style={shellStyles.cardTitle}>Pistas</Text>
            {visibleGameplayHints.length ? (
              visibleGameplayHints.map((hint) => (
                <View key={hint.hintId} style={styles.hintItem}>
                  <View style={shellStyles.row}>
                    <StatusChip label="Pista" tone="info" />
                    <StatusChip label={formatUnlockReason(hint.unlockReason)} tone="neutral" />
                  </View>
                  <Text style={shellStyles.cardText}>{hint.content}</Text>
                  {isStaticMapReady(hint) ? (
                    <StaticHintMap latitude={hint.latitude!} longitude={hint.longitude!} />
                  ) : null}
                </View>
              ))
            ) : (
              <View style={shellStyles.card}>
                <StatusChip label="Sin pistas aún" tone="warn" />
                <Text style={shellStyles.cardText}>
                  Cuando el operador libere una pista para tu equipo, aparecerá aquí al instante.
                </Text>
              </View>
            )}
          </View>
        </>
      )}

      <AnswerFeedbackSheet feedback={feedback} onContinue={() => setFeedback(null)} />

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
              <Text style={styles.scannerTitle}>Escanea el QR</Text>
              <Text style={styles.scannerText}>
                Mantén el código dentro del marco. Se valida al instante para tu etapa actual.
              </Text>
              {submitting ? (
                <View style={styles.inlineStatus}>
                  <ActivityIndicator color={colors.text.onBrand} />
                  <Text style={styles.inlineStatusText}>Enviando...</Text>
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
    {showStartTransition ? (
      <StartCountdown onDone={() => setShowStartTransition(false)} />
    ) : null}
    </>
  );
}

const styles = StyleSheet.create({
  headerRow: {
    alignItems: "center",
    flexDirection: "row",
    justifyContent: "space-between"
  },
  headerChips: {
    alignItems: "center",
    flexDirection: "row",
    flexWrap: "wrap",
    gap: 8
  },
  connRow: {
    alignItems: "center",
    flexDirection: "row",
    gap: 8
  },
  connDot: {
    borderRadius: 999,
    height: 10,
    width: 10
  },
  connLabel: {
    color: colors.text.secondary,
    fontSize: 13,
    fontWeight: "700"
  },
  inlineStatus: {
    alignItems: "center",
    flexDirection: "row",
    gap: 10
  },
  inlineStatusText: {
    color: colors.text.onBrand,
    fontSize: 14,
    fontWeight: "700"
  },
  warning: {
    color: colors.state.warn.text,
    fontSize: 14,
    lineHeight: 20
  },
  choicePrompt: {
    color: colors.text.primary,
    fontSize: 14,
    fontWeight: "700",
    lineHeight: 20
  },
  choiceGrid: {
    flexDirection: "row",
    flexWrap: "wrap",
    gap: 10
  },
  choiceButton: {
    alignItems: "center",
    backgroundColor: colors.surface.card,
    borderColor: colors.surface.cardBorder,
    borderRadius: 16,
    borderWidth: 1.5,
    flexBasis: "47%",
    flexGrow: 1,
    justifyContent: "center",
    minHeight: 56,
    paddingHorizontal: 14,
    paddingVertical: 12
  },
  choiceButtonSelected: {
    backgroundColor: colors.brand.primaryTint,
    borderColor: colors.brand.primary
  },
  choiceButtonDisabled: {
    opacity: 0.5
  },
  choiceButtonLabel: {
    color: colors.text.primary,
    fontSize: 15,
    fontWeight: "700",
    textAlign: "center"
  },
  choiceButtonLabelSelected: {
    color: colors.brand.primaryStrong,
    fontWeight: "800"
  },
  resultHero: {
    alignItems: "center",
    backgroundColor: colors.brand.primaryTint,
    borderColor: colors.brand.primary,
    borderRadius: 26,
    borderWidth: 2,
    gap: 8,
    paddingHorizontal: 22,
    paddingVertical: 28
  },
  resultTitle: {
    color: colors.text.primary,
    fontSize: 24,
    fontWeight: "900",
    textAlign: "center"
  },
  resultPlace: {
    color: colors.text.primary,
    fontSize: 16,
    fontWeight: "700",
    textAlign: "center"
  },
  resultScore: {
    color: colors.brand.primaryStrong,
    fontSize: 48,
    fontWeight: "900",
    lineHeight: 54
  },
  resultScoreLabel: {
    color: colors.text.secondary,
    fontSize: 13,
    fontWeight: "700",
    letterSpacing: 0.6,
    textTransform: "uppercase"
  },
  resolutionsSection: {
    backgroundColor: colors.brand.primaryTintAlt,
    borderColor: colors.brand.primaryRing,
    borderRadius: 18,
    borderWidth: 1,
    gap: 14,
    padding: 14
  },
  resolutionsTitle: {
    color: colors.text.primary,
    fontSize: 22,
    fontWeight: "900",
    lineHeight: 28
  },
  resolutionStageCard: {
    backgroundColor: colors.surface.card,
    borderColor: colors.surface.cardBorder,
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
    color: colors.text.primary,
    fontSize: 19,
    fontWeight: "900",
    lineHeight: 25
  },
  resolutionHintItem: {
    backgroundColor: colors.surface.card,
    borderColor: colors.surface.cardBorder,
    borderRadius: 16,
    borderWidth: 1,
    gap: 10,
    padding: 12
  },
  solutionItem: {
    backgroundColor: colors.state.warn.fillMuted,
    borderColor: colors.state.warn.borderMuted,
    borderRadius: 16,
    borderWidth: 2,
    gap: 10,
    padding: 12
  },
  resolutionHintText: {
    color: colors.text.primary,
    fontSize: 15,
    fontWeight: "700",
    lineHeight: 21
  },
  scannerModal: {
    backgroundColor: colors.overlay.scrim,
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
    borderColor: colors.text.onBrand,
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
    color: colors.text.onBrand,
    fontSize: 22,
    fontWeight: "800"
  },
  scannerText: {
    color: colors.overlay.text,
    fontSize: 14,
    lineHeight: 20
  },
  scannerCancelButton: {
    alignItems: "center",
    backgroundColor: colors.surface.card,
    borderRadius: 16,
    paddingVertical: 12
  },
  scannerCancelLabel: {
    color: colors.text.primary,
    fontSize: 15,
    fontWeight: "800"
  },
  buttonPressed: {
    opacity: 0.85
  },
  error: {
    color: colors.state.error.text,
    fontSize: 14,
    lineHeight: 20
  },
  stageTitle: {
    color: colors.text.primary,
    fontSize: 24,
    fontWeight: "800",
    lineHeight: 30
  },
  promptCard: {
    backgroundColor: colors.brand.secondaryTint,
    borderColor: colors.brand.secondaryRing,
    borderRadius: 18,
    borderWidth: 1,
    gap: 8,
    padding: 16
  },
  promptLabel: {
    color: colors.brand.secondaryRing,
    fontSize: 13,
    fontWeight: "800",
    letterSpacing: 0.4,
    textTransform: "uppercase"
  },
  promptText: {
    color: colors.text.primary,
    fontSize: 18,
    fontWeight: "600",
    lineHeight: 26
  },
  hintItem: {
    backgroundColor: colors.surface.card,
    borderColor: colors.surface.cardBorder,
    borderRadius: 18,
    borderWidth: 1,
    gap: 10,
    padding: 14
  }
});
