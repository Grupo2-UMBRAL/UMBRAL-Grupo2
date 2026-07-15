"use client";

import { type FormEvent, useCallback, useEffect, useMemo, useState } from "react";
import {
  HubConnectionBuilder,
  HttpTransportType,
  LogLevel
} from "@microsoft/signalr";
import { getClientConfig } from "@/lib/config";
import "./live-session-dashboard.css";

// Vocabulario del front: "etapa" = la unidad jugable (el "play" del backend),
// "misión" = la plantilla, "sesión" = la LiveSession en curso. El endpoint de
// mission-management habla "play"; lo adaptamos al modelo "etapa" en el borde del
// fetch (toEligibleMissionSummary / toEligibleMissionDetail) para que el resto de la
// UI permanezca en "etapa".

type EligibleMissionSummary = {
  id: string;
  name: string;
  maximumDurationMinutes: number;
  activeMissionStageCount: number;
};

type EligibleMissionStageHint = {
  content: string;
  isSolution: boolean;
  latitude: number | null;
  longitude: number | null;
};

type EligibleMissionStage = {
  id: string;
  name: string;
  sourceOrder: number;
  resolvedTimeBudgetMinutes: number;
  gameType: string;
  prompt: string;
  expectedQrHash: string | null;
  hints: EligibleMissionStageHint[];
};

type EligibleMissionDetail = {
  id: string;
  name: string;
  description: string;
  maximumDurationMinutes: number;
  missionStages: EligibleMissionStage[];
};

// --- forma de cable "play" de mission-management (adaptada a "etapa" arriba) ---
type EligiblePlayPayload = {
  id: string;
  order: number;
  gameType: string;
  timeLimitMinutes: number;
  prompt: string;
  expectedQrHash: string | null;
  hints: { content: string; isSolution: boolean; latitude: number | null; longitude: number | null }[] | null;
};

type EligibleMissionSummaryPayload = {
  id: string;
  name: string;
  maximumDurationMinutes: number;
  activePlayCount: number;
};

type EligibleMissionDetailPayload = {
  id: string;
  name: string;
  description: string;
  maximumDurationMinutes: number;
  plays: EligiblePlayPayload[] | null;
};

function toEligibleMissionSummary(payload: EligibleMissionSummaryPayload): EligibleMissionSummary {
  return {
    id: payload.id,
    name: payload.name,
    maximumDurationMinutes: payload.maximumDurationMinutes,
    activeMissionStageCount: payload.activePlayCount
  };
}

function toEligibleMissionStage(play: EligiblePlayPayload): EligibleMissionStage {
  return {
    id: play.id,
    name: `Etapa ${play.order}`,
    sourceOrder: play.order,
    resolvedTimeBudgetMinutes: play.timeLimitMinutes,
    gameType: play.gameType,
    prompt: play.prompt,
    expectedQrHash: play.expectedQrHash ?? null,
    hints: (play.hints ?? []).map((hint) => ({
      content: hint.content,
      isSolution: hint.isSolution,
      latitude: hint.latitude,
      longitude: hint.longitude
    }))
  };
}

function toEligibleMissionDetail(payload: EligibleMissionDetailPayload): EligibleMissionDetail {
  return {
    id: payload.id,
    name: payload.name,
    description: payload.description,
    maximumDurationMinutes: payload.maximumDurationMinutes,
    missionStages: (payload.plays ?? []).map(toEligibleMissionStage)
  };
}

type LiveSessionStageHint = {
  id: string;
  content: string;
  isSolution: boolean;
  latitude: number | null;
  longitude: number | null;
};

type LiveSessionStage = {
  missionStageId: string;
  name: string;
  sessionStageOrder: number;
  sourceOrder: number;
  resolvedTimeBudgetMinutes: number;
  gameType: string;
  prompt: string;
  expectedQrHash: string | null;
  triviaValidAnswer: string | null;
  triviaInitialValidationCriterion: string | null;
  hints: LiveSessionStageHint[];
};

type LiveSession = {
  id: string;
  missionId: string;
  missionName: string;
  name: string;
  state: string;
  scheduledStartAtUtc: string | null;
  createdAtUtc: string;
  joinCode: string | null;
  enrollmentWindowOpenedAtUtc: string | null;
  enrollmentWindowClosedAtUtc: string | null;
  registeredSessionTeamCount: number;
  sessionStageFlow: LiveSessionStage[];
};

type LiveSessionLifecycleAction = "start" | "pause" | "resume" | "finalize" | "cancel";

type EnrollmentAction = "generate-join-code" | "open-window" | "close-window";
type CurrentSessionStageSnapshot = {
  missionStageId: string;
  name: string;
  sessionStageOrder: number;
  sourceOrder: number;
  resolvedTimeBudgetMinutes: number;
  difficulty: string;
  gameType: string;
};

type VisibleHintSnapshot = {
  hintId: string;
  missionStageId: string;
  content: string;
  isSolution: boolean;
  latitude: number | null;
  longitude: number | null;
  unlockedAtUtc: string;
  unlockReason: string;
};

type LiveSessionOverviewTeam = {
  sessionTeamId: string;
  teamName: string;
  participantCount: number;
  progressState: string;
  currentStage: CurrentSessionStageSnapshot | null;
  releasedHints: VisibleHintSnapshot[];
};

type LiveSessionOverview = {
  liveSessionId: string;
  name: string;
  missionId: string;
  missionName: string;
  sessionState: string;
  scheduledStartAtUtc: string | null;
  remainingSeconds?: number;
  serverTimeUtc: string;
  sync: {
    sequenceNumber: number;
    lastUpdatedUtc: string;
    serverTimeUtc: string;
  };
  sessionTeams: LiveSessionOverviewTeam[];
};

type SessionTeamReleasedHintDetail = {
  releasedHintId: string;
  hintId: string;
  missionStageId: string;
  stageName: string;
  content: string;
  isSolution: boolean;
  latitude: number | null;
  longitude: number | null;
  releasedAtUtc: string;
  unlockReason: string;
};

type SessionTeamEvidenceSubmissionDetail = {
  id: string;
  missionStageId: string;
  stageName: string;
  sessionStageOrder: number;
  difficulty: string;
  gameType: string;
  submittedHash: string | null;
  submittedText: string | null;
  validationOutcome: string;
  failureReason: string | null;
  submittedAtUtc: string;
  isTriviaCorrectionEligible: boolean;
};

type SessionTeamDetailResponse = {
  liveSessionId: string;
  liveSessionName: string;
  sessionState: string;
  sessionTeamId: string;
  teamName: string;
  participantCount: number;
  progressState: string;
  currentStage: CurrentSessionStageSnapshot | null;
  currentStageStartedAtUtc: string;
  releasedHints: SessionTeamReleasedHintDetail[];
  evidenceSubmissions: SessionTeamEvidenceSubmissionDetail[];
  isInactive: boolean;
  inactivityThresholdMinutes: number;
  serverTimeUtc: string;
  sync: {
    sequenceNumber: number;
    lastUpdatedUtc: string;
    serverTimeUtc: string;
  };
};

type TeamDetailTimelineItem =
  | {
      kind: "hint";
      id: string;
      occurredAtUtc: string;
      stageName: string;
      hint: SessionTeamReleasedHintDetail;
    }
  | {
      kind: "submission";
      id: string;
      occurredAtUtc: string;
      stageName: string;
      submission: SessionTeamEvidenceSubmissionDetail;
    };

type SnapshotRefreshPolicy = 1 | 2 | "ApplyIncremental" | "RefreshSnapshot";

type RealtimeEventMetadata = {
  liveSessionId: string;
  sequenceNumber: number;
  occurredAtUtc: string;
  refreshPolicy: SnapshotRefreshPolicy;
  reason: string;
};

type SessionStateChangedPayload = {
  metadata: RealtimeEventMetadata;
  previousState: string;
  currentState: string;
  remainingSeconds?: number | null;
};

type TeamProgressChangedPayload = {
  metadata: RealtimeEventMetadata;
  sessionTeamId: string;
  previousStage?: CurrentSessionStageSnapshot | null;
  currentStage?: CurrentSessionStageSnapshot | null;
  progressState: string;
};

type LegacyLiveSessionStateChangedEvent = {
  liveSessionId: string;
  previousState: string;
  state: string;
  registeredSessionTeamCount: number;
  occurredAtUtc: string;
};

type RealtimeConnectionState =
  | { kind: "connecting"; label: "Reconectando"; detail: string }
  | { kind: "connected"; label: "Conectado"; detail: string }
  | { kind: "reconnecting"; label: "Reconectando"; detail: string }
  | { kind: "disconnected"; label: "Desconectado"; detail: string }
  | { kind: "error"; label: "Desconectado"; detail: string };

type RankingItem = {
  rank: number;
  sessionTeamId: string;
  visibleScore: number;
  resolutionTime: string;
};

type RankingPayload = {
  liveSessionId: string;
  generatedAtUtc: string;
  items: RankingItem[];
};

type SessionEventLogItem = {
  id: string;
  liveSessionId: string;
  eventType: string;
  description: string;
  timestamp: string;
};

type PenaltySeverity = "Minor" | "Major" | "Critical";

type PenaltyDraft = {
  sessionTeamId: string;
  severity: PenaltySeverity;
  reason: string;
  commandId: string;
};

type ApplyPenaltyResponse = {
  liveSessionId: string;
  sessionTeamId: string;
  commandId: string;
  penaltyId: string | null;
  scoreEntryId: string | null;
  penaltyApplied: boolean;
  visibleScore: number;
  ranking: RankingPayload;
  recordedAtUtc: string;
};

type LiveSessionDraft = {
  name: string;
  scheduledStartAtLocal: string;
};

type OperationalHintDraft = {
  missionStageId: string;
  content: string;
  latitude: string;
  longitude: string;
};

type SessionStageOperationalStatus = "Completed" | "Pending";

type LiveSessionsWorkspaceProps = {
  accessToken: string;
};

type LifecycleActionViewModel = {
  action: LiveSessionLifecycleAction;
  label: string;
  requiresConfirmation: boolean;
  disabled: boolean;
  available: boolean;
};

type LiveSessionOverviewDashboardProps = {
  liveSession: LiveSession;
  overview: LiveSessionOverview | null;
  connectionState: RealtimeConnectionState;
  scoringConnectionState: RealtimeConnectionState;
  isLoadingOverview: boolean;
  isLoadingEventLog: boolean;
  lifecycleActions: LifecycleActionViewModel[];
  lifecycleActionPending: LiveSessionLifecycleAction | null;
  rankingItems: RankingItem[];
  eventLogItems: SessionEventLogItem[];
  isLoadingRanking: boolean;
  rankingError: string | null;
  eventLogError: string | null;
  selectedSessionTeamId: string | null;
  sessionTeamDetail: SessionTeamDetailResponse | null;
  sessionTeamDetailError: string | null;
  inactivityThresholdMinutes: number;
  isLoadingSessionTeamDetail: boolean;
  overridePendingSubmissionId: string | null;
  onSelectSessionTeam: (sessionTeamId: string | null) => void;
  onRefreshSessionTeamDetail: () => void;
  onInactivityThresholdChange: (nextValue: number) => void;
  onOverrideSubmission: (submissionId: string, reason: string) => void;
  onLifecycleAction: (action: LiveSessionLifecycleAction) => void;
  onRefreshEventLog: () => void;
  onRefreshOverview: () => void;
  onRefreshRanking: () => void;
};

function createAuthorizedHeaders(accessToken: string) {
  return {
    Authorization: `Bearer ${accessToken}`
  };
}

function createEmptyDraft(): LiveSessionDraft {
  return {
    name: "",
    scheduledStartAtLocal: ""
  };
}

function createEmptyOperationalHintDraft(): OperationalHintDraft {
  return {
    missionStageId: "",
    content: "",
    latitude: "",
    longitude: ""
  };
}

function createPenaltyCommandId() {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }

  return `penalty-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

function createEmptyPenaltyDraft(sessionTeamId = ""): PenaltyDraft {
  return {
    sessionTeamId,
    severity: "Minor",
    reason: "",
    commandId: createPenaltyCommandId()
  };
}

async function readFailureDetail(response: Response) {
  const contentType = response.headers.get("content-type") ?? "";

  try {
    if (contentType.includes("application/json")) {
      const payload = await response.json();

      if (typeof payload === "string" && payload.trim()) {
        return payload;
      }

      if (payload && typeof payload === "object") {
        if ("detail" in payload && typeof payload.detail === "string" && payload.detail.trim()) {
          return payload.detail;
        }

        if ("title" in payload && typeof payload.title === "string" && payload.title.trim()) {
          return payload.title;
        }
      }
    }

    const text = await response.text();
    if (text.trim()) {
      return text.trim();
    }
  } catch {
    return `${response.status} ${response.statusText}`;
  }

  return `${response.status} ${response.statusText}`;
}

function formatTimestamp(value: string | null) {
  if (!value) {
    return "No programado";
  }

  const timestamp = new Date(value);
  if (Number.isNaN(timestamp.getTime())) {
    return value;
  }

  return timestamp.toISOString().replace("T", " ").replace(/\.\d{3}Z$/, " UTC");
}

function formatShortTimestamp(value: string | null | undefined) {
  if (!value) {
    return "Sin sincronización";
  }

  const timestamp = new Date(value);
  if (Number.isNaN(timestamp.getTime())) {
    return value;
  }

  return timestamp.toLocaleTimeString([], {
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit"
  });
}

function formatRelativeTimestamp(value: string) {
  const timestamp = new Date(value);
  if (Number.isNaN(timestamp.getTime())) {
    return value;
  }

  const elapsedSeconds = Math.max(0, Math.floor((Date.now() - timestamp.getTime()) / 1000));

  if (elapsedSeconds < 10) {
    return "Ahora";
  }

  if (elapsedSeconds < 60) {
    return `hace ${elapsedSeconds}s`;
  }

  const elapsedMinutes = Math.floor(elapsedSeconds / 60);
  if (elapsedMinutes < 60) {
    return `hace ${elapsedMinutes}m`;
  }

  const elapsedHours = Math.floor(elapsedMinutes / 60);
  if (elapsedHours < 24) {
    return `hace ${elapsedHours}h`;
  }

  const elapsedDays = Math.floor(elapsedHours / 24);
  return `hace ${elapsedDays}d`;
}

function formatRemainingSeconds(value: number | null | undefined) {
  if (value === null || value === undefined) {
    return "Sin temporizador";
  }

  const clampedValue = Math.max(0, value);
  const hours = Math.floor(clampedValue / 3600);
  const minutes = Math.floor((clampedValue % 3600) / 60);
  const seconds = clampedValue % 60;

  if (hours > 0) {
    return `${hours}h ${minutes.toString().padStart(2, "0")}m`;
  }

  return `${minutes}m ${seconds.toString().padStart(2, "0")}s`;
}

function formatElapsedDuration(startedAtUtc: string, nowMs: number) {
  const startedAtMs = new Date(startedAtUtc).getTime();
  if (Number.isNaN(startedAtMs)) {
    return "Desconocido";
  }

  const elapsedSeconds = Math.max(0, Math.floor((nowMs - startedAtMs) / 1000));
  const hours = Math.floor(elapsedSeconds / 3600);
  const minutes = Math.floor((elapsedSeconds % 3600) / 60);
  const seconds = elapsedSeconds % 60;

  if (hours > 0) {
    return `${hours}h ${minutes.toString().padStart(2, "0")}m ${seconds.toString().padStart(2, "0")}s`;
  }

  return `${minutes}m ${seconds.toString().padStart(2, "0")}s`;
}

function formatResolutionTime(value: string) {
  const [hours = "00", minutes = "00", seconds = "00"] = value.split(":");
  const secondParts = seconds.split(".");
  return `${hours.padStart(2, "0")}:${minutes.padStart(2, "0")}:${secondParts[0].padStart(2, "0")}`;
}

function findRankingTeamName(sessionTeamId: string, teams: LiveSessionOverviewTeam[]) {
  return (
    teams.find((team) => team.sessionTeamId === sessionTeamId)?.teamName ??
    `Equipo de sesión ${sessionTeamId.slice(0, 8)}`
  );
}

function translateSessionState(sessionState: string): string {
  switch (sessionState) {
    case "Scheduled":
      return "Programada";
    case "Active":
    case "Running":
      return "Activa";
    case "Paused":
      return "Pausada";
    case "Completed":
      return "Completada";
    case "Cancelled":
    case "Canceled":
      return "Cancelada";
    default:
      return sessionState;
  }
}

function translateProgressState(progressState: string): string {
  switch (progressState) {
    case "NotStarted":
      return "No iniciado";
    case "Active":
      return "Activo";
    case "Completed":
      return "Completado";
    default:
      return progressState;
  }
}

function translateGameType(gameType?: string): string {
  if (!gameType) return "Desconocido";
  const normalized = gameType.replace(/\s+/g, "").toLowerCase();
  switch (normalized) {
    case "treasurehunt":
      return "Búsqueda del Tesoro";
    case "trivia":
      return "Trivia";
    default:
      return gameType;
  }
}

function translateDifficulty(difficulty?: string): string {
  if (!difficulty) return "N/A";
  const norm = difficulty.toLowerCase().trim();
  switch (norm) {
    case "easy":
      return "Fácil";
    case "medium":
      return "Medio";
    case "hard":
      return "Difícil";
    default:
      return difficulty;
  }
}

function translateValidationOutcome(outcome: string): string {
  switch (outcome) {
    case "Accepted":
      return "Aceptado";
    case "Rejected":
      return "Rechazado";
    case "Pending":
      return "Pendiente";
    default:
      return outcome;
  }
}

function parseOptionalCoordinate(value: string) {
  if (!value.trim()) {
    return null;
  }

  const parsed = Number(value);
  if (!Number.isFinite(parsed)) {
    throw new Error("Las coordenadas deben ser valores numéricos.");
  }

  return parsed;
}

function normalizeInactivityThresholdMinutes(value: number) {
  return Number.isFinite(value) ? Math.max(1, Math.floor(value)) : 10;
}

function getReleasedHintsForTeam(team: LiveSessionOverviewTeam) {
  return team.releasedHints ?? [];
}

function getConnectionIndicatorClass(connectionState: RealtimeConnectionState) {
  if (connectionState.kind === "connected") {
    return "connection-indicator";
  }

  if (connectionState.kind === "connecting" || connectionState.kind === "reconnecting") {
    return "connection-indicator";
  }

  return "connection-indicator";
}

function getConnectionDotClass(connectionState: RealtimeConnectionState) {
  if (connectionState.kind === "connected") {
    return "status-dot status-dot-green status-dot-pulse";
  }

  if (connectionState.kind === "connecting" || connectionState.kind === "reconnecting") {
    return "status-dot status-dot-amber status-dot-pulse";
  }

  return "status-dot status-dot-red";
}

function getSessionStateBadgeClass(sessionState: string) {
  if (["Active", "Running"].includes(sessionState)) {
    return "badge badge-green";
  }

  if (sessionState === "Paused") {
    return "badge badge-amber";
  }

  if (sessionState === "Scheduled") {
    return "badge badge-blue";
  }

  if (["Completed", "Finalized"].includes(sessionState)) {
    return "badge badge-accent";
  }

  if (["Cancelled", "Canceled"].includes(sessionState)) {
    return "badge badge-red";
  }

  return "badge badge-muted";
}

function getProgressBadgeClass(progressState: string) {
  if (progressState === "Completed") {
    return "badge badge-accent";
  }

  if (progressState === "NotStarted") {
    return "badge badge-muted";
  }

  return "badge badge-green";
}

function getStageLabel(stage: CurrentSessionStageSnapshot | null) {
  if (!stage) {
    return "Sin etapa actual";
  }

  return `#${stage.sessionStageOrder} ${stage.name}`;
}

type StageProgressBlockStatus = "done" | "current" | "pending";

type StageProgressBlock = {
  order: number;
  name: string;
  status: StageProgressBlockStatus;
};

// Maps a team onto one block per Session Stage: stages before the team's current
// one are done, the current one is highlighted, the rest are pending. A team that
// finished the flow shows every block as done.
function getTeamStageProgress(
  team: LiveSessionOverviewTeam,
  orderedStages: LiveSessionStage[]
): StageProgressBlock[] {
  const isCompleted = team.progressState === "Completed";
  const currentOrder = team.currentStage?.sessionStageOrder ?? 0;

  return orderedStages.map((stage) => {
    let status: StageProgressBlockStatus;
    if (isCompleted || stage.sessionStageOrder < currentOrder) {
      status = "done";
    } else if (stage.sessionStageOrder === currentOrder) {
      status = "current";
    } else {
      status = "pending";
    }

    return { order: stage.sessionStageOrder, name: stage.name, status };
  });
}

function TeamStageProgress({ blocks }: { blocks: StageProgressBlock[] }) {
  if (blocks.length === 0) {
    return null;
  }

  const doneCount = blocks.filter((block) => block.status === "done").length;
  const currentBlock = blocks.find((block) => block.status === "current");
  const label = currentBlock
    ? `Etapa ${currentBlock.order} de ${blocks.length}`
    : doneCount === blocks.length
      ? `Completó las ${blocks.length} etapas`
      : `Sin iniciar · ${blocks.length} etapas`;

  return (
    <div className="stage-progress">
      <div className="stage-progress-track">
        {blocks.map((block) => (
          <span
            className={`stage-progress-block is-${block.status}`}
            key={block.order}
            title={`Etapa #${block.order}: ${block.name}`}
          />
        ))}
      </div>
      <span className="stage-progress-label">{label}</span>
    </div>
  );
}

function isSnapshotRefreshRequired(refreshPolicy: SnapshotRefreshPolicy) {
  return refreshPolicy === 2 || refreshPolicy === "RefreshSnapshot";
}

function syncFromMetadata(metadata: RealtimeEventMetadata, previousSync: LiveSessionOverview["sync"]) {
  return {
    sequenceNumber: metadata.sequenceNumber,
    lastUpdatedUtc: metadata.occurredAtUtc,
    serverTimeUtc: previousSync.serverTimeUtc
  };
}

function sortOverviewTeams(teams: LiveSessionOverviewTeam[]) {
  return [...teams].sort((left, right) => {
    const leftCompleted = left.progressState === "Completed";
    const rightCompleted = right.progressState === "Completed";

    if (leftCompleted !== rightCompleted) {
      return leftCompleted ? 1 : -1;
    }

    return left.teamName.localeCompare(right.teamName);
  });
}

function getSessionStageOperationalStatus(
  sessionStage: LiveSessionStage,
  sessionTeams: LiveSessionOverviewTeam[]
): SessionStageOperationalStatus {
  const completedByAnyTeam = sessionTeams.some(
    (team) =>
      team.progressState === "Completed" ||
      (team.currentStage !== null && team.currentStage.sessionStageOrder > sessionStage.sessionStageOrder)
  );

  return completedByAnyTeam ? "Completed" : "Pending";
}

// Ranking-position badge tint for the synchronized flow-board team cards.
function getFlowRankClass(rank: number | undefined) {
  if (rank === 1) return "flow-rank is-first";
  if (rank === 2) return "flow-rank is-second";
  if (rank === 3) return "flow-rank is-third";
  return "flow-rank";
}

function sortSessionEventLogItems(items: SessionEventLogItem[]) {
  return [...items].sort((left, right) => {
    const leftTime = new Date(left.timestamp).getTime();
    const rightTime = new Date(right.timestamp).getTime();

    if (Number.isNaN(leftTime) || Number.isNaN(rightTime) || leftTime === rightTime) {
      return right.id.localeCompare(left.id);
    }

    return rightTime - leftTime;
  });
}

function upsertSessionEventLogItem(items: SessionEventLogItem[], nextItem: SessionEventLogItem) {
  return sortSessionEventLogItems([nextItem, ...items.filter((item) => item.id !== nextItem.id)]);
}

function createTeamDetailTimeline(
  detail: SessionTeamDetailResponse,
  showTriviaOnly: boolean
): TeamDetailTimelineItem[] {
  const hintItems: TeamDetailTimelineItem[] = showTriviaOnly
    ? []
    : detail.releasedHints.map((hint) => ({
        kind: "hint",
        id: `hint-${hint.releasedHintId}`,
        occurredAtUtc: hint.releasedAtUtc,
        stageName: hint.stageName,
        hint
      }));
  const submissionItems: TeamDetailTimelineItem[] = detail.evidenceSubmissions
    .filter((submission) =>
      showTriviaOnly ? stringEqualsIgnoreCase(submission.gameType, "Trivia") : true
    )
    .map((submission) => ({
      kind: "submission",
      id: `submission-${submission.id}`,
      occurredAtUtc: submission.submittedAtUtc,
      stageName: submission.stageName,
      submission
    }));

  return [...hintItems, ...submissionItems].sort((left, right) => {
    const leftTime = new Date(left.occurredAtUtc).getTime();
    const rightTime = new Date(right.occurredAtUtc).getTime();

    if (Number.isNaN(leftTime) || Number.isNaN(rightTime) || leftTime === rightTime) {
      return right.id.localeCompare(left.id);
    }

    return rightTime - leftTime;
  });
}

function stringEqualsIgnoreCase(left: string, right: string) {
  return left.localeCompare(right, undefined, { sensitivity: "accent" }) === 0;
}

type ScoringRankingWidgetProps = {
  rankingItems: RankingItem[];
  teams: LiveSessionOverviewTeam[];
  isLoading: boolean;
  error: string | null;
  connectionState: RealtimeConnectionState;
  onRefresh: () => void;
};

function ScoringRankingWidget({
  rankingItems,
  teams,
  isLoading,
  error,
  connectionState,
  onRefresh
}: ScoringRankingWidgetProps) {
  return (
    <section className="card">
      <div className="card-header">
        <div className="stack-sm">
          <span className="eyebrow">Puntuación y Auditoría</span>
          <h4>Clasificación</h4>
        </div>
        <div className="card-header-actions">
          <span className={getConnectionIndicatorClass(connectionState)}>
            <span className={getConnectionDotClass(connectionState)} />
            Puntuación: {connectionState.label}
          </span>
          <button className="btn btn-ghost btn-sm" disabled={isLoading} onClick={onRefresh} type="button">
            {isLoading ? "Sincronizando..." : "Actualizar"}
          </button>
        </div>
      </div>

      {error ? (
        <div className="error-banner">
          <strong>Clasificación no disponible.</strong>
          <p>{error}</p>
        </div>
      ) : null}

      {!error && rankingItems.length > 0 ? (
        <div className="table-wrap">
          <table className="ranking-table">
            <thead>
              <tr>
                <th>Puesto</th>
                <th>Equipo</th>
                <th>Puntos</th>
                <th>Tiempo</th>
              </tr>
            </thead>
            <tbody>
              {rankingItems.map((entry, index) => {
                const previousEntry = rankingItems[index - 1];
                const isSharedRank = previousEntry?.rank === entry.rank;

                return (
                  <tr key={entry.sessionTeamId}>
                    <td>
                      <span className="rank-number">#{entry.rank}</span>
                    </td>
                    <td>
                      <strong>{findRankingTeamName(entry.sessionTeamId, teams)}</strong>
                      {isSharedRank ? <p className="text-muted text-xs">Empate conservado</p> : null}
                    </td>
                    <td>{entry.visibleScore} pts</td>
                    <td className="mono">{formatResolutionTime(entry.resolutionTime)}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      ) : null}

      {!error && !isLoading && rankingItems.length === 0 ? (
        <div className="empty-state">
          <strong>Aún no hay entradas de puntuación.</strong>
          <p>La clasificación aparecerá cuando Puntuación y Auditoría registre crédito de etapa.</p>
        </div>
      ) : null}
    </section>
  );
}

type SessionEventTimelineProps = {
  eventLogItems: SessionEventLogItem[];
  isLoading: boolean;
  error: string | null;
  connectionState: RealtimeConnectionState;
  onRefresh: () => void;
};

function SessionEventTimeline({
  eventLogItems,
  isLoading,
  error,
  connectionState,
  onRefresh
}: SessionEventTimelineProps) {
  return (
    <section className="card">
      <div className="card-header">
        <div className="stack-sm">
          <span className="eyebrow">Bitácora de auditoría</span>
          <h4>Línea de tiempo</h4>
        </div>
        <div className="card-header-actions">
          <span className={getConnectionIndicatorClass(connectionState)}>
            <span className={getConnectionDotClass(connectionState)} />
            Eventos: {connectionState.label}
          </span>
          <button className="btn btn-ghost btn-sm" disabled={isLoading} onClick={onRefresh} type="button">
            {isLoading ? "Sincronizando..." : "Actualizar"}
          </button>
        </div>
      </div>

      {error ? (
        <div className="error-banner">
          <strong>Línea de tiempo no disponible.</strong>
          <p>{error}</p>
        </div>
      ) : null}

      {!error && eventLogItems.length > 0 ? (
        <div className="timeline">
          {eventLogItems.map((eventLog) => (
            <div className="timeline-item" key={eventLog.id}>
              <div className="timeline-dot" />
              <div className="timeline-content">
                <div className="row-sm">
                  <span className="badge badge-blue">{eventLog.eventType}</span>
                  <span className="timeline-time">
                    <time dateTime={eventLog.timestamp}>{formatRelativeTimestamp(eventLog.timestamp)}</time>
                  </span>
                </div>
                <p>{eventLog.description}</p>
                <span className="text-muted text-xs">{formatTimestamp(eventLog.timestamp)}</span>
              </div>
            </div>
          ))}
        </div>
      ) : null}

      {!error && !isLoading && eventLogItems.length === 0 ? (
        <div className="empty-state">
          <strong>Sin eventos auditables aún.</strong>
          <p>La línea de tiempo se mantendrá lista mientras el registro de eventos de sesión espera eventos de puntuación u operador.</p>
        </div>
      ) : null}
    </section>
  );
}

type SessionTeamDetailPanelProps = {
  detail: SessionTeamDetailResponse | null;
  error: string | null;
  isLoading: boolean;
  inactivityThresholdMinutes: number;
  overridePendingSubmissionId: string | null;
  onInactivityThresholdChange: (nextValue: number) => void;
  onRefresh: () => void;
  onOverrideSubmission: (submissionId: string, reason: string) => void;
  onClose: () => void;
};

function SessionTeamDetailPanel({
  detail,
  error,
  isLoading,
  inactivityThresholdMinutes,
  overridePendingSubmissionId,
  onInactivityThresholdChange,
  onRefresh,
  onOverrideSubmission,
  onClose
}: SessionTeamDetailPanelProps) {
  const [nowMs, setNowMs] = useState(() => Date.now());
  const [showTriviaOnly, setShowTriviaOnly] = useState(false);
  const [overrideSubmissionId, setOverrideSubmissionId] = useState<string | null>(null);
  const [overrideReason, setOverrideReason] = useState("");

  useEffect(() => {
    const intervalId = window.setInterval(() => setNowMs(Date.now()), 1000);
    return () => window.clearInterval(intervalId);
  }, []);

  const timelineItems = useMemo(
    () => (detail ? createTeamDetailTimeline(detail, showTriviaOnly) : []),
    [detail, showTriviaOnly]
  );

  if (!detail) {
    return (
      <aside className="drawer">
        <div className="drawer-header">
          <span className="eyebrow">Detalles del equipo de sesión</span>
          <button className="btn btn-ghost btn-sm" onClick={onClose} type="button">
            Cerrar
          </button>
        </div>
        <div className="empty-state">
          <strong>Seleccione un equipo de la sesión.</strong>
          <p>Los detalles del operador se muestran aquí con la actividad, pistas, envíos y estado de inactividad.</p>
        </div>
        {error ? (
          <div className="error-banner">
            <strong>Detalles del equipo no disponibles.</strong>
            <p>{error}</p>
          </div>
        ) : null}
      </aside>
    );
  }

  return (
    <aside className="drawer">
      <div className="drawer-header">
        <div className="stack-sm">
          <span className="eyebrow">Detalles del equipo de sesión</span>
          <h4>{detail.teamName}</h4>
          <span className="text-muted text-sm">{detail.participantCount} participante(s)</span>
        </div>
        <div className="row-sm">
          <span className={detail.isInactive ? "badge badge-red" : "badge badge-green"}>
            {detail.isInactive ? "Inactivo" : "Activo"}
          </span>
          <button className="btn btn-ghost btn-sm" onClick={onClose} type="button">
            Cerrar
          </button>
        </div>
      </div>

      <div className="drawer-body">
        {detail.isInactive ? (
          <div className="error-banner">
            <strong>Sin envíos de evidencia recientes.</strong>
            <p>La última actividad es más antigua que el límite configurado.</p>
          </div>
        ) : null}

        <div className="row-sm">
          <div className="form-group">
            <label className="form-label">Límite de inactividad</label>
            <input
              className="form-input"
              min={1}
              onChange={(event) => onInactivityThresholdChange(Number(event.target.value))}
              type="number"
              value={inactivityThresholdMinutes}
            />
          </div>
          <button className="btn btn-ghost btn-sm" disabled={isLoading} onClick={onRefresh} type="button">
            {isLoading ? "Actualizando" : "Actualizar"}
          </button>
        </div>

        {error ? (
          <div className="error-banner">
            <strong>Detalles del equipo desactualizados.</strong>
            <p>{error}</p>
          </div>
        ) : null}

        <div className="detail-panel">
          <div className="detail-row">
            <span className="detail-label">Etapa actual</span>
            <span className="detail-value">{getStageLabel(detail.currentStage)}</span>
          </div>
          <div className="detail-row">
            <span className="detail-label">Tiempo en la etapa</span>
            <span className="detail-value mono">{formatElapsedDuration(detail.currentStageStartedAtUtc, nowMs)}</span>
          </div>
          <div className="detail-row">
            <span className="detail-label">Progreso</span>
            <span className="detail-value">{translateProgressState(detail.progressState)}</span>
          </div>
        </div>

        <section className="card-section">
          <div className="card-header">
            <div className="stack-sm">
              <span className="eyebrow">Actividad</span>
              <h5>Pistas y Envíos de Evidencia</h5>
            </div>
            <label className="checkbox-label">
              <input
                checked={showTriviaOnly}
                onChange={(event) => setShowTriviaOnly(event.target.checked)}
                type="checkbox"
              />
              <span>Mostrar Solo Respuestas Trivia</span>
            </label>
          </div>

          {timelineItems.length > 0 ? (
            <div className="timeline">
              {timelineItems.map((item) => (
                <div className="timeline-item" key={item.id}>
                  <div className="timeline-dot" />
                  <div className="timeline-content">
                    <div className="row-sm">
                      <span className={item.kind === "hint" ? "badge badge-blue" : "badge badge-amber"}>
                        {item.kind === "hint" ? "Pista" : translateGameType(item.submission.gameType)}
                      </span>
                      <span className="timeline-time">
                        <time dateTime={item.occurredAtUtc}>{formatRelativeTimestamp(item.occurredAtUtc)}</time>
                      </span>
                    </div>

                    {item.kind === "hint" ? (
                      <>
                        <p>{item.hint.content}</p>
                        <span className="text-muted text-xs">
                          {item.stageName} Â· {item.hint.unlockReason} Â· {formatTimestamp(item.hint.releasedAtUtc)}
                        </span>
                      </>
                    ) : (
                      <>
                        <div className="row-between">
                          <strong>{item.submission.submittedText ?? item.submission.submittedHash ?? "Evidencia enviada"}</strong>
                          <span
                            className={
                              item.submission.validationOutcome === "Accepted"
                                ? "badge badge-green"
                                : "badge badge-red"
                            }
                          >
                            {translateValidationOutcome(item.submission.validationOutcome)}
                          </span>
                        </div>
                        <span className="text-muted text-xs">
                          {item.stageName} Â· {translateDifficulty(item.submission.difficulty)} Â· {formatTimestamp(item.submission.submittedAtUtc)}
                        </span>
                        {item.submission.failureReason ? <p className="text-muted text-xs">{item.submission.failureReason}</p> : null}

                        {item.submission.isTriviaCorrectionEligible ? (
                          <div className="stack-sm">
                            {overrideSubmissionId === item.submission.id ? (
                              <form
                                className="stack-sm"
                                onSubmit={(event) => {
                                  event.preventDefault();
                                  if (!overrideReason.trim()) {
                                    return;
                                  }

                                  onOverrideSubmission(item.submission.id, overrideReason.trim());
                                  setOverrideSubmissionId(null);
                                  setOverrideReason("");
                                }}
                                role="dialog"
                                aria-label="Motivo de anulación de validación"
                              >
                                <div className="form-group">
                                  <label className="form-label">Motivo del operador</label>
                                  <textarea
                                    className="form-textarea"
                                    maxLength={500}
                                    onChange={(event) => setOverrideReason(event.target.value)}
                                    required
                                    value={overrideReason}
                                  />
                                </div>
                                <div className="row-sm">
                                  <button
                                    className="btn btn-success btn-sm"
                                    disabled={overridePendingSubmissionId === item.submission.id}
                                    type="submit"
                                  >
                                    {overridePendingSubmissionId === item.submission.id ? "Enviando" : "Forzar aceptación"}
                                  </button>
                                  <button
                                    className="btn btn-ghost btn-sm"
                                    onClick={() => {
                                      setOverrideSubmissionId(null);
                                      setOverrideReason("");
                                    }}
                                    type="button"
                                  >
                                    Cancelar
                                  </button>
                                </div>
                              </form>
                            ) : (
                              <button
                                className="btn btn-ghost btn-sm"
                                disabled={overridePendingSubmissionId !== null}
                                onClick={() => {
                                  setOverrideSubmissionId(item.submission.id);
                                  setOverrideReason("");
                                }}
                                type="button"
                              >
                                Forzar Aceptación (Override)
                              </button>
                            )}
                          </div>
                        ) : null}
                      </>
                    )}
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <div className="empty-state">
              <strong>Sin actividad del equipo coincidente.</strong>
              <p>Limpie el filtro o espere a que lleguen pistas y envíos de evidencia.</p>
            </div>
          )}
        </section>
      </div>
    </aside>
  );
}

export function LiveSessionOverviewDashboard({
  liveSession,
  overview,
  connectionState,
  scoringConnectionState,
  isLoadingOverview,
  isLoadingEventLog,
  lifecycleActions,
  lifecycleActionPending,
  rankingItems,
  eventLogItems,
  isLoadingRanking,
  rankingError,
  eventLogError,
  selectedSessionTeamId,
  sessionTeamDetail,
  sessionTeamDetailError,
  inactivityThresholdMinutes,
  isLoadingSessionTeamDetail,
  overridePendingSubmissionId,
  onSelectSessionTeam,
  onRefreshSessionTeamDetail,
  onInactivityThresholdChange,
  onOverrideSubmission,
  onLifecycleAction,
  onRefreshEventLog,
  onRefreshOverview,
  onRefreshRanking
}: LiveSessionOverviewDashboardProps) {
  const sessionState = overview?.sessionState ?? liveSession.state;
  const teams = overview ? sortOverviewTeams(overview.sessionTeams) : [];
  const activeTeamCount = teams.filter((team) => team.progressState !== "Completed").length;
  const remainingSeconds = overview?.remainingSeconds;
  const lastSyncLabel = formatShortTimestamp(overview?.sync.lastUpdatedUtc);
  const overviewIsStale = connectionState.kind !== "connected";
  const orderedStages = [...(liveSession.sessionStageFlow ?? [])].sort(
    (a, b) => a.sessionStageOrder - b.sessionStageOrder
  );

  return (
    <div className="stack-lg">
      <section className="card">
        <div className="card-header">
          <div className="stack-sm">
            <span className="eyebrow">Operación en vivo</span>
            <h3>{overview?.name ?? liveSession.name}</h3>
            <span className="text-muted">{overview?.missionName ?? liveSession.missionName}</span>
          </div>
          <div className="card-header-actions">
            <span className={getConnectionIndicatorClass(connectionState)}>
              <span className={getConnectionDotClass(connectionState)} />
              Sincronización en tiempo real: {connectionState.label}
            </span>
            <button className="btn btn-ghost" disabled={isLoadingOverview} onClick={onRefreshOverview} type="button">
              {isLoadingOverview ? "Actualizando..." : "Actualizar vista"}
            </button>
          </div>
        </div>
      </section>

      <div className="split-layout-wide">
        <div className="card card-compact">
          <strong className="text-sm">Estado de la Sesión</strong>
          <p className="text-lg">{translateSessionState(sessionState)}</p>
        </div>
        <div className="card card-compact">
          <strong className="text-sm">Tiempo restante</strong>
          <p className="text-lg mono">{formatRemainingSeconds(remainingSeconds)}</p>
        </div>
        <div className="card card-compact">
          <strong className="text-sm">Equipos activos</strong>
          <p className="text-lg">{overview ? activeTeamCount : liveSession.registeredSessionTeamCount}</p>
        </div>
        <div className="card card-compact">
          <strong className="text-sm">Última sincronización</strong>
          <p className="text-lg mono">{lastSyncLabel}</p>
        </div>
      </div>

      <section className="card">
        <div className="card-header">
          <div className="stack-sm">
            <span className="eyebrow">Ciclo de vida</span>
            <h4>Control de sesión</h4>
          </div>
          <span className={getSessionStateBadgeClass(sessionState)}>{translateSessionState(sessionState)}</span>
        </div>

        <div className="row-sm row-wrap">
          {lifecycleActions.map((action) => {
            let btnClass = "btn btn-ghost";
            if (action.action === "start") btnClass = "btn btn-success";
            if (action.action === "resume") btnClass = "btn btn-success";
            if (action.action === "pause") btnClass = "btn btn-ghost";
            if (action.action === "finalize") btnClass = "btn btn-primary";
            if (action.action === "cancel") btnClass = "btn btn-danger";

            return (
              <button
                className={btnClass}
                disabled={action.disabled || lifecycleActionPending !== null}
                key={action.action}
                onClick={() => onLifecycleAction(action.action)}
                style={action.action === "cancel" ? { marginLeft: "auto" } : undefined}
                type="button"
              >
                {lifecycleActionPending === action.action ? "Actualizando..." : action.label}
              </button>
            );
          })}
        </div>

        {overviewIsStale ? (
          <p className="text-muted text-sm">
            La conexión en tiempo real no está completamente activa. La captura permanece visible y la actualización manual está disponible.
          </p>
        ) : (
          <p className="text-muted text-sm">{connectionState.detail}</p>
        )}
      </section>

      <section className="card">
        <div className="card-header">
          <div className="stack-sm">
            <span className="eyebrow">Equipos de la Sesión</span>
            <h4>Progreso operativo</h4>
          </div>
          {isLoadingOverview ? <span className="badge badge-amber">Sincronizando</span> : null}
        </div>

        {teams.length > 0 ? (
          <div className={selectedSessionTeamId ? "split-layout" : ""}>
            <div className="stack-sm">
              {teams.map((team) => {
                const isSelected = team.sessionTeamId === selectedSessionTeamId;

                return (
                  <button
                    className={isSelected ? "card card-compact clickable is-selected" : "card card-compact clickable"}
                    key={team.sessionTeamId}
                    onClick={() => onSelectSessionTeam(isSelected ? null : team.sessionTeamId)}
                    type="button"
                  >
                    <div className="row-between">
                      <div className="stack-sm">
                        <strong>{team.teamName}</strong>
                        <span className="text-muted text-xs">{team.participantCount} participante(s)</span>
                      </div>
                      <span className={getProgressBadgeClass(team.progressState)}>{translateProgressState(team.progressState)}</span>
                    </div>

                    <TeamStageProgress blocks={getTeamStageProgress(team, orderedStages)} />

                    <div className="detail-panel">
                      <div className="detail-row">
                        <span className="detail-label">Etapa actual</span>
                        <span className="detail-value">{getStageLabel(team.currentStage)}</span>
                      </div>
                      <div className="detail-row">
                        <span className="detail-label">Dificultad</span>
                        <span className="detail-value">{team.currentStage ? translateDifficulty(team.currentStage.difficulty) : "N/A"}</span>
                      </div>
                      <div className="detail-row">
                        <span className="detail-label">Tipo de juego</span>
                        <span className="detail-value">{team.currentStage ? translateGameType(team.currentStage.gameType) : "N/A"}</span>
                      </div>
                      <div className="detail-row">
                        <span className="detail-label">Pistas visibles</span>
                        <span className="detail-value">{getReleasedHintsForTeam(team).length}</span>
                      </div>
                    </div>
                  </button>
                );
              })}
            </div>

            {selectedSessionTeamId ? (
              <SessionTeamDetailPanel
                detail={sessionTeamDetail}
                error={sessionTeamDetailError}
                inactivityThresholdMinutes={inactivityThresholdMinutes}
                isLoading={isLoadingSessionTeamDetail}
                key={selectedSessionTeamId ?? "empty-session-team-detail"}
                onClose={() => onSelectSessionTeam(null)}
                onInactivityThresholdChange={onInactivityThresholdChange}
                onOverrideSubmission={onOverrideSubmission}
                onRefresh={onRefreshSessionTeamDetail}
                overridePendingSubmissionId={overridePendingSubmissionId}
              />
            ) : null}
          </div>
        ) : (
          <div className="empty-state">
            <strong>No se cargaron equipos de sesión.</strong>
            <p>Use la actualización manual si el registro de participantes cambió mientras se reconectaba en tiempo real.</p>
          </div>
        )}
      </section>

      <div className="split-layout">
        <ScoringRankingWidget
          connectionState={scoringConnectionState}
          error={rankingError}
          isLoading={isLoadingRanking}
          onRefresh={onRefreshRanking}
          rankingItems={rankingItems}
          teams={teams}
        />
        <SessionEventTimeline
          connectionState={scoringConnectionState}
          error={eventLogError}
          eventLogItems={eventLogItems}
          isLoading={isLoadingEventLog}
          onRefresh={onRefreshEventLog}
        />
      </div>
    </div>
  );
}

export function LiveSessionsWorkspace({ accessToken }: LiveSessionsWorkspaceProps) {
  const [missions, setMissions] = useState<EligibleMissionSummary[]>([]);
  const [selectedMissionId, setSelectedMissionId] = useState<string | null>(null);
  const [selectedMission, setSelectedMission] = useState<EligibleMissionDetail | null>(null);
  const [selectedMissionStageIds, setSelectedMissionStageIds] = useState<string[]>([]);
  const [liveSessions, setLiveSessions] = useState<LiveSession[]>([]);
  const [selectedLiveSessionId, setSelectedLiveSessionId] = useState<string | null>(null);
  const [selectedLiveSessionOverview, setSelectedLiveSessionOverview] = useState<LiveSessionOverview | null>(null);
  const [selectedLiveSessionRanking, setSelectedLiveSessionRanking] = useState<RankingPayload | null>(null);
  const [selectedLiveSessionEventLog, setSelectedLiveSessionEventLog] = useState<SessionEventLogItem[]>([]);
  const [selectedSessionTeamId, setSelectedSessionTeamId] = useState<string | null>(null);
  const [selectedSessionTeamDetail, setSelectedSessionTeamDetail] = useState<SessionTeamDetailResponse | null>(null);
  const [inactivityThresholdMinutes] = useState(10);
  const [draft, setDraft] = useState<LiveSessionDraft>(createEmptyDraft);
  const [penaltyDraft, setPenaltyDraft] = useState<PenaltyDraft>(createEmptyPenaltyDraft);
  const [operationalHintDraft, setOperationalHintDraft] = useState<OperationalHintDraft>(
    createEmptyOperationalHintDraft
  );
  const [isLoadingMissions, setIsLoadingMissions] = useState(true);
  const [isLoadingMissionDetail, setIsLoadingMissionDetail] = useState(false);
  const [isLoadingLiveSessions, setIsLoadingLiveSessions] = useState(true);
  const [, setIsLoadingLiveSessionOverview] = useState(false);
  const [, setIsLoadingSessionTeamDetail] = useState(false);
  const [, setIsLoadingRanking] = useState(false);
  const [, setIsLoadingEventLog] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isSubmittingPenalty, setIsSubmittingPenalty] = useState(false);
  const [isSubmittingHint, setIsSubmittingHint] = useState(false);
  const [overridePendingSubmissionId, setOverridePendingSubmissionId] = useState<string | null>(null);
  const [deactivatingStageId, setDeactivatingStageId] = useState<string | null>(null);
  const [lifecycleActionPending, setLifecycleActionPending] = useState<LiveSessionLifecycleAction | null>(null);
  const [enrollmentActionPending, setEnrollmentActionPending] = useState<EnrollmentAction | null>(null);
  const [newTeamName, setNewTeamName] = useState("");
  const [isCreatingTeam, setIsCreatingTeam] = useState(false);
  const [sessionRealtimeConnection, setSessionRealtimeConnection] = useState<RealtimeConnectionState>({
    kind: "disconnected",
    label: "Desconectado",
    detail: "SignalR esperando la selección de una LiveSession."
  });
  const [, setScoringRealtimeConnection] = useState<RealtimeConnectionState>({
    kind: "disconnected",
    label: "Desconectado",
    detail: "SignalR esperando la selección de una LiveSession."
  });
  const [, setRankingError] = useState<string | null>(null);
  const [, setEventLogError] = useState<string | null>(null);
  const [, setSessionTeamDetailError] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<string | null>(null);

  const eligibleMissionsUrl = useMemo(
    () => `${getClientConfig().edgeProxyPublicBaseUrl}/mission-management/api/mission-management/missions/eligible-for-live-session`,
    []
  );
  const SessionManagementUrl = useMemo(
    () => `${getClientConfig().edgeProxyPublicBaseUrl}/session-management/api/session-management`,
    []
  );
  const liveSessionsUrl = useMemo(
    () => `${SessionManagementUrl}/live-sessions`,
    [SessionManagementUrl]
  );
  const sessionHubUrl = useMemo(() => getClientConfig().sessionHubUrl, []);
  const scoringAuditSessionsUrl = useMemo(
    () => `${getClientConfig().edgeProxyPublicBaseUrl}/scoring-monitoring/api/scoring-monitoring/sessions`,
    []
  );
  const scoringAuditHubUrl = useMemo(
    () => getClientConfig().scoringHubUrl,
    []
  );

  const selectedLiveSession =
    liveSessions.find((liveSession) => liveSession.id === selectedLiveSessionId) ?? null;
  const lifecycleActions = useMemo(() => {
    if (!selectedLiveSession) {
      return [];
    }

    const state = selectedLiveSession.state;

    // `available` = the action makes sense in the current state (shown to the operator).
    // `disabled`  = available but blocked by a precondition (e.g. Start needs a team).
    const actions: LifecycleActionViewModel[] = [
      {
        action: "start",
        label: "Iniciar sesión",
        requiresConfirmation: true,
        available: state === "Scheduled",
        disabled: selectedLiveSession.registeredSessionTeamCount === 0
      },
      {
        action: "resume",
        label: "Reanudar sesión",
        requiresConfirmation: false,
        available: state === "Paused",
        disabled: false
      },
      {
        action: "pause",
        label: "Pausar sesión",
        requiresConfirmation: true,
        available: state === "Active",
        disabled: false
      },
      {
        action: "finalize",
        label: "Finalizar sesión",
        requiresConfirmation: true,
        available: ["Active", "Paused"].includes(state),
        disabled: false
      },
      {
        action: "cancel",
        label: "Cancelar sesión",
        requiresConfirmation: true,
        available: ["Scheduled", "Active", "Paused"].includes(state),
        disabled: false
      }
    ];

    return actions.filter((action) => action.available);
  }, [selectedLiveSession]);

  const missionSummary = useMemo(() => {
    const totalActiveStages = missions.reduce((total, mission) => total + mission.activeMissionStageCount, 0);

    return {
      totalMissions: missions.length,
      totalActiveStages,
      totalLiveSessions: liveSessions.length
    };
  }, [liveSessions.length, missions]);

  const draftPreview = useMemo(() => {
    if (!selectedMission) {
      return [];
    }

    return selectedMissionStageIds
      .map((missionStageId) => (selectedMission.missionStages || []).find((missionStage) => missionStage.id === missionStageId))
      .filter((missionStage): missionStage is EligibleMissionStage => missionStage !== undefined)
      .map((missionStage, index) => ({
        ...missionStage,
        draftSessionStageOrder: index + 1
      }));
  }, [selectedMission, selectedMissionStageIds]);

  const selectedLiveSessionStages = selectedLiveSession?.sessionStageFlow ?? [];
  const isSelectedLiveSessionOverviewCurrent =
    selectedLiveSession !== null && selectedLiveSessionOverview?.liveSessionId === selectedLiveSession.id;
  const selectedLiveSessionOverviewTeams = useMemo(
    () => (isSelectedLiveSessionOverviewCurrent && selectedLiveSessionOverview?.sessionTeams ? selectedLiveSessionOverview.sessionTeams : []),
    [isSelectedLiveSessionOverviewCurrent, selectedLiveSessionOverview]
  );
  const selectedRankingItems =
    selectedLiveSessionRanking && selectedLiveSessionRanking.liveSessionId === selectedLiveSession?.id
      ? selectedLiveSessionRanking.items || []
      : [];
  const selectedEventLogItems = selectedLiveSession
    ? selectedLiveSessionEventLog.filter((eventLog) => eventLog.liveSessionId === selectedLiveSession.id) || []
    : [];
  const operationalHintStageId =
    operationalHintDraft.missionStageId || selectedLiveSessionStages[0]?.missionStageId || "";
  const effectivePenaltySessionTeamId = useMemo(() => {
    if (
      penaltyDraft.sessionTeamId &&
      selectedLiveSessionOverviewTeams.some((team) => team.sessionTeamId === penaltyDraft.sessionTeamId)
    ) {
      return penaltyDraft.sessionTeamId;
    }

    return selectedLiveSessionOverviewTeams[0]?.sessionTeamId ?? "";
  }, [penaltyDraft.sessionTeamId, selectedLiveSessionOverviewTeams]);

  const loadMissions = useCallback(
    async (preferredMissionId?: string) => {
      setErrorMessage(null);

      try {
        const response = await fetch(eligibleMissionsUrl, {
          headers: createAuthorizedHeaders(accessToken)
        });

        if (!response.ok) {
          throw new Error(await readFailureDetail(response));
        }

        const payload = (await response.json()) as EligibleMissionSummaryPayload[];
        const mapped = payload.map(toEligibleMissionSummary);
        setMissions(mapped);

        const nextMissionId =
          preferredMissionId && mapped.some((mission) => mission.id === preferredMissionId)
            ? preferredMissionId
            : mapped[0]?.id ?? null;

        setSelectedMissionId(nextMissionId);
      } catch (error) {
        setErrorMessage(error instanceof Error ? error.message : "No se pudieron cargar las Misiones elegibles.");
      } finally {
        setIsLoadingMissions(false);
      }
    },
    [accessToken, eligibleMissionsUrl]
  );

  const loadLiveSessions = useCallback(
    async (preferredLiveSessionId?: string) => {
      setErrorMessage(null);

      try {
        const response = await fetch(liveSessionsUrl, {
          headers: createAuthorizedHeaders(accessToken)
        });

        if (!response.ok) {
          throw new Error(await readFailureDetail(response));
        }

        const payload = (await response.json()) as LiveSession[];
        setLiveSessions(payload);

        const nextLiveSessionId =
          preferredLiveSessionId && payload.some((liveSession) => liveSession.id === preferredLiveSessionId)
            ? preferredLiveSessionId
            : null;

        setSelectedLiveSessionId(nextLiveSessionId);
      } catch (error) {
        setErrorMessage(error instanceof Error ? error.message : "No se pudieron cargar las LiveSessions.");
      } finally {
        setIsLoadingLiveSessions(false);
      }
    },
    [accessToken, liveSessionsUrl]
  );

  const loadLiveSessionOverview = useCallback(
    async (liveSessionId: string) => {
      setIsLoadingLiveSessionOverview(true);
      setErrorMessage(null);

      try {
        const response = await fetch(`${liveSessionsUrl}/${liveSessionId}/overview`, {
          headers: createAuthorizedHeaders(accessToken)
        });

        if (!response.ok) {
          throw new Error(await readFailureDetail(response));
        }

        const payload = (await response.json()) as LiveSessionOverview;
        setSelectedLiveSessionOverview(payload);
      } catch (error) {
        setErrorMessage(error instanceof Error ? error.message : "No se pudo cargar la vista general de la LiveSession.");
        setSelectedLiveSessionOverview(null);
      } finally {
        setIsLoadingLiveSessionOverview(false);
      }
    },
    [accessToken, liveSessionsUrl]
  );

  const loadSessionTeamDetail = useCallback(
    async (liveSessionId: string, sessionTeamId: string, thresholdMinutes: number) => {
      const normalizedThreshold = normalizeInactivityThresholdMinutes(thresholdMinutes);

      setIsLoadingSessionTeamDetail(true);
      setSessionTeamDetailError(null);

      try {
        const response = await fetch(
          `${liveSessionsUrl}/${liveSessionId}/teams/${sessionTeamId}/detail?inactivityThresholdMinutes=${normalizedThreshold}`,
          {
            headers: createAuthorizedHeaders(accessToken)
          }
        );

        if (!response.ok) {
          throw new Error(await readFailureDetail(response));
        }

        const payload = (await response.json()) as SessionTeamDetailResponse;
        setSelectedSessionTeamDetail(payload);
      } catch (error) {
        setSessionTeamDetailError(error instanceof Error ? error.message : "No se pudieron cargar los detalles del equipo de sesión.");
        setSelectedSessionTeamDetail(null);
      } finally {
        setIsLoadingSessionTeamDetail(false);
      }
    },
    [accessToken, liveSessionsUrl]
  );

  const loadLiveSessionRanking = useCallback(
    async (liveSessionId: string) => {
      setIsLoadingRanking(true);
      setRankingError(null);

      try {
        const response = await fetch(`${scoringAuditSessionsUrl}/${liveSessionId}/ranking`, {
          headers: createAuthorizedHeaders(accessToken)
        });

        if (response.status === 404) {
          setSelectedLiveSessionRanking({
            liveSessionId,
            generatedAtUtc: new Date().toISOString(),
            items: []
          });
          return;
        }

        if (!response.ok) {
          throw new Error(await readFailureDetail(response));
        }

        const payload = (await response.json()) as RankingPayload;
        setSelectedLiveSessionRanking(payload);
      } catch (error) {
        setSelectedLiveSessionRanking(null);
        setRankingError(error instanceof Error ? error.message : "No se pudo cargar el Ranking.");
      } finally {
        setIsLoadingRanking(false);
      }
    },
    [accessToken, scoringAuditSessionsUrl]
  );

  const loadLiveSessionEventLog = useCallback(
    async (liveSessionId: string) => {
      setIsLoadingEventLog(true);
      setEventLogError(null);

      try {
        const response = await fetch(`${scoringAuditSessionsUrl}/${liveSessionId}/event-log`, {
          headers: createAuthorizedHeaders(accessToken)
        });

        if (response.status === 404) {
          setSelectedLiveSessionEventLog([]);
          return;
        }

        if (!response.ok) {
          throw new Error(await readFailureDetail(response));
        }

        const payload = (await response.json()) as SessionEventLogItem[];
        setSelectedLiveSessionEventLog(sortSessionEventLogItems(payload));
      } catch (error) {
        setSelectedLiveSessionEventLog([]);
        setEventLogError(error instanceof Error ? error.message : "No se pudo cargar el registro de eventos de la sesión.");
      } finally {
        setIsLoadingEventLog(false);
      }
    },
    [accessToken, scoringAuditSessionsUrl]
  );

  const loadMissionDetail = useCallback(
    async (missionId: string) => {
      setIsLoadingMissionDetail(true);
      setErrorMessage(null);

      try {
        const response = await fetch(`${eligibleMissionsUrl}/${missionId}`, {
          headers: createAuthorizedHeaders(accessToken)
        });

        if (!response.ok) {
          throw new Error(await readFailureDetail(response));
        }

        const payload = (await response.json()) as EligibleMissionDetailPayload;
        const mission = toEligibleMissionDetail(payload);
        setSelectedMission(mission);
        setSelectedMissionStageIds(mission.missionStages.map((missionStage) => missionStage.id));
        setDraft((current) => ({
          name: current.name.trim() ? current.name : `${mission.name} / Ejecución programada`,
          scheduledStartAtLocal: current.scheduledStartAtLocal
        }));
      } catch (error) {
        setErrorMessage(error instanceof Error ? error.message : "No se pudieron cargar los detalles de la misión elegible.");
      } finally {
        setIsLoadingMissionDetail(false);
      }
    },
    [accessToken, eligibleMissionsUrl]
  );

  const refreshSelectedOverview = useCallback(() => {
    if (!selectedLiveSessionId) {
      return;
    }

    setIsLoadingLiveSessions(true);
    void loadLiveSessions(selectedLiveSessionId);
    void loadLiveSessionOverview(selectedLiveSessionId);
  }, [loadLiveSessionOverview, loadLiveSessions, selectedLiveSessionId]);

  function handleSelectSessionTeam(sessionTeamId: string | null) {
    setSelectedSessionTeamId(sessionTeamId);
    setSelectedSessionTeamDetail(null);
    setSessionTeamDetailError(null);
  }

  const applySessionStateChanged = useCallback(
    (payload: SessionStateChangedPayload) => {
      if (payload.metadata.liveSessionId !== selectedLiveSessionId) {
        return;
      }

      setLiveSessions((current) =>
        current.map((liveSession) =>
          liveSession.id === payload.metadata.liveSessionId
            ? { ...liveSession, state: payload.currentState }
            : liveSession
        )
      );

      setSelectedLiveSessionOverview((current) => {
        if (!current || current.liveSessionId !== payload.metadata.liveSessionId) {
          return current;
        }

        return {
          ...current,
          sessionState: payload.currentState,
          remainingSeconds: payload.remainingSeconds ?? current.remainingSeconds,
          sync: syncFromMetadata(payload.metadata, current.sync)
        };
      });

      if (isSnapshotRefreshRequired(payload.metadata.refreshPolicy)) {
        refreshSelectedOverview();
      }
    },
    [refreshSelectedOverview, selectedLiveSessionId]
  );

  const applyTeamProgressChanged = useCallback(
    (payload: TeamProgressChangedPayload) => {
      if (payload.metadata.liveSessionId !== selectedLiveSessionId) {
        return;
      }

      setSelectedLiveSessionOverview((current) => {
        if (!current || current.liveSessionId !== payload.metadata.liveSessionId) {
          return current;
        }

        const hasTeam = current.sessionTeams.some((team) => team.sessionTeamId === payload.sessionTeamId);
        if (!hasTeam) {
          return current;
        }

        return {
          ...current,
          sessionTeams: current.sessionTeams.map((team) =>
            team.sessionTeamId === payload.sessionTeamId
              ? {
                  ...team,
                  currentStage: payload.currentStage ?? null,
                  progressState: payload.progressState
                }
              : team
          ),
          sync: syncFromMetadata(payload.metadata, current.sync)
        };
      });

      if (isSnapshotRefreshRequired(payload.metadata.refreshPolicy)) {
        refreshSelectedOverview();
      }
    },
    [refreshSelectedOverview, selectedLiveSessionId]
  );

  useEffect(() => {
    let active = true;

    if (!selectedLiveSessionId || !accessToken.trim() || !sessionHubUrl.trim()) {
      queueMicrotask(() => {
        if (active) {
          setSessionRealtimeConnection({
            kind: "disconnected",
            label: "Desconectado",
            detail: "SignalR esperando la selección de una LiveSession."
          });
        }
      });

      return () => {
        active = false;
      };
    }

    const connection = new HubConnectionBuilder()
      .withUrl(sessionHubUrl, {
        accessTokenFactory: () => accessToken,
        skipNegotiation: true,
        transport: HttpTransportType.WebSockets
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on("ReceiveSessionStateChanged", (payload: SessionStateChangedPayload) => {
      if (active) {
        applySessionStateChanged(payload);
      }
    });

    connection.on("ReceiveTeamProgressChanged", (payload: TeamProgressChangedPayload) => {
      if (active) {
        applyTeamProgressChanged(payload);
      }
    });

    connection.on("liveSessionStateChanged", (payload: LegacyLiveSessionStateChangedEvent) => {
      if (!active || payload.liveSessionId !== selectedLiveSessionId) {
        return;
      }

      setLiveSessions((current) =>
        current.map((liveSession) =>
          liveSession.id === payload.liveSessionId
            ? {
                ...liveSession,
                state: payload.state,
                registeredSessionTeamCount: payload.registeredSessionTeamCount
              }
            : liveSession
        )
      );
      refreshSelectedOverview();
    });

    connection.onreconnecting(() => {
      if (!active) {
        return;
      }

      setSessionRealtimeConnection({
        kind: "reconnecting",
        label: "Reconectando",
        detail: "Transmisión en tiempo real perdida. La captura permanece visible."
      });
    });

    connection.onreconnected(() => {
      if (!active) {
        return;
      }

      setSessionRealtimeConnection({
        kind: "connected",
        label: "Conectado",
        detail: "Transmisión de sesión en tiempo real restaurada."
      });
      refreshSelectedOverview();
    });

    connection.onclose((error) => {
      if (!active) {
        return;
      }

      setSessionRealtimeConnection({
        kind: error ? "error" : "disconnected",
        label: "Desconectado",
        detail: error ? `SignalR cerrado: ${error.message}` : "Transmisión de sesión en tiempo real cerrada."
      });
    });

    queueMicrotask(() => {
      if (active) {
        setSessionRealtimeConnection({
          kind: "connecting",
          label: "Reconectando",
          detail: "Abriendo la transmisión de sesión en tiempo real."
        });
      }
    });

    void connection.start().then(
      () => {
        if (!active) {
          void connection.stop();
          return;
        }

        setSessionRealtimeConnection({
          kind: "connected",
          label: "Conectado",
          detail: "Transmisión de sesión en tiempo real conectada."
        });
      },
      (error: unknown) => {
        if (!active) {
          return;
        }

        setSessionRealtimeConnection({
          kind: "error",
          label: "Desconectado",
          detail: error instanceof Error ? error.message : "No se pudo conectar a la transmisión de sesión en tiempo real."
        });
      }
    );

    return () => {
      active = false;
      void connection.stop();
    };
  }, [
    accessToken,
    applySessionStateChanged,
    applyTeamProgressChanged,
    refreshSelectedOverview,
    selectedLiveSessionId,
    sessionHubUrl
  ]);

  useEffect(() => {
    queueMicrotask(() => {
      void loadMissions();
      void loadLiveSessions();
    });
  }, [loadLiveSessions, loadMissions]);

  useEffect(() => {
    if (!selectedMissionId) {
      queueMicrotask(() => {
        setSelectedMission(null);
        setSelectedMissionStageIds([]);
      });
      return;
    }

    queueMicrotask(() => {
      void loadMissionDetail(selectedMissionId);
    });
  }, [loadMissionDetail, selectedMissionId]);

  useEffect(() => {
    if (!selectedLiveSessionId) {
      queueMicrotask(() => {
        setSelectedLiveSessionOverview(null);
        setSelectedLiveSessionRanking(null);
        setSelectedLiveSessionEventLog([]);
        setSelectedSessionTeamId(null);
        setSelectedSessionTeamDetail(null);
        setSessionTeamDetailError(null);
        setRankingError(null);
        setEventLogError(null);
      });
      return;
    }

    queueMicrotask(() => {
      setSelectedSessionTeamId(null);
      setSelectedSessionTeamDetail(null);
      setSessionTeamDetailError(null);
      void loadLiveSessionOverview(selectedLiveSessionId);
      void loadLiveSessionRanking(selectedLiveSessionId);
      void loadLiveSessionEventLog(selectedLiveSessionId);
    });
  }, [loadLiveSessionEventLog, loadLiveSessionOverview, loadLiveSessionRanking, selectedLiveSessionId]);

  useEffect(() => {
    if (!selectedLiveSessionId || !selectedSessionTeamId) {
      queueMicrotask(() => {
        setSelectedSessionTeamDetail(null);
        setSessionTeamDetailError(null);
      });
      return;
    }

    queueMicrotask(() => {
      void loadSessionTeamDetail(selectedLiveSessionId, selectedSessionTeamId, inactivityThresholdMinutes);
    });
  }, [inactivityThresholdMinutes, loadSessionTeamDetail, selectedLiveSessionId, selectedSessionTeamId]);

  useEffect(() => {
    let active = true;

    if (!selectedLiveSessionId || !accessToken.trim() || !scoringAuditHubUrl.trim()) {
      queueMicrotask(() => {
        if (active) {
          setScoringRealtimeConnection({
            kind: "disconnected",
            label: "Desconectado",
            detail: "SignalR esperando la selección de una LiveSession."
          });
        }
      });

      return () => {
        active = false;
      };
    }

    const connection = new HubConnectionBuilder()
      .withUrl(scoringAuditHubUrl, {
        accessTokenFactory: () => accessToken,
        skipNegotiation: true,
        transport: HttpTransportType.WebSockets
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on("ReceiveRankingUpdated", (payload: RankingPayload) => {
      if (!active || payload.liveSessionId !== selectedLiveSessionId) {
        return;
      }

      setSelectedLiveSessionRanking(payload);
      setRankingError(null);
    });

    connection.on("ReceiveEventLogUpdated", (payload: SessionEventLogItem) => {
      if (!active || payload.liveSessionId !== selectedLiveSessionId) {
        return;
      }

      setSelectedLiveSessionEventLog((current) => upsertSessionEventLogItem(current, payload));
      setEventLogError(null);
    });

    connection.onreconnecting(() => {
      if (!active) {
        return;
      }

      setScoringRealtimeConnection({
        kind: "reconnecting",
        label: "Reconectando",
        detail: "Transmisión de Scoring y Auditoría perdida. Los componentes mantienen la última captura."
      });
    });

    connection.onreconnected(() => {
      if (active) {
        setScoringRealtimeConnection({
          kind: "connected",
          label: "Conectado",
          detail: "Transmisión de Scoring y Auditoría restaurada."
        });
        void loadLiveSessionRanking(selectedLiveSessionId);
        void loadLiveSessionEventLog(selectedLiveSessionId);
      }
    });

    connection.onclose((error) => {
      if (!active) {
        return;
      }

      setScoringRealtimeConnection({
        kind: error ? "error" : "disconnected",
        label: "Desconectado",
        detail: error ? `Scoring y Auditoría cerrado: ${error.message}` : "Transmisión de Scoring y Auditoría cerrada."
      });
    });

    queueMicrotask(() => {
      if (active) {
        setScoringRealtimeConnection({
          kind: "connecting",
          label: "Reconectando",
          detail: "Abriendo la transmisión de Scoring y Auditoría."
        });
      }
    });

    void connection.start().then(
      () => {
        if (!active) {
          void connection.stop();
          return;
        }

        setScoringRealtimeConnection({
          kind: "connected",
          label: "Conectado",
          detail: "Transmisión de Scoring y Auditoría conectada."
        });
      },
      (error: unknown) => {
        if (!active) {
          return;
        }

        setScoringRealtimeConnection({
          kind: "error",
          label: "Desconectado",
          detail: error instanceof Error ? error.message : "No se pudo conectar al servicio de tiempo real de Scoring y Auditoría."
        });
      }
    );

    return () => {
      active = false;
      void connection.stop();
    };
  }, [accessToken, loadLiveSessionEventLog, loadLiveSessionRanking, scoringAuditHubUrl, selectedLiveSessionId]);

  useEffect(() => {
    if (!selectedLiveSession || operationalHintDraft.missionStageId) {
      return;
    }

    queueMicrotask(() => {
      setOperationalHintDraft((current) => ({
        ...current,
        missionStageId: selectedLiveSession.sessionStageFlow[0]?.missionStageId ?? ""
      }));
    });
  }, [operationalHintDraft.missionStageId, selectedLiveSession]);

  function toggleMissionStageSelection(missionStageId: string) {
    setSelectedMissionStageIds((current) =>
      current.includes(missionStageId)
        ? current.filter((existingMissionStageId) => existingMissionStageId !== missionStageId)
        : [...current, missionStageId]
    );
  }

  function moveSelectedMissionStage(missionStageId: string, direction: -1 | 1) {
    setSelectedMissionStageIds((current) => {
      const currentIndex = current.indexOf(missionStageId);
      const nextIndex = currentIndex + direction;

      if (currentIndex === -1 || nextIndex < 0 || nextIndex >= current.length) {
        return current;
      }

      const nextSelection = [...current];
      [nextSelection[currentIndex], nextSelection[nextIndex]] = [nextSelection[nextIndex], nextSelection[currentIndex]];

      return nextSelection;
    });
  }

  async function handleCreateLiveSession(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!selectedMission) {
      setErrorMessage("Seleccione una Misión activa primero.");
      return;
    }

    if (!draft.name.trim()) {
      setErrorMessage("El nombre de la LiveSession es obligatorio.");
      return;
    }

    if (selectedMissionStageIds.length === 0) {
      setErrorMessage("El Flujo de Etapas de Sesión debe conservar al menos una Etapa de Misión activa.");
      return;
    }

    setIsSubmitting(true);
    setFeedback(null);
    setErrorMessage(null);

    const scheduledStartAtUtc = draft.scheduledStartAtLocal
      ? new Date(draft.scheduledStartAtLocal).toISOString()
      : null;

    try {
      const response = await fetch(liveSessionsUrl, {
        method: "POST",
        headers: {
          ...createAuthorizedHeaders(accessToken),
          "Content-Type": "application/json"
        },
        body: JSON.stringify({
          missionId: selectedMission.id,
          name: draft.name.trim(),
          scheduledStartAtUtc,
          selectedMissionStageIds
        })
      });

      if (!response.ok) {
        throw new Error(await readFailureDetail(response));
      }

      const liveSession = (await response.json()) as LiveSession;
      setSelectedLiveSessionId(liveSession.id);
      setDraft({
        name: `${selectedMission.name} / Ejecución de seguimiento`,
        scheduledStartAtLocal: ""
      });
      setFeedback("LiveSession programada a partir de la captura de Misión activa.");
      await loadLiveSessions(liveSession.id);
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "No se pudo crear la LiveSession.");
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleLifecycleAction(action: LiveSessionLifecycleAction) {
    if (!selectedLiveSession) {
      return;
    }

    const selectedAction = lifecycleActions.find((candidate) => candidate.action === action);
    if (!selectedAction || selectedAction.disabled) {
      return;
    }

    if (
      selectedAction.requiresConfirmation &&
      !window.confirm(`Â¿Confirmar ${selectedAction.label.toLowerCase()} para "${selectedLiveSession.name}"?`)
    ) {
      return;
    }

    setLifecycleActionPending(action);
    setErrorMessage(null);
    setFeedback(null);

    try {
      const response = await fetch(`${liveSessionsUrl}/${selectedLiveSession.id}/lifecycle/${action}`, {
        method: "POST",
        headers: createAuthorizedHeaders(accessToken)
      });

      if (!response.ok) {
        throw new Error(await readFailureDetail(response));
      }

      await loadLiveSessions(selectedLiveSession.id);
      await loadLiveSessionOverview(selectedLiveSession.id);
      setFeedback(`LiveSession movida a través de la acción de ciclo de vida: ${action}.`);
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "No se pudo actualizar el ciclo de vida de la sesión.");
    } finally {
      setLifecycleActionPending(null);
    }
  }

  async function handleDeactivateStage(missionStageId: string) {
    if (!selectedLiveSession) {
      return;
    }

    setDeactivatingStageId(missionStageId);
    setErrorMessage(null);
    setFeedback(null);

    try {
      const response = await fetch(`${liveSessionsUrl}/${selectedLiveSession.id}/stages/${missionStageId}/deactivate`, {
        method: "POST",
        headers: createAuthorizedHeaders(accessToken)
      });

      if (!response.ok) {
        throw new Error(await readFailureDetail(response));
      }

      await loadLiveSessions(selectedLiveSession.id);
      await loadLiveSessionOverview(selectedLiveSession.id);
      setFeedback("Etapa de sesión desactivada para esta LiveSession.");
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "No se pudo desactivar la etapa de sesión.");
    } finally {
      setDeactivatingStageId(null);
    }
  }

  async function handleEnrollmentAction(action: EnrollmentAction) {
    if (!selectedLiveSession || enrollmentActionPending !== null) {
      return;
    }

    const endpointByAction: Record<EnrollmentAction, string> = {
      "generate-join-code": "session-enrollment/join-code",
      "open-window": "session-enrollment/window/open",
      "close-window": "session-enrollment/window/close"
    };
    const successByAction: Record<EnrollmentAction, string> = {
      "generate-join-code": "Código de unión generado.",
      "open-window": "Ventana de inscripción abierta. Los participantes ya pueden registrar equipos.",
      "close-window": "Ventana de inscripción cerrada."
    };

    setEnrollmentActionPending(action);
    setErrorMessage(null);
    setFeedback(null);

    try {
      const response = await fetch(`${liveSessionsUrl}/${selectedLiveSession.id}/${endpointByAction[action]}`, {
        method: "POST",
        headers: createAuthorizedHeaders(accessToken)
      });

      if (!response.ok) {
        throw new Error(await readFailureDetail(response));
      }

      await loadLiveSessions(selectedLiveSession.id);
      setFeedback(successByAction[action]);
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "No se pudo actualizar la inscripción de la sesión.");
    } finally {
      setEnrollmentActionPending(null);
    }
  }

  async function handleCreateSessionTeam(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!selectedLiveSession || isCreatingTeam) {
      return;
    }

    const liveSessionId = selectedLiveSession.id;
    if (!liveSessionId) {
      setErrorMessage("Error de estado: No se pudo determinar el ID de la sesión en vivo.");
      console.error("LiveSession ID is undefined or missing in selectedLiveSession:", selectedLiveSession);
      return;
    }

    const trimmedTeamName = newTeamName.trim();
    if (trimmedTeamName.length < 3) {
      setErrorMessage("El nombre del equipo debe tener al menos 3 caracteres.");
      return;
    }

    setIsCreatingTeam(true);
    setErrorMessage(null);
    setFeedback(null);

    try {
      const url = `${liveSessionsUrl}/${encodeURIComponent(liveSessionId)}/session-teams`;
      const response = await fetch(url, {
        method: "POST",
        headers: {
          ...createAuthorizedHeaders(accessToken),
          "Content-Type": "application/json"
        },
        body: JSON.stringify({ teamName: trimmedTeamName })
      });

      if (!response.ok) {
        const errorDetail = await readFailureDetail(response);
        console.error("Error response from backend when creating team:", errorDetail);
        throw new Error(errorDetail);
      }

      setNewTeamName("");
      await loadLiveSessions(liveSessionId);
      await loadLiveSessionOverview(liveSessionId);
      setFeedback(`Equipo "${trimmedTeamName}" creado. Los jugadores ya pueden unirse con el código de la sesión.`);
    } catch (error) {
      console.error("Caught error during handleCreateSessionTeam:", error);
      setErrorMessage(error instanceof Error ? error.message : "No se pudo crear el equipo.");
    } finally {
      setIsCreatingTeam(false);
    }
  }

  async function handleApplyPenalty(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!selectedLiveSession) {
      return;
    }

    if (!effectivePenaltySessionTeamId) {
      setErrorMessage("Seleccione un equipo de la sesión para la penalización.");
      return;
    }

    if (!penaltyDraft.reason.trim()) {
      setErrorMessage("El motivo de la penalización es obligatorio.");
      return;
    }

    setIsSubmittingPenalty(true);
    setErrorMessage(null);
    setFeedback(null);

    try {
      const response = await fetch(`${liveSessionsUrl}/${selectedLiveSession.id}/penalties`, {
        method: "POST",
        headers: {
          ...createAuthorizedHeaders(accessToken),
          "Content-Type": "application/json"
        },
        body: JSON.stringify({
          sessionTeamId: effectivePenaltySessionTeamId,
          commandId: penaltyDraft.commandId,
          severity: penaltyDraft.severity,
          reason: penaltyDraft.reason.trim()
        })
      });

      if (!response.ok) {
        throw new Error(await readFailureDetail(response));
      }

      const payload = (await response.json()) as ApplyPenaltyResponse;
      setSelectedLiveSessionRanking(payload.ranking);
      setPenaltyDraft(createEmptyPenaltyDraft(payload.sessionTeamId));
      setFeedback(
        payload.penaltyApplied
          ? `Penalización aplicada con severidad ${penaltyDraft.severity}.`
          : "Comando de penalización duplicado ignorado. El Ranking no ha cambiado."
      );
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "No se pudo aplicar la penalización.");
    } finally {
      setIsSubmittingPenalty(false);
    }
  }

  async function handleReleaseHint(hintId: string, sessionTeamId: string | null) {
    if (!selectedLiveSession) {
      return;
    }

    setIsSubmittingHint(true);
    setErrorMessage(null);
    setFeedback(null);

    try {
      const response = await fetch(`${liveSessionsUrl}/${selectedLiveSession.id}/hints/${hintId}/release`, {
        method: "POST",
        headers: {
          ...createAuthorizedHeaders(accessToken),
          "Content-Type": "application/json"
        },
        body: JSON.stringify({ sessionTeamId })
      });

      if (!response.ok) {
        throw new Error(await readFailureDetail(response));
      }

      await loadLiveSessionOverview(selectedLiveSession.id);
      setFeedback("Pista liberada para los equipos de sesión elegibles.");
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "No se pudo liberar la pista.");
    } finally {
      setIsSubmittingHint(false);
    }
  }

  async function handleCreateOperationalHint(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!selectedLiveSession || !operationalHintStageId) {
      return;
    }

    if (!operationalHintDraft.content.trim()) {
      setErrorMessage("El contenido de la pista es obligatorio.");
      return;
    }

    setIsSubmittingHint(true);
    setErrorMessage(null);
    setFeedback(null);

    try {
      const response = await fetch(`${liveSessionsUrl}/${selectedLiveSession.id}/stages/${operationalHintStageId}/hints`, {
        method: "POST",
        headers: {
          ...createAuthorizedHeaders(accessToken),
          "Content-Type": "application/json"
        },
        body: JSON.stringify({
          content: operationalHintDraft.content.trim(),
          latitude: parseOptionalCoordinate(operationalHintDraft.latitude),
          longitude: parseOptionalCoordinate(operationalHintDraft.longitude)
        })
      });

      if (!response.ok) {
        throw new Error(await readFailureDetail(response));
      }

      setOperationalHintDraft(createEmptyOperationalHintDraft());
      await loadLiveSessions(selectedLiveSession.id);
      await loadLiveSessionOverview(selectedLiveSession.id);
      setFeedback("Pista operativa añadida a la captura de la LiveSession.");
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "No se pudo crear la pista operativa.");
    } finally {
      setIsSubmittingHint(false);
    }
  }

  async function handleOverrideSubmission(submissionId: string, reason: string) {
    if (!selectedLiveSession || !selectedSessionTeamId) {
      return;
    }

    setOverridePendingSubmissionId(submissionId);
    setErrorMessage(null);
    setFeedback(null);

    try {
      const response = await fetch(`${SessionManagementUrl}/submissions/${submissionId}/override`, {
        method: "POST",
        headers: {
          ...createAuthorizedHeaders(accessToken),
          "Content-Type": "application/json"
        },
        body: JSON.stringify({
          isAccepted: true,
          reason
        })
      });

      if (!response.ok) {
        throw new Error(await readFailureDetail(response));
      }

      await loadSessionTeamDetail(selectedLiveSession.id, selectedSessionTeamId, inactivityThresholdMinutes);
      await loadLiveSessionOverview(selectedLiveSession.id);
      setFeedback("La anulación de validación aceptó el envío de evidencia de trivia.");
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "No se pudo aplicar la anulación de validación.");
    } finally {
      setOverridePendingSubmissionId(null);
    }
  }

  // --- DASHBOARD DE SESIÓN (PANTALLA COMPLETA) ---
  if (selectedLiveSessionId && selectedLiveSession) {
    // Synchronized flow board: the whole group is on one stage. The current group
    // stage is the first that no team has completed yet; teams either finished it
    // (waiting) or are still on it. Skipping deactivates that stage to force advance.
    const flowStages = [...selectedLiveSession.sessionStageFlow].sort(
      (a, b) => a.sessionStageOrder - b.sessionStageOrder
    );
    const flowTeams = selectedLiveSessionOverviewTeams;
    const currentFlowStage =
      flowStages.find(
        (stage) => getSessionStageOperationalStatus(stage, flowTeams) !== "Completed"
      ) ?? flowStages[flowStages.length - 1] ?? null;
    const currentFlowOrder = currentFlowStage?.sessionStageOrder ?? 0;
    const nextFlowStage = currentFlowStage
      ? flowStages.find((stage) => stage.sessionStageOrder > currentFlowStage.sessionStageOrder) ?? null
      : null;
    const teamsFinishedCurrentStage = flowTeams.filter(
      (team) =>
        team.progressState === "Completed" ||
        (team.currentStage !== null && team.currentStage.sessionStageOrder > currentFlowOrder)
    ).length;
    const isSessionPaused = selectedLiveSession.state === "Paused";
    const flowPauseAction =
      lifecycleActions.find((candidate) => candidate.action === "pause" || candidate.action === "resume") ?? null;

    const teamsInPlay = flowTeams.filter((team) => team.progressState !== "Completed").length;
    const teamsAtGoal = flowTeams.filter((team) => team.progressState === "Completed").length;
    // Per-team inactivity is only exposed on the team-detail response, not the overview
    // list, so the sidebar tile can't compute it yet. See docs/web-refactor-followups.md.
    const inactiveCount = 0;
    const rankedSidebarTeams = [...flowTeams]
      .map((team) => ({
        team,
        ranking: selectedRankingItems.find((item) => item.sessionTeamId === team.sessionTeamId) ?? null
      }))
      .sort((a, b) => (a.ranking?.rank ?? 999) - (b.ranking?.rank ?? 999));
    const drawerTeam = flowTeams.find((team) => team.sessionTeamId === selectedSessionTeamId) ?? null;
    const drawerDetail =
      selectedSessionTeamDetail && selectedSessionTeamDetail.sessionTeamId === selectedSessionTeamId
        ? selectedSessionTeamDetail
        : null;
    const drawerRanking = drawerTeam
      ? selectedRankingItems.find((item) => item.sessionTeamId === drawerTeam.sessionTeamId) ?? null
      : null;
    const drawerCurrentStage = drawerDetail?.currentStage ?? drawerTeam?.currentStage ?? null;
    const drawerStageHints = drawerCurrentStage
      ? flowStages.find((stage) => stage.missionStageId === drawerCurrentStage.missionStageId)?.hints ?? []
      : [];

    return (
      <div className="ops-root">
        <header className="ops-header">
          <div className="ops-title">
            <div className="ops-title-row">
              <h2>{selectedLiveSession.name}</h2>
              <span className={getSessionStateBadgeClass(selectedLiveSession.state)}>
                {translateSessionState(selectedLiveSession.state)}
              </span>
            </div>
            <span className="ops-title-sub">{selectedLiveSession.missionName}</span>
          </div>

          <div className="ops-mode-toggle">
            <button className="ops-mode-btn is-active" type="button">
              <span className="ops-mode-dot" />En Vivo
            </button>
            <button className="ops-mode-btn" onClick={() => setSelectedLiveSessionId(null)} type="button">
              Planificar
            </button>
          </div>

          <div className="ops-header-right">
            <span className="ops-conn">
              <span className={getConnectionDotClass(sessionRealtimeConnection)} />
              <span className="mono ops-conn-label">{sessionRealtimeConnection.label}</span>
            </span>
            <div className="ops-timer">
              <span className="ops-timer-value mono">
                {selectedLiveSession.state === "Scheduled"
                  ? selectedLiveSession.scheduledStartAtUtc
                    ? formatTimestamp(selectedLiveSession.scheduledStartAtUtc)
                    : "Sin programar"
                  : formatRemainingSeconds(selectedLiveSessionOverview?.remainingSeconds)}
              </span>
              <span className="ops-timer-label">restante</span>
            </div>
          </div>
        </header>

        {errorMessage ? <div className="ops-banner is-error">{errorMessage}</div> : null}
        {feedback ? <div className="ops-banner is-ok">{feedback}</div> : null}

        <div className="ops-body">
          <aside className="ops-sidebar">
            <div className="ops-stat-grid">
              <div className="ops-stat">
                <span className="ops-stat-label">En juego</span>
                <span className="ops-stat-value mono">{teamsInPlay}</span>
              </div>
              <div className="ops-stat">
                <span className="ops-stat-label">En meta</span>
                <span className="ops-stat-value mono is-green">{teamsAtGoal}</span>
              </div>
              <div className="ops-stat">
                <span className="ops-stat-label">Inactivos</span>
                <span className={inactiveCount > 0 ? "ops-stat-value mono is-red" : "ops-stat-value mono"}>{inactiveCount}</span>
              </div>
              <div className="ops-stat">
                <span className="ops-stat-label">Equipos</span>
                <span className="ops-stat-value mono">{flowTeams.length}</span>
              </div>
            </div>

            <section className="ops-sb-section">
              <span className="eyebrow">Control de sesión</span>
              <div className="ops-control-actions">
                {lifecycleActions.length > 0 ? (
                  lifecycleActions.map((candidate) => {
                    let cls = "btn btn-ghost";
                    if (candidate.action === "start" || candidate.action === "resume") cls = "btn btn-success";
                    if (candidate.action === "finalize") cls = "btn btn-primary";
                    if (candidate.action === "cancel") cls = "btn btn-danger";
                    return (
                      <button
                        className={cls}
                        disabled={candidate.disabled || lifecycleActionPending !== null}
                        key={candidate.action}
                        onClick={() => void handleLifecycleAction(candidate.action)}
                        type="button"
                      >
                        {lifecycleActionPending === candidate.action ? "..." : candidate.label}
                      </button>
                    );
                  })
                ) : (
                  <span className="text-muted text-sm">Sin acciones disponibles.</span>
                )}
              </div>
            </section>

            <section className="ops-sb-section">
              <span className="eyebrow">Clasificación</span>
              <div className="ops-ranking">
                {rankedSidebarTeams.length > 0 ? (
                  rankedSidebarTeams.map(({ team, ranking }) => (
                    <button
                      className={selectedSessionTeamId === team.sessionTeamId ? "ops-rank-row is-selected" : "ops-rank-row"}
                      key={team.sessionTeamId}
                      onClick={() => handleSelectSessionTeam(team.sessionTeamId)}
                      type="button"
                    >
                      <span className={getFlowRankClass(ranking?.rank)}>{ranking ? `#${ranking.rank}` : "#—"}</span>
                      <span className="ops-rank-name">{team.teamName}</span>
                      <span className="ops-rank-score mono">{ranking ? ranking.visibleScore : 0}</span>
                    </button>
                  ))
                ) : (
                  <span className="text-muted text-sm">Sin equipos.</span>
                )}
              </div>
            </section>

            <section className="ops-sb-section ops-sb-grow">
              <span className="eyebrow">Actividad en vivo</span>
              <div className="ops-activity">
                {selectedEventLogItems.length > 0 ? (
                  selectedEventLogItems.map((log) => (
                    <div className="ops-activity-row" key={log.id}>
                      <span className="ops-activity-dot" />
                      <div className="ops-activity-body">
                        <span className="ops-activity-text">{log.description}</span>
                        <span className="ops-activity-time mono">{formatShortTimestamp(log.timestamp)}</span>
                      </div>
                    </div>
                  ))
                ) : (
                  <span className="text-muted text-sm">Sin eventos recientes.</span>
                )}
              </div>
            </section>
          </aside>

          <main className="ops-main">
          <div className="ops-board-head">
            <div className="stack-sm">
              <span className="eyebrow">Tablero de flujo</span>
              <span className="text-muted text-sm">
                El grupo avanza en conjunto · cuando un equipo completa la etapa, todos pasan a la siguiente.
              </span>
            </div>
            <span className="ops-board-count">
              <span className="ops-board-dot" />
              {flowTeams.length} equipos · avance sincronizado
            </span>
          </div>

          {selectedLiveSession.state === "Scheduled" ? (
            <div className="stack">
              <div className="stack-sm">
                <span className="eyebrow">Preparación de inscripción</span>
                <h4>Código y Ventana</h4>
                <p className="text-muted text-xs">
                  1) Generá el código de unión. 2) Abrí la inscripción para que los participantes registren equipos con ese código. 3) Con al menos un equipo registrado, iniciá la sesión desde la barra lateral.
                </p>
              </div>
              <div className="row-sm row-wrap">
                <button
                  className="btn btn-primary"
                  disabled={Boolean(selectedLiveSession.joinCode) || enrollmentActionPending !== null}
                  onClick={() => void handleEnrollmentAction("generate-join-code")}
                  type="button"
                >
                  {enrollmentActionPending === "generate-join-code"
                    ? "Generando..."
                    : selectedLiveSession.joinCode
                      ? "Código generado"
                      : "Generar código de unión"}
                </button>
                <button
                  className="btn btn-success"
                  disabled={
                    !selectedLiveSession.joinCode ||
                    Boolean(selectedLiveSession.enrollmentWindowOpenedAtUtc) ||
                    Boolean(selectedLiveSession.enrollmentWindowClosedAtUtc) ||
                    enrollmentActionPending !== null
                  }
                  onClick={() => void handleEnrollmentAction("open-window")}
                  type="button"
                >
                  {enrollmentActionPending === "open-window" ? "Abriendo..." : "Abrir inscripción"}
                </button>
                <button
                  className="btn btn-ghost"
                  disabled={
                    !selectedLiveSession.enrollmentWindowOpenedAtUtc ||
                    Boolean(selectedLiveSession.enrollmentWindowClosedAtUtc) ||
                    enrollmentActionPending !== null
                  }
                  onClick={() => void handleEnrollmentAction("close-window")}
                  type="button"
                >
                  {enrollmentActionPending === "close-window" ? "Cerrando..." : "Cerrar inscripción"}
                </button>
              </div>

              {selectedLiveSession.joinCode && (
                <div className="detail-panel" style={{ marginTop: "var(--space-sm)" }}>
                  <div className="detail-row">
                    <span className="detail-label">Código de unión</span>
                    <span className="detail-value mono">{selectedLiveSession.joinCode}</span>
                  </div>
                  <div className="detail-row">
                    <span className="detail-label">Ventana de inscripción</span>
                    <span className="detail-value">
                      {selectedLiveSession.enrollmentWindowOpenedAtUtc
                        ? selectedLiveSession.enrollmentWindowClosedAtUtc
                          ? `Cerrado el ${formatTimestamp(selectedLiveSession.enrollmentWindowClosedAtUtc)}`
                          : `Abierto desde ${formatTimestamp(selectedLiveSession.enrollmentWindowOpenedAtUtc)}`
                        : "No abierto"}
                    </span>
                  </div>
                </div>
              )}

              <hr style={{ borderColor: "var(--border)", margin: "var(--space-md) 0" }} />

              <div className="stack-sm">
                <span className="eyebrow">Crear equipo (operador)</span>
                <p className="text-muted text-xs">
                  Podés armar equipos vacíos vos mismo para que los jugadores se unan, o ayudar si tienen problemas.
                </p>
                <form className="row-sm row-wrap" onSubmit={(event) => void handleCreateSessionTeam(event)}>
                  <input
                    className="form-input"
                    maxLength={80}
                    onChange={(event) => setNewTeamName(event.target.value)}
                    placeholder="Nombre del equipo"
                    value={newTeamName}
                  />
                  <button
                    className="btn btn-primary"
                    disabled={isCreatingTeam || newTeamName.trim().length < 3}
                    type="submit"
                  >
                    {isCreatingTeam ? "Creando..." : "Crear equipo"}
                  </button>
                </form>
                {selectedLiveSessionOverviewTeams.length > 0 ? (
                  <div className="row-sm row-wrap" style={{ marginTop: "var(--space-sm)" }}>
                    {selectedLiveSessionOverviewTeams.map((team) => (
                      <span className="badge badge-blue" key={team.sessionTeamId}>
                        {team.teamName}
                      </span>
                    ))}
                  </div>
                ) : null}
              </div>
            </div>
          ) : (
            <>
              <div className="flow-stepper">
                {flowStages.map((stage, index) => {
                  const stepStatus =
                    stage.sessionStageOrder < currentFlowOrder
                      ? "done"
                      : stage.sessionStageOrder === currentFlowOrder
                        ? "current"
                        : "upcoming";
                  return (
                    <div className="flow-step" key={stage.missionStageId}>
                      {index > 0 ? (
                        <span className={`flow-step-connector is-${stepStatus === "upcoming" ? "upcoming" : "done"}`} />
                      ) : null}
                      <div className={`flow-step-node is-${stepStatus}`}>
                        {stepStatus === "done" ? "✓" : stage.sessionStageOrder}
                      </div>
                      <span className={`flow-step-name is-${stepStatus}`}>{stage.name}</span>
                      <span className="flow-step-state">
                        {stepStatus === "done" ? "Completada" : stepStatus === "current" ? "En curso" : "Próxima"}
                      </span>
                    </div>
                  );
                })}
              </div>

              {currentFlowStage ? (
                <div className={isSessionPaused ? "flow-current is-paused" : "flow-current"}>
                  <div className="flow-current-header">
                    <div className="flow-current-icon">
                      {currentFlowStage.gameType === "Trivia" ? "?" : "◈"}
                    </div>
                    <div className="stack-sm flow-current-heading">
                      <span className="flow-current-eyebrow">
                        Etapa actual · E{currentFlowStage.sessionStageOrder}
                        {isSessionPaused ? (
                          <span className="flow-paused-chip"><span className="flow-pulse-dot" />En pausa</span>
                        ) : null}
                      </span>
                      <strong className="flow-current-title">{currentFlowStage.name}</strong>
                      <span className="text-secondary text-sm">
                        {translateGameType(currentFlowStage.gameType)} · {currentFlowStage.resolvedTimeBudgetMinutes} min
                      </span>
                    </div>
                    <div className="flow-current-count">
                      <span className="mono flow-current-count-value">
                        {teamsFinishedCurrentStage}/{flowTeams.length}
                      </span>
                      <span className="flow-current-count-label">terminaron la etapa</span>
                    </div>
                  </div>

                  <div className="flow-progress-track">
                    <div
                      className="flow-progress-fill"
                      style={{ width: `${flowTeams.length ? (teamsFinishedCurrentStage / flowTeams.length) * 100 : 0}%` }}
                    />
                  </div>
                  {nextFlowStage ? (
                    <p className="text-muted text-sm">
                      En cuanto un equipo termine, el grupo completo avanza a <strong>{nextFlowStage.name}</strong>.
                    </p>
                  ) : (
                    <p className="text-muted text-sm">Es la última etapa de la sesión.</p>
                  )}

                  <div className="flow-current-actions">
                    {flowPauseAction ? (
                      <button
                        type="button"
                        className={flowPauseAction.action === "resume" ? "btn btn-success" : "btn btn-ghost"}
                        disabled={flowPauseAction.disabled || lifecycleActionPending !== null}
                        onClick={() => handleLifecycleAction(flowPauseAction.action)}
                      >
                        {lifecycleActionPending === flowPauseAction.action ? "Actualizando..." : flowPauseAction.label}
                      </button>
                    ) : null}
                    <button
                      type="button"
                      className="btn btn-danger"
                      disabled={!nextFlowStage || deactivatingStageId !== null}
                      onClick={() => handleDeactivateStage(currentFlowStage.missionStageId)}
                      title={!nextFlowStage ? "Última etapa" : "Fuerza el avance de todo el grupo a la siguiente etapa"}
                    >
                      {deactivatingStageId === currentFlowStage.missionStageId
                        ? "Saltando..."
                        : nextFlowStage
                          ? "Saltar etapa"
                          : "Última etapa"}
                    </button>
                  </div>
                </div>
              ) : null}

              {flowTeams.length > 0 ? (
                <div className="flow-team-grid">
                  {flowTeams.map((team) => {
                    const rankingItem = selectedRankingItems.find((item) => item.sessionTeamId === team.sessionTeamId);
                    const isSelected = selectedSessionTeamId === team.sessionTeamId;
                    const hasFinishedStage =
                      team.progressState === "Completed" ||
                      (team.currentStage !== null && team.currentStage.sessionStageOrder > currentFlowOrder);
                    return (
                      <button
                        key={team.sessionTeamId}
                        type="button"
                        className={isSelected ? "flow-team-card is-selected" : "flow-team-card"}
                        onClick={() => handleSelectSessionTeam(team.sessionTeamId)}
                      >
                        <div className="flow-team-card-top">
                          <span className={getFlowRankClass(rankingItem?.rank)}>
                            {rankingItem ? `#${rankingItem.rank}` : "#—"}
                          </span>
                          <span className="flow-team-card-name">{team.teamName}</span>
                        </div>
                        <span className={hasFinishedStage ? "badge badge-green" : "badge badge-amber"}>
                          {hasFinishedStage ? "Etapa lista · esperando" : "En curso"}
                        </span>
                        <div className="flow-team-card-footer">
                          <span className="mono flow-team-card-score">{rankingItem ? rankingItem.visibleScore : 0} pts</span>
                          <span className="flow-team-card-meta">
                            {team.participantCount} part. · {team.releasedHints.length} pistas
                          </span>
                        </div>
                      </button>
                    );
                  })}
                </div>
              ) : (
                <div className="empty-state">
                  <strong>Bienvenido al Comando</strong>
                  <p>Esperando interacciones y progreso de los equipos...</p>
                </div>
              )}
            </>
          )}
          </main>
        </div>

        {selectedSessionTeamId && drawerTeam ? (
          <div className="ops-drawer-overlay" onClick={() => setSelectedSessionTeamId(null)}>
            <aside className="ops-drawer" onClick={(event) => event.stopPropagation()}>
              <div className="ops-drawer-header">
                <span className={getFlowRankClass(drawerRanking?.rank)}>{drawerRanking ? `#${drawerRanking.rank}` : "#—"}</span>
                <div className="ops-drawer-heading">
                  <h3>{drawerTeam.teamName}</h3>
                  <span className="text-muted text-sm">
                    {drawerCurrentStage ? `${drawerCurrentStage.name} · en etapa` : "Sin etapa actual"}
                  </span>
                </div>
                <button className="ops-drawer-close" onClick={() => setSelectedSessionTeamId(null)} type="button" aria-label="Cerrar">
                  ✕
                </button>
              </div>

              <div className="ops-drawer-stats">
                <div className="ops-stat">
                  <span className="ops-stat-label">Puntaje</span>
                  <span className="ops-stat-value mono is-accent">{drawerRanking ? drawerRanking.visibleScore : 0}</span>
                </div>
                <div className="ops-stat">
                  <span className="ops-stat-label">Puesto</span>
                  <span className="ops-stat-value mono">{drawerRanking ? `#${drawerRanking.rank}` : "#—"}</span>
                </div>
                <div className="ops-stat">
                  <span className="ops-stat-label">Jugadores</span>
                  <span className="ops-stat-value mono">{drawerTeam.participantCount}</span>
                </div>
              </div>

              {drawerDetail?.isInactive ? (
                <div className="ops-drawer-alert"><span className="flow-pulse-dot" />Sin actividad reciente. Considerá liberar una pista.</div>
              ) : null}

              <div className="ops-drawer-section">
                <span className="eyebrow">Recorrido de etapas</span>
                <div className="ops-journey">
                  {flowStages.map((stage) => {
                    const currentOrder = drawerCurrentStage?.sessionStageOrder ?? 0;
                    const completed = drawerTeam.progressState === "Completed" || stage.sessionStageOrder < currentOrder;
                    const isCurrent = !completed && stage.sessionStageOrder === currentOrder;
                    const label = completed ? "Completada" : isCurrent ? "Actual" : "Pendiente";
                    const rowClass = completed ? "is-done" : isCurrent ? "is-current" : "is-upcoming";
                    return (
                      <div className={`ops-journey-row ${rowClass}`} key={stage.missionStageId}>
                        <span className="ops-journey-dot" />
                        <div className="ops-journey-body">
                          <div className="ops-journey-head">
                            <strong>{stage.name}</strong>
                            <span className={completed ? "badge badge-green" : isCurrent ? "badge badge-amber" : "badge"}>{label}</span>
                          </div>
                          <span className="text-muted text-xs">{translateGameType(stage.gameType)} · {stage.resolvedTimeBudgetMinutes} min</span>
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>

              {drawerCurrentStage ? (
                <div className="ops-drawer-section">
                  <span className="eyebrow">Pistas · etapa actual</span>
                  <div className="stack-sm">
                    {drawerStageHints.map((hint) => {
                      const released = getReleasedHintsForTeam(drawerTeam).some((item) => item.hintId === hint.id);
                      return (
                        <div className="ops-hint-row" key={hint.id}>
                          <span className="ops-hint-text">{hint.content}</span>
                          <button
                            type="button"
                            className={released ? "btn btn-ghost" : "btn btn-primary"}
                            disabled={released || isSubmittingHint}
                            onClick={() => void handleReleaseHint(hint.id, drawerTeam.sessionTeamId)}
                          >
                            {released ? "Liberada" : "Liberar"}
                          </button>
                        </div>
                      );
                    })}
                    {drawerStageHints.length === 0 ? (
                      <span className="text-muted text-sm">Sin pistas predefinidas para esta etapa.</span>
                    ) : null}
                  </div>
                  <form className="ops-hint-create" onSubmit={handleCreateOperationalHint}>
                    <textarea
                      className="ls-input"
                      maxLength={500}
                      placeholder="Nueva pista operativa para esta etapa…"
                      rows={2}
                      value={operationalHintDraft.content}
                      onChange={(event) =>
                        setOperationalHintDraft((curr) => ({
                          ...curr,
                          content: event.target.value,
                          missionStageId: drawerCurrentStage.missionStageId
                        }))
                      }
                    />
                    <button
                      className="btn btn-ghost"
                      disabled={isSubmittingHint || !operationalHintDraft.content.trim()}
                      type="submit"
                    >
                      {isSubmittingHint ? "Guardando..." : "Crear pista para esta etapa"}
                    </button>
                  </form>
                </div>
              ) : null}

              {drawerDetail && drawerDetail.evidenceSubmissions.length > 0 ? (
                <div className="ops-drawer-section">
                  <span className="eyebrow">Evidencias recientes</span>
                  <div className="stack-sm">
                    {drawerDetail.evidenceSubmissions.map((submission) => (
                      <div className="card card-compact" key={submission.id}>
                        <div className="row-between">
                          <div className="stack-sm">
                            <strong>{submission.stageName}</strong>
                            <span className="text-muted text-xs">{formatShortTimestamp(submission.submittedAtUtc)}</span>
                          </div>
                          <span className={submission.validationOutcome === "Rejected" ? "badge badge-red" : submission.validationOutcome === "Accepted" ? "badge badge-green" : "badge badge-amber"}>
                            {translateValidationOutcome(submission.validationOutcome)}
                          </span>
                        </div>
                        {submission.submittedText ? (
                          <p className="text-muted text-sm" style={{ marginTop: "var(--space-sm)" }}><strong>Respuesta:</strong> {submission.submittedText}</p>
                        ) : null}
                        {submission.validationOutcome === "Rejected" ? (
                          <div className="form-actions" style={{ marginTop: "var(--space-sm)" }}>
                            <button
                              type="button"
                              className="btn btn-primary"
                              disabled={overridePendingSubmissionId === submission.id}
                              onClick={() => void handleOverrideSubmission(submission.id, "Aceptado manualmente por el operador.")}
                            >
                              {overridePendingSubmissionId === submission.id ? "Aceptando..." : "Aceptar (Forzar)"}
                            </button>
                          </div>
                        ) : null}
                      </div>
                    ))}
                  </div>
                </div>
              ) : null}

              <div className="ops-drawer-section">
                <span className="eyebrow">Aplicar penalización</span>
                <form className="stack-sm" onSubmit={handleApplyPenalty}>
                  <div className="ops-severity-grid">
                    {([
                      { key: "Minor", label: "Menor", points: "-50" },
                      { key: "Major", label: "Mayor", points: "-100" },
                      { key: "Critical", label: "Crítica", points: "-200" }
                    ] as const).map((severity) => (
                      <button
                        type="button"
                        key={severity.key}
                        className={penaltyDraft.severity === severity.key ? "ops-severity is-active" : "ops-severity"}
                        onClick={() => setPenaltyDraft((curr) => ({ ...curr, severity: severity.key, sessionTeamId: drawerTeam.sessionTeamId }))}
                      >
                        <span className="ops-severity-label">{severity.label}</span>
                        <span className="ops-severity-points mono">{severity.points}</span>
                      </button>
                    ))}
                  </div>
                  <textarea
                    className="ls-input"
                    maxLength={200}
                    placeholder="Motivo (obligatorio)…"
                    rows={2}
                    value={penaltyDraft.reason}
                    onChange={(event) => setPenaltyDraft((curr) => ({ ...curr, reason: event.target.value, sessionTeamId: drawerTeam.sessionTeamId }))}
                  />
                  <button type="submit" className="btn btn-danger" disabled={isSubmittingPenalty || !penaltyDraft.reason.trim()}>
                    {isSubmittingPenalty ? "Aplicando..." : `Aplicar penalización a ${drawerTeam.teamName}`}
                  </button>
                </form>
              </div>
            </aside>
          </div>
        ) : null}
      </div>
    );
  }

  return (
    <div className="workspace-section">
      <div className="workspace-section-header">
        <div className="stack-sm">
          <span className="eyebrow">Operaciones de sesión</span>
          <h2>Espacio de trabajo de programación de LiveSession</h2>
        </div>
        <p className="text-muted">
          El operador selecciona una Misión activa, recorta el Flujo de Etapas de Sesión a etapas activas, reordena la ejecución efectiva
          y persiste una captura de LiveSession Programada sin modificar el Diseño de la Misión.
        </p>
      </div>

      <div className="workspace-section-body">
        <div className="split-layout-wide">
          <div className="card card-compact">
            <strong className="text-sm">Misiones elegibles</strong>
            <p className="text-lg">{missionSummary.totalMissions}</p>
          </div>
          <div className="card card-compact">
            <strong className="text-sm">Etapas de misión activas</strong>
            <p className="text-lg">{missionSummary.totalActiveStages}</p>
          </div>
          <div className="card card-compact">
            <strong className="text-sm">LiveSessions programadas</strong>
            <p className="text-lg">{missionSummary.totalLiveSessions}</p>
          </div>
        </div>

        {errorMessage ? <p className="error-banner">{errorMessage}</p> : null}
        {feedback ? <p className="success-banner">{feedback}</p> : null}

        <div className="split-layout">
          <section className="card">
            <div className="card-header">
              <div className="stack-sm">
                <span className="eyebrow">Origen de la misión</span>
                <h3>Misiones elegibles</h3>
              </div>
              <button
                className="btn btn-ghost btn-sm"
                onClick={() => {
                  setIsLoadingMissions(true);
                  void loadMissions(selectedMissionId ?? undefined);
                }}
                type="button"
              >
                Actualizar
              </button>
            </div>

            {isLoadingMissions ? <p className="loading-center">Cargando misiones elegibles.</p> : null}

            {!isLoadingMissions && missions.length === 0 ? (
              <div className="empty-state">
                <strong>Ninguna misión activa puede iniciar una LiveSession todavía.</strong>
                <p>El Diseño de la Misión debe exponer al menos una Etapa de Misión activa antes de que sea posible programar.</p>
              </div>
            ) : null}

            <div className="stack-sm">
              {missions.map((mission) => (
                <button
                  className={mission.id === selectedMissionId ? "card card-compact clickable is-selected" : "card card-compact clickable"}
                  key={mission.id}
                  onClick={() => {
                    setFeedback(null);
                    setSelectedMissionId(mission.id);
                  }}
                  type="button"
                >
                  <div className="row-between">
                    <strong>{mission.name}</strong>
                    <span className="badge badge-green">{mission.activeMissionStageCount} etapas</span>
                  </div>
                  <div className="detail-panel">
                    <div className="detail-row">
                      <span className="detail-label">Duración</span>
                      <span className="detail-value">{mission.maximumDurationMinutes} min</span>
                    </div>
                  </div>
                </button>
              ))}
            </div>
          </section>

          <section className="card">
            <div className="card-header">
              <div className="stack-sm">
                <span className="eyebrow">Crear</span>
                <h3>{selectedMission?.name ?? "LiveSession programada"}</h3>
              </div>
              {selectedMission ? <span className="badge badge-green">{draftPreview.length} seleccionados</span> : null}
            </div>

            {isLoadingMissionDetail ? <p className="loading-center">Cargando flujo de etapas de la misión.</p> : null}

            {selectedMission ? (
              <form className="stack" onSubmit={handleCreateLiveSession}>
                <div className="form-group">
                  <label className="form-label">Nombre de la LiveSession</label>
                  <input
                    className="form-input"
                    maxLength={120}
                    onChange={(event) =>
                      setDraft((current) => ({
                        ...current,
                        name: event.target.value
                      }))
                    }
                    required
                    value={draft.name}
                  />
                </div>

                <div className="form-row">
                  <div className="form-group">
                    <label className="form-label">Inicio programado</label>
                    <input
                      className="form-input"
                      onChange={(event) =>
                        setDraft((current) => ({
                          ...current,
                          scheduledStartAtLocal: event.target.value
                        }))
                      }
                      type="datetime-local"
                      value={draft.scheduledStartAtLocal}
                    />
                  </div>

                  <div className="form-group">
                    <label className="form-label">Captura de la misión</label>
                    <input className="form-input" disabled value={`${(selectedMission.missionStages || []).length} etapas activas`} />
                    <span className="form-hint">
                      La misión sigue siendo reutilizable. La LiveSession almacena su propia captura efectiva del Flujo de Etapas de Sesión.
                    </span>
                  </div>
                </div>

                <section className="card-section">
                  <div className="card-header">
                    <div className="stack-sm">
                      <span className="eyebrow">Flujo de etapas de sesión</span>
                      <h4>Etapas de misión elegibles</h4>
                    </div>
                  </div>

                  <div className="stage-flow">
                    {(selectedMission.missionStages || []).map((missionStage) => {
                      const isSelected = selectedMissionStageIds.includes(missionStage.id);
                      const selectedIndex = selectedMissionStageIds.indexOf(missionStage.id);

                      return (
                        <div className="stage-item" key={missionStage.id}>
                          <div className="stage-item-info">
                            <div className="stack-sm">
                              <strong className="stage-name">{missionStage.name}</strong>
                              <span className="stage-meta">
                                Orden de origen {missionStage.sourceOrder}. {translateGameType(missionStage.gameType)}.{" "}
                                {missionStage.resolvedTimeBudgetMinutes} min.
                              </span>
                              <p>{missionStage.prompt}</p>
                            </div>
                            <span className={isSelected ? "badge badge-green" : "badge badge-red"}>
                              {isSelected ? `seleccionado #${selectedIndex + 1}` : "excluido"}
                            </span>
                          </div>

                          <div className="row-sm">
                            <button
                              className={isSelected ? "btn btn-danger btn-sm" : "btn btn-ghost btn-sm"}
                              onClick={() => toggleMissionStageSelection(missionStage.id)}
                              type="button"
                            >
                              {isSelected ? "Quitar del flujo" : "Agregar al flujo"}
                            </button>
                            <button
                              className="btn btn-ghost btn-sm"
                              disabled={!isSelected || selectedIndex <= 0}
                              onClick={() => moveSelectedMissionStage(missionStage.id, -1)}
                              type="button"
                            >
                              Subir
                            </button>
                            <button
                              className="btn btn-ghost btn-sm"
                              disabled={!isSelected || selectedIndex === -1 || selectedIndex >= selectedMissionStageIds.length - 1}
                              onClick={() => moveSelectedMissionStage(missionStage.id, 1)}
                              type="button"
                            >
                              Bajar
                            </button>
                          </div>
                        </div>
                      );
                    })}
                  </div>
                </section>

                <section className="card-section">
                  <div className="card-header">
                    <div className="stack-sm">
                      <span className="eyebrow">Vista previa</span>
                      <h3>Flujo efectivo de etapas de sesión</h3>
                    </div>
                  </div>

                  {draftPreview.length === 0 ? (
                    <div className="empty-state">
                      <strong>Ninguna etapa de misión activa seleccionada.</strong>
                      <p>Mantenga al menos una etapa en el flujo antes de programar la LiveSession.</p>
                    </div>
                  ) : (
                    <div className="stage-flow">
                      {draftPreview.map((missionStage) => (
                        <div className="stage-item" key={missionStage.id}>
                          <div className="stage-item-info">
                            <strong className="stage-name">
                              <span className="stage-number">#{missionStage.draftSessionStageOrder}</span> {missionStage.name}
                            </strong>
                            <span className="badge badge-green">{translateGameType(missionStage.gameType)}</span>
                          </div>
                          <span className="stage-meta">
                            Orden de origen {missionStage.sourceOrder}. {missionStage.resolvedTimeBudgetMinutes} min.{" "}
                            {(missionStage.hints || []).length} pistas copiadas a la captura de la sesión.
                          </span>
                          <p>{missionStage.prompt}</p>
                        </div>
                      ))}
                    </div>
                  )}
                </section>

                <div className="form-actions">
                  <button className="btn btn-primary" disabled={isSubmitting} type="submit">
                    {isSubmitting ? "Programando..." : "Crear LiveSession programada"}
                  </button>
                </div>
              </form>
            ) : (
              <div className="empty-state">
                <strong>Seleccione una misión elegible.</strong>
                <p>La programación del operador comienza a partir de una captura de Misión activa expuesta por el Diseño de Misión.</p>
              </div>
            )}
          </section>
        </div>

        <section className="card">
          <div className="card-header">
            <div className="stack-sm">
              <span className="eyebrow">Salida programada</span>
              <h3>LiveSessions persistidas</h3>
            </div>
            <button
              className="btn btn-ghost btn-sm"
              onClick={() => {
                setIsLoadingLiveSessions(true);
                void loadLiveSessions(selectedLiveSessionId ?? undefined);
              }}
              type="button"
            >
              Actualizar
            </button>
          </div>

          {isLoadingLiveSessions ? <p className="loading-center">Cargando LiveSessions.</p> : null}

          {!isLoadingLiveSessions && liveSessions.length === 0 ? (
            <div className="empty-state">
              <strong>Ninguna LiveSession programada todavía.</strong>
              <p>La primera creación exitosa aparecerá aquí con la captura persistida del Flujo de Etapas de Sesión.</p>
            </div>
          ) : null}

          <div className="stack">
            {[...liveSessions].sort((a, b) => {
              const isAActive = a.state === "Active" || a.state === "Paused";
              const isBActive = b.state === "Active" || b.state === "Paused";
              if (isAActive && !isBActive) return -1;
              if (!isAActive && isBActive) return 1;
              return 0;
            }).map((liveSession) => (
              <button
                className={liveSession.id === selectedLiveSession?.id ? "card card-compact clickable is-selected" : "card card-compact clickable"}
                key={liveSession.id}
                onClick={() => setSelectedLiveSessionId(liveSession.id)}
                type="button"
              >
                <div className="row-between">
                  <strong>{liveSession.name}</strong>
                  <span className={getSessionStateBadgeClass(liveSession.state)}>{translateSessionState(liveSession.state)}</span>
                </div>
                <p className="text-muted">{liveSession.missionName}</p>
                <div className="detail-panel">
                  <div className="detail-row">
                    <span className="detail-label">Programada</span>
                    <span className="detail-value">{formatTimestamp(liveSession.scheduledStartAtUtc)}</span>
                  </div>
                  <div className="detail-row">
                    <span className="detail-label">Equipos</span>
                    <span className="detail-value">{liveSession.registeredSessionTeamCount}</span>
                  </div>
                </div>
              </button>
            ))}
          </div>
        </section>

      </div>
    </div>
  );
}
