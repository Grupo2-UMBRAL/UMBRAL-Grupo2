"use client";

import { type FormEvent, useCallback, useEffect, useMemo, useState } from "react";
import {
  HubConnectionBuilder,
  HttpTransportType,
  LogLevel
} from "@microsoft/signalr";
import { getClientConfig } from "@/lib/config";

type EligibleMissionSummary = {
  id: string;
  name: string;
  difficulty: string;
  maximumDurationMinutes: number;
  gameType: string;
  activeMissionStageCount: number;
};

type EligibleMissionStageHint = {
  id: string;
  content: string;
  isSolution: boolean;
  latitude: number | null;
  longitude: number | null;
};

type EligibleMissionStage = {
  id: string;
  name: string;
  sessionStageOrder: number;
  sourceOrder: number;
  resolvedTimeBudgetMinutes: number;
  gameType: string;
  prompt: string;
  expectedQrHash: string | null;
  triviaValidAnswer: string | null;
  triviaInitialValidationCriterion: string | null;
  hints: EligibleMissionStageHint[];
};

type EligibleMissionDetail = {
  id: string;
  name: string;
  description: string;
  difficulty: string;
  maximumDurationMinutes: number;
  gameType: string;
  missionStages: EligibleMissionStage[];
};

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
  onSelectSessionTeam: (sessionTeamId: string) => void;
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
    return "Sin sincronizaciÃ³n";
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
    `Equipo de sesiÃ³n ${sessionTeamId.slice(0, 8)}`
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

function translateGameType(gameType: string): string {
  const normalized = gameType.replace(/\s+/g, "").toLowerCase();
  if (normalized === "treasurehunt") {
    return "BÃºsqueda del tesoro";
  }
  if (normalized === "trivia") {
    return "Trivia";
  }
  return gameType;
}

function translateDifficulty(difficulty: string): string {
  const norm = difficulty.toLowerCase().trim();
  switch (norm) {
    case "easy":
      return "FÃ¡cil";
    case "medium":
      return "Medio";
    case "hard":
      return "DifÃ­cil";
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
    throw new Error("Las coordenadas deben ser valores numÃ©ricos.");
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

function getDifficultyBadgeClass(difficulty: string) {
  const norm = difficulty.toLowerCase().trim();
  switch (norm) {
    case "easy":
      return "badge badge-green";
    case "medium":
      return "badge badge-amber";
    case "hard":
      return "badge badge-red";
    default:
      return "badge badge-muted";
  }
}

function getStageLabel(stage: CurrentSessionStageSnapshot | null) {
  if (!stage) {
    return "Sin etapa actual";
  }

  return `#${stage.sessionStageOrder} ${stage.name}`;
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

function countTeamsAtOrBeyondSessionStage(sessionStage: LiveSessionStage, sessionTeams: LiveSessionOverviewTeam[]) {
  return sessionTeams.filter(
    (team) =>
      team.progressState === "Completed" ||
      (team.currentStage !== null && team.currentStage.sessionStageOrder >= sessionStage.sessionStageOrder)
  ).length;
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
          <span className="eyebrow">PuntuaciÃ³n y AuditorÃ­a</span>
          <h4>ClasificaciÃ³n</h4>
        </div>
        <div className="card-header-actions">
          <span className={getConnectionIndicatorClass(connectionState)}>
            <span className={getConnectionDotClass(connectionState)} />
            PuntuaciÃ³n: {connectionState.label}
          </span>
          <button className="btn btn-ghost btn-sm" disabled={isLoading} onClick={onRefresh} type="button">
            {isLoading ? "Sincronizando..." : "Actualizar"}
          </button>
        </div>
      </div>

      {error ? (
        <div className="error-banner">
          <strong>ClasificaciÃ³n no disponible.</strong>
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
          <strong>AÃºn no hay entradas de puntuaciÃ³n.</strong>
          <p>La clasificaciÃ³n aparecerÃ¡ cuando PuntuaciÃ³n y AuditorÃ­a registre crÃ©dito de etapa.</p>
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
          <span className="eyebrow">BitÃ¡cora de auditorÃ­a</span>
          <h4>LÃ­nea de tiempo</h4>
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
          <strong>LÃ­nea de tiempo no disponible.</strong>
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
          <strong>Sin eventos auditables aÃºn.</strong>
          <p>La lÃ­nea de tiempo se mantendrÃ¡ lista mientras el registro de eventos de sesiÃ³n espera eventos de puntuaciÃ³n u operador.</p>
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
};

function SessionTeamDetailPanel({
  detail,
  error,
  isLoading,
  inactivityThresholdMinutes,
  overridePendingSubmissionId,
  onInactivityThresholdChange,
  onRefresh,
  onOverrideSubmission
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
        <div className="empty-state">
          <strong>Seleccione un equipo de la sesiÃ³n.</strong>
          <p>Los detalles del operador se muestran aquÃ­ con la actividad, pistas, envÃ­os y estado de inactividad.</p>
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
          <span className="eyebrow">Detalles del equipo de sesiÃ³n</span>
          <h4>{detail.teamName}</h4>
          <span className="text-muted text-sm">{detail.participantCount} participante(s)</span>
        </div>
        <span className={detail.isInactive ? "badge badge-red" : "badge badge-green"}>
          {detail.isInactive ? "Inactivo" : "Activo"}
        </span>
      </div>

      <div className="drawer-body">
        {detail.isInactive ? (
          <div className="error-banner">
            <strong>Sin envÃ­os de evidencia recientes.</strong>
            <p>La Ãºltima actividad es mÃ¡s antigua que el lÃ­mite configurado.</p>
          </div>
        ) : null}

        <div className="row-sm">
          <div className="form-group">
            <label className="form-label">LÃ­mite de inactividad</label>
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
              <h5>Pistas y EnvÃ­os de Evidencia</h5>
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
                                aria-label="Motivo de anulaciÃ³n de validaciÃ³n"
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
                                    {overridePendingSubmissionId === item.submission.id ? "Enviando" : "Forzar aceptaciÃ³n"}
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
                                Forzar AceptaciÃ³n (Override)
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
              <p>Limpie el filtro o espere a que lleguen pistas y envÃ­os de evidencia.</p>
            </div>
          )}
        </section>
      </div>
    </aside>
  );
}

function LiveSessionOverviewDashboard({
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

  return (
    <div className="stack-lg">
      <section className="card">
        <div className="card-header">
          <div className="stack-sm">
            <span className="eyebrow">OperaciÃ³n en vivo</span>
            <h3>{overview?.name ?? liveSession.name}</h3>
            <span className="text-muted">{overview?.missionName ?? liveSession.missionName}</span>
          </div>
          <div className="card-header-actions">
            <span className={getConnectionIndicatorClass(connectionState)}>
              <span className={getConnectionDotClass(connectionState)} />
              SincronizaciÃ³n en tiempo real: {connectionState.label}
            </span>
            <button className="btn btn-ghost" disabled={isLoadingOverview} onClick={onRefreshOverview} type="button">
              {isLoadingOverview ? "Actualizando..." : "Actualizar vista"}
            </button>
          </div>
        </div>
      </section>

      <div className="split-layout-wide">
        <div className="card card-compact">
          <strong className="text-sm">Estado de la SesiÃ³n</strong>
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
          <strong className="text-sm">Ãšltima sincronizaciÃ³n</strong>
          <p className="text-lg mono">{lastSyncLabel}</p>
        </div>
      </div>

      <section className="card">
        <div className="card-header">
          <div className="stack-sm">
            <span className="eyebrow">Ciclo de vida</span>
            <h4>Control de sesiÃ³n</h4>
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
                type="button"
              >
                {lifecycleActionPending === action.action ? "Actualizando..." : action.label}
              </button>
            );
          })}
        </div>

        {overviewIsStale ? (
          <p className="text-muted text-sm">
            La conexiÃ³n en tiempo real no estÃ¡ completamente activa. La captura permanece visible y la actualizaciÃ³n manual estÃ¡ disponible.
          </p>
        ) : (
          <p className="text-muted text-sm">{connectionState.detail}</p>
        )}
      </section>

      <section className="card">
        <div className="card-header">
          <div className="stack-sm">
            <span className="eyebrow">Equipos de la SesiÃ³n</span>
            <h4>Progreso operativo</h4>
          </div>
          {isLoadingOverview ? <span className="badge badge-amber">Sincronizando</span> : null}
        </div>

        {teams.length > 0 ? (
          <div className="split-layout">
            <div className="stack-sm">
              {teams.map((team) => {
                const isSelected = team.sessionTeamId === selectedSessionTeamId;

                return (
                  <button
                    className={isSelected ? "card card-compact clickable is-selected" : "card card-compact clickable"}
                    key={team.sessionTeamId}
                    onClick={() => onSelectSessionTeam(team.sessionTeamId)}
                    type="button"
                  >
                    <div className="row-between">
                      <div className="stack-sm">
                        <strong>{team.teamName}</strong>
                        <span className="text-muted text-xs">{team.participantCount} participante(s)</span>
                      </div>
                      <span className={getProgressBadgeClass(team.progressState)}>{translateProgressState(team.progressState)}</span>
                    </div>

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

            <SessionTeamDetailPanel
              detail={sessionTeamDetail}
              error={sessionTeamDetailError}
              inactivityThresholdMinutes={inactivityThresholdMinutes}
              isLoading={isLoadingSessionTeamDetail}
              key={selectedSessionTeamId ?? "empty-session-team-detail"}
              onInactivityThresholdChange={onInactivityThresholdChange}
              onOverrideSubmission={onOverrideSubmission}
              onRefresh={onRefreshSessionTeamDetail}
              overridePendingSubmissionId={overridePendingSubmissionId}
            />
          </div>
        ) : (
          <div className="empty-state">
            <strong>No se cargaron equipos de sesiÃ³n.</strong>
            <p>Use la actualizaciÃ³n manual si el registro de participantes cambiÃ³ mientras se reconectaba en tiempo real.</p>
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
  const [inactivityThresholdMinutes, setInactivityThresholdMinutes] = useState(10);
  const [draft, setDraft] = useState<LiveSessionDraft>(createEmptyDraft);
  const [penaltyDraft, setPenaltyDraft] = useState<PenaltyDraft>(createEmptyPenaltyDraft);
  const [operationalHintDraft, setOperationalHintDraft] = useState<OperationalHintDraft>(
    createEmptyOperationalHintDraft
  );
  const [isLoadingMissions, setIsLoadingMissions] = useState(true);
  const [isLoadingMissionDetail, setIsLoadingMissionDetail] = useState(false);
  const [isLoadingLiveSessions, setIsLoadingLiveSessions] = useState(true);
  const [isLoadingLiveSessionOverview, setIsLoadingLiveSessionOverview] = useState(false);
  const [isLoadingSessionTeamDetail, setIsLoadingSessionTeamDetail] = useState(false);
  const [isLoadingRanking, setIsLoadingRanking] = useState(false);
  const [isLoadingEventLog, setIsLoadingEventLog] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isSubmittingPenalty, setIsSubmittingPenalty] = useState(false);
  const [isSubmittingHint, setIsSubmittingHint] = useState(false);
  const [overridePendingSubmissionId, setOverridePendingSubmissionId] = useState<string | null>(null);
  const [deactivatingStageId, setDeactivatingStageId] = useState<string | null>(null);
  const [lifecycleActionPending, setLifecycleActionPending] = useState<LiveSessionLifecycleAction | null>(null);
  const [sessionRealtimeConnection, setSessionRealtimeConnection] = useState<RealtimeConnectionState>({
    kind: "disconnected",
    label: "Desconectado",
    detail: "SignalR esperando la selecciÃ³n de una LiveSession."
  });
  const [scoringRealtimeConnection, setScoringRealtimeConnection] = useState<RealtimeConnectionState>({
    kind: "disconnected",
    label: "Desconectado",
    detail: "SignalR esperando la selecciÃ³n de una LiveSession."
  });
  const [rankingError, setRankingError] = useState<string | null>(null);
  const [eventLogError, setEventLogError] = useState<string | null>(null);
  const [sessionTeamDetailError, setSessionTeamDetailError] = useState<string | null>(null);
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
    liveSessions.find((liveSession) => liveSession.id === selectedLiveSessionId) ?? liveSessions[0] ?? null;
  const isSelectedLiveSessionActive =
    selectedLiveSession !== null && ["Active", "Paused"].includes(selectedLiveSession.state);
  const isSelectedLiveSessionStageDeactivationAllowed =
    selectedLiveSession !== null && ["Scheduled", "Active", "Paused"].includes(selectedLiveSession.state);

  const lifecycleActions = useMemo(() => {
    if (!selectedLiveSession) {
      return [];
    }

    const actions: LifecycleActionViewModel[] = [
      {
        action: "start",
        label: "Iniciar sesiÃ³n",
        requiresConfirmation: true,
        disabled: selectedLiveSession.state !== "Scheduled" || selectedLiveSession.registeredSessionTeamCount === 0
      },
      {
        action: "pause",
        label: "Pausar sesiÃ³n",
        requiresConfirmation: true,
        disabled: selectedLiveSession.state !== "Active"
      },
      {
        action: "resume",
        label: "Reanudar sesiÃ³n",
        requiresConfirmation: false,
        disabled: selectedLiveSession.state !== "Paused"
      },
      {
        action: "finalize",
        label: "Finalizar sesiÃ³n",
        requiresConfirmation: true,
        disabled: !["Active", "Paused"].includes(selectedLiveSession.state)
      },
      {
        action: "cancel",
        label: "Cancelar sesiÃ³n",
        requiresConfirmation: true,
        disabled: !["Scheduled", "Active", "Paused"].includes(selectedLiveSession.state)
      }
    ];

    return actions;
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
      .map((missionStageId) => selectedMission.missionStages.find((missionStage) => missionStage.id === missionStageId))
      .filter((missionStage): missionStage is EligibleMissionStage => missionStage !== undefined)
      .map((missionStage, index) => ({
        ...missionStage,
        draftSessionStageOrder: index + 1
      }));
  }, [selectedMission, selectedMissionStageIds]);

  const selectedLiveSessionStages = selectedLiveSession?.sessionStageFlow ?? [];
  const hasSingleSelectedLiveSessionStage = selectedLiveSessionStages.length <= 1;
  const isSelectedLiveSessionOverviewCurrent =
    selectedLiveSession !== null && selectedLiveSessionOverview?.liveSessionId === selectedLiveSession.id;
  const selectedLiveSessionOverviewTeams = useMemo(
    () => (isSelectedLiveSessionOverviewCurrent ? selectedLiveSessionOverview.sessionTeams : []),
    [isSelectedLiveSessionOverviewCurrent, selectedLiveSessionOverview]
  );
  const selectedRankingItems =
    selectedLiveSessionRanking &&
    selectedLiveSessionRanking.liveSessionId === selectedLiveSession?.id
      ? selectedLiveSessionRanking.items
      : [];
  const selectedEventLogItems = selectedLiveSession
    ? selectedLiveSessionEventLog.filter((eventLog) => eventLog.liveSessionId === selectedLiveSession.id)
    : [];
  const pendingLiveSessionStageCount = selectedLiveSessionStages.filter(
    (sessionStage) => getSessionStageOperationalStatus(sessionStage, selectedLiveSessionOverviewTeams) === "Pending"
  ).length;
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

        const payload = (await response.json()) as EligibleMissionSummary[];
        setMissions(payload);

        const nextMissionId =
          preferredMissionId && payload.some((mission) => mission.id === preferredMissionId)
            ? preferredMissionId
            : payload[0]?.id ?? null;

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
            : payload[0]?.id ?? null;

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
        setSessionTeamDetailError(error instanceof Error ? error.message : "No se pudieron cargar los detalles del equipo de sesiÃ³n.");
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
        setEventLogError(error instanceof Error ? error.message : "No se pudo cargar el registro de eventos de la sesiÃ³n.");
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

        const mission = (await response.json()) as EligibleMissionDetail;
        setSelectedMission(mission);
        setSelectedMissionStageIds(mission.missionStages.map((missionStage) => missionStage.id));
        setDraft((current) => ({
          name: current.name.trim() ? current.name : `${mission.name} / EjecuciÃ³n programada`,
          scheduledStartAtLocal: current.scheduledStartAtLocal
        }));
      } catch (error) {
        setErrorMessage(error instanceof Error ? error.message : "No se pudieron cargar los detalles de la misiÃ³n elegible.");
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

  const refreshSelectedSessionTeamDetail = useCallback(() => {
    if (!selectedLiveSessionId || !selectedSessionTeamId) {
      return;
    }

    void loadSessionTeamDetail(selectedLiveSessionId, selectedSessionTeamId, inactivityThresholdMinutes);
  }, [inactivityThresholdMinutes, loadSessionTeamDetail, selectedLiveSessionId, selectedSessionTeamId]);

  function handleSelectSessionTeam(sessionTeamId: string) {
    setSelectedSessionTeamId(sessionTeamId);
    setSelectedSessionTeamDetail(null);
    setSessionTeamDetailError(null);
  }

  function handleInactivityThresholdChange(nextValue: number) {
    setInactivityThresholdMinutes(normalizeInactivityThresholdMinutes(nextValue));
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
            detail: "SignalR esperando la selecciÃ³n de una LiveSession."
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
        detail: "TransmisiÃ³n en tiempo real perdida. La captura permanece visible."
      });
    });

    connection.onreconnected(() => {
      if (!active) {
        return;
      }

      setSessionRealtimeConnection({
        kind: "connected",
        label: "Conectado",
        detail: "TransmisiÃ³n de sesiÃ³n en tiempo real restaurada."
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
        detail: error ? `SignalR cerrado: ${error.message}` : "TransmisiÃ³n de sesiÃ³n en tiempo real cerrada."
      });
    });

    queueMicrotask(() => {
      if (active) {
        setSessionRealtimeConnection({
          kind: "connecting",
          label: "Reconectando",
          detail: "Abriendo la transmisiÃ³n de sesiÃ³n en tiempo real."
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
          detail: "TransmisiÃ³n de sesiÃ³n en tiempo real conectada."
        });
      },
      (error: unknown) => {
        if (!active) {
          return;
        }

        setSessionRealtimeConnection({
          kind: "error",
          label: "Desconectado",
          detail: error instanceof Error ? error.message : "No se pudo conectar a la transmisiÃ³n de sesiÃ³n en tiempo real."
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
            detail: "SignalR esperando la selecciÃ³n de una LiveSession."
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
        detail: "TransmisiÃ³n de Scoring y AuditorÃ­a perdida. Los componentes mantienen la Ãºltima captura."
      });
    });

    connection.onreconnected(() => {
      if (active) {
        setScoringRealtimeConnection({
          kind: "connected",
          label: "Conectado",
          detail: "TransmisiÃ³n de Scoring y AuditorÃ­a restaurada."
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
        detail: error ? `Scoring y AuditorÃ­a cerrado: ${error.message}` : "TransmisiÃ³n de Scoring y AuditorÃ­a cerrada."
      });
    });

    queueMicrotask(() => {
      if (active) {
        setScoringRealtimeConnection({
          kind: "connecting",
          label: "Reconectando",
          detail: "Abriendo la transmisiÃ³n de Scoring y AuditorÃ­a."
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
          detail: "TransmisiÃ³n de Scoring y AuditorÃ­a conectada."
        });
      },
      (error: unknown) => {
        if (!active) {
          return;
        }

        setScoringRealtimeConnection({
          kind: "error",
          label: "Desconectado",
          detail: error instanceof Error ? error.message : "No se pudo conectar al servicio de tiempo real de Scoring y AuditorÃ­a."
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
      setErrorMessage("Seleccione una MisiÃ³n activa primero.");
      return;
    }

    if (!draft.name.trim()) {
      setErrorMessage("El nombre de la LiveSession es obligatorio.");
      return;
    }

    if (selectedMissionStageIds.length === 0) {
      setErrorMessage("El Flujo de Etapas de SesiÃ³n debe conservar al menos una Etapa de MisiÃ³n activa.");
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
        name: `${selectedMission.name} / EjecuciÃ³n de seguimiento`,
        scheduledStartAtLocal: ""
      });
      setFeedback("LiveSession programada a partir de la captura de MisiÃ³n activa.");
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
      setFeedback(`LiveSession movida a travÃ©s de la acciÃ³n de ciclo de vida: ${action}.`);
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "No se pudo actualizar el ciclo de vida de la sesiÃ³n.");
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
      setFeedback("Etapa de sesiÃ³n desactivada para esta LiveSession.");
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "No se pudo desactivar la etapa de sesiÃ³n.");
    } finally {
      setDeactivatingStageId(null);
    }
  }

  async function handleApplyPenalty(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!selectedLiveSession) {
      return;
    }

    if (!effectivePenaltySessionTeamId) {
      setErrorMessage("Seleccione un equipo de la sesiÃ³n para la penalizaciÃ³n.");
      return;
    }

    if (!penaltyDraft.reason.trim()) {
      setErrorMessage("El motivo de la penalizaciÃ³n es obligatorio.");
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
          ? `PenalizaciÃ³n aplicada con severidad ${penaltyDraft.severity}.`
          : "Comando de penalizaciÃ³n duplicado ignorado. El Ranking no ha cambiado."
      );
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "No se pudo aplicar la penalizaciÃ³n.");
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
      setFeedback("Pista liberada para los equipos de sesiÃ³n elegibles.");
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
      setFeedback("Pista operativa aÃ±adida a la captura de la LiveSession.");
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
      setFeedback("La anulaciÃ³n de validaciÃ³n aceptÃ³ el envÃ­o de evidencia de trivia.");
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "No se pudo aplicar la anulaciÃ³n de validaciÃ³n.");
    } finally {
      setOverridePendingSubmissionId(null);
    }
  }

  return (
    <div className="workspace-section">
      <div className="workspace-section-header">
        <div className="stack-sm">
          <span className="eyebrow">Operaciones de sesiÃ³n</span>
          <h2>Espacio de trabajo de programaciÃ³n de LiveSession</h2>
        </div>
        <p className="text-muted">
          El operador selecciona una MisiÃ³n activa, recorta el Flujo de Etapas de SesiÃ³n a etapas activas, reordena la ejecuciÃ³n efectiva
          y persiste una captura de LiveSession Programada sin modificar el DiseÃ±o de la MisiÃ³n.
        </p>
      </div>

      <div className="workspace-section-body">
        <div className="split-layout-wide">
          <div className="card card-compact">
            <strong className="text-sm">Misiones elegibles</strong>
            <p className="text-lg">{missionSummary.totalMissions}</p>
          </div>
          <div className="card card-compact">
            <strong className="text-sm">Etapas de misiÃ³n activas</strong>
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
                <span className="eyebrow">Origen de la misiÃ³n</span>
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
                <strong>Ninguna misiÃ³n activa puede iniciar una LiveSession todavÃ­a.</strong>
                <p>El DiseÃ±o de la MisiÃ³n debe exponer al menos una Etapa de MisiÃ³n activa antes de que sea posible programar.</p>
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
                  <p className={getDifficultyBadgeClass(mission.difficulty)}>{translateDifficulty(mission.difficulty)}</p>
                  <div className="detail-panel">
                    <div className="detail-row">
                      <span className="detail-label">Tipo de catÃ¡logo</span>
                      <span className="detail-value">{translateGameType(mission.gameType)}</span>
                    </div>
                    <div className="detail-row">
                      <span className="detail-label">DuraciÃ³n</span>
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

            {isLoadingMissionDetail ? <p className="loading-center">Cargando flujo de etapas de la misiÃ³n.</p> : null}

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
                    <label className="form-label">Captura de la misiÃ³n</label>
                    <input className="form-input" disabled value={`${selectedMission.missionStages.length} etapas activas`} />
                    <span className="form-hint">
                      La misiÃ³n sigue siendo reutilizable. La LiveSession almacena su propia captura efectiva del Flujo de Etapas de SesiÃ³n.
                    </span>
                  </div>
                </div>

                <section className="card-section">
                  <div className="card-header">
                    <div className="stack-sm">
                      <span className="eyebrow">Flujo de etapas de sesiÃ³n</span>
                      <h4>Etapas de misiÃ³n elegibles</h4>
                    </div>
                  </div>

                  <div className="stage-flow">
                    {selectedMission.missionStages.map((missionStage) => {
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
                      <h3>Flujo efectivo de etapas de sesiÃ³n</h3>
                    </div>
                  </div>

                  {draftPreview.length === 0 ? (
                    <div className="empty-state">
                      <strong>Ninguna etapa de misiÃ³n activa seleccionada.</strong>
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
                            {missionStage.hints.length} pistas copiadas a la captura de la sesiÃ³n.
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
                <strong>Seleccione una misiÃ³n elegible.</strong>
                <p>La programaciÃ³n del operador comienza a partir de una captura de MisiÃ³n activa expuesta por el DiseÃ±o de MisiÃ³n.</p>
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
              <strong>Ninguna LiveSession programada todavÃ­a.</strong>
              <p>La primera creaciÃ³n exitosa aparecerÃ¡ aquÃ­ con la captura persistida del Flujo de Etapas de SesiÃ³n.</p>
            </div>
          ) : null}

          <div className="split-layout">
            <section className="stack-sm">
              {liveSessions.map((liveSession) => (
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
            </section>

            <section className="card">
              <div className="card-header">
                <div className="stack-sm">
                  <span className="eyebrow">Detalle de la captura</span>
                  <h3>{selectedLiveSession?.name ?? "LiveSession programada"}</h3>
                </div>
                {selectedLiveSession ? <span className={getSessionStateBadgeClass(selectedLiveSession.state)}>{translateSessionState(selectedLiveSession.state)}</span> : null}
              </div>

              {selectedLiveSession ? (
                <>
                  {selectedLiveSession.state !== "Scheduled" ? (
                    <LiveSessionOverviewDashboard
                      connectionState={sessionRealtimeConnection}
                      eventLogError={eventLogError}
                      eventLogItems={selectedEventLogItems}
                      isLoadingEventLog={isLoadingEventLog}
                      isLoadingOverview={isLoadingLiveSessionOverview}
                      isLoadingRanking={isLoadingRanking}
                      isLoadingSessionTeamDetail={isLoadingSessionTeamDetail}
                      lifecycleActionPending={lifecycleActionPending}
                      lifecycleActions={lifecycleActions}
                      liveSession={selectedLiveSession}
                      inactivityThresholdMinutes={inactivityThresholdMinutes}
                      onLifecycleAction={(action) => {
                        void handleLifecycleAction(action);
                      }}
                      onInactivityThresholdChange={handleInactivityThresholdChange}
                      onOverrideSubmission={(submissionId, reason) => {
                        void handleOverrideSubmission(submissionId, reason);
                      }}
                      onRefreshEventLog={() => {
                        void loadLiveSessionEventLog(selectedLiveSession.id);
                      }}
                      onRefreshOverview={refreshSelectedOverview}
                      onRefreshRanking={() => {
                        void loadLiveSessionRanking(selectedLiveSession.id);
                      }}
                      onRefreshSessionTeamDetail={refreshSelectedSessionTeamDetail}
                      onSelectSessionTeam={handleSelectSessionTeam}
                      overridePendingSubmissionId={overridePendingSubmissionId}
                      overview={isSelectedLiveSessionOverviewCurrent ? selectedLiveSessionOverview : null}
                      rankingError={rankingError}
                      rankingItems={selectedRankingItems}
                      scoringConnectionState={scoringRealtimeConnection}
                      selectedSessionTeamId={selectedSessionTeamId}
                      sessionTeamDetail={selectedSessionTeamDetail}
                      sessionTeamDetailError={sessionTeamDetailError}
                    />
                  ) : null}

                  {selectedLiveSession.state === "Scheduled" ? (
                    <>
                      <div className="detail-panel">
                        <div className="detail-row">
                          <span className="detail-label">MisiÃ³n</span>
                          <span className="detail-value">{selectedLiveSession.missionName}</span>
                        </div>
                        <div className="detail-row">
                          <span className="detail-label">Inicio programado</span>
                          <span className="detail-value">{formatTimestamp(selectedLiveSession.scheduledStartAtUtc)}</span>
                        </div>
                        <div className="detail-row">
                          <span className="detail-label">Creado el</span>
                          <span className="detail-value">{formatTimestamp(selectedLiveSession.createdAtUtc)}</span>
                        </div>
                        <div className="detail-row">
                          <span className="detail-label">CÃ³digo de uniÃ³n</span>
                          <span className="detail-value mono">{selectedLiveSession.joinCode ?? "No generado aÃºn"}</span>
                        </div>
                        <div className="detail-row">
                          <span className="detail-label">Ventana de inscripciÃ³n</span>
                          <span className="detail-value">
                            {selectedLiveSession.enrollmentWindowOpenedAtUtc
                              ? selectedLiveSession.enrollmentWindowClosedAtUtc
                                ? `Cerrado el ${formatTimestamp(selectedLiveSession.enrollmentWindowClosedAtUtc)}`
                                : `Abierto desde ${formatTimestamp(selectedLiveSession.enrollmentWindowOpenedAtUtc)}`
                              : "No abierto"}
                          </span>
                        </div>
                        <div className="detail-row">
                          <span className="detail-label">Equipos registrados</span>
                          <span className="detail-value">{selectedLiveSession.registeredSessionTeamCount}</span>
                        </div>
                      </div>

                      <div className="row-sm row-wrap">
                        {lifecycleActions.map((action) => {
                          let btnClass = "btn btn-ghost";
                          if (action.action === "start") btnClass = "btn btn-success";
                          if (action.action === "resume") btnClass = "btn btn-success";
                          if (action.action === "finalize") btnClass = "btn btn-primary";
                          if (action.action === "cancel") btnClass = "btn btn-danger";

                          return (
                            <button
                              className={btnClass}
                              disabled={action.disabled || lifecycleActionPending !== null}
                              key={action.action}
                              onClick={() => {
                                void handleLifecycleAction(action.action);
                              }}
                              type="button"
                            >
                              {lifecycleActionPending === action.action ? "Actualizando..." : action.label}
                            </button>
                          );
                        })}
                      </div>

                      {selectedLiveSession.state === "Scheduled" && selectedLiveSession.registeredSessionTeamCount === 0 ? (
                        <p className="info-banner">
                          El inicio permanece bloqueado hasta que el registro de sesiÃ³n registre al menos un equipo de sesiÃ³n.
                        </p>
                      ) : null}

                      <div className="stage-flow">
                        {selectedLiveSession.sessionStageFlow.map((missionStage) => {
                          const stageStatus = getSessionStageOperationalStatus(
                            missionStage,
                            selectedLiveSessionOverviewTeams
                          );
                          const isStageCompleted = stageStatus === "Completed";
                          const teamsAtOrBeyondStage = countTeamsAtOrBeyondSessionStage(
                            missionStage,
                            selectedLiveSessionOverviewTeams
                          );
                          const disableDeactivation =
                            !isSelectedLiveSessionStageDeactivationAllowed ||
                            !isSelectedLiveSessionOverviewCurrent ||
                            isLoadingLiveSessionOverview ||
                            isStageCompleted ||
                            hasSingleSelectedLiveSessionStage ||
                            pendingLiveSessionStageCount <= 1 ||
                            deactivatingStageId !== null;

                          return (
                            <div className={isStageCompleted ? "stage-item is-deactivated" : "stage-item"} key={missionStage.missionStageId}>
                              <div className="stage-item-info">
                                <strong className="stage-name">
                                  <span className="stage-number">#{missionStage.sessionStageOrder}</span> {missionStage.name}
                                </strong>
                                <div className="row-sm">
                                  <span className="badge badge-blue">{translateGameType(missionStage.gameType)}</span>
                                  <span className={isStageCompleted ? "badge badge-accent" : "badge badge-amber"}>
                                    {isStageCompleted ? "Completada" : "Pendiente"}
                                  </span>
                                </div>
                              </div>
                              <span className="stage-meta">
                                Orden de origen {missionStage.sourceOrder}. {missionStage.resolvedTimeBudgetMinutes} min.{" "}
                                {missionStage.hints.length} pistas en la captura. {teamsAtOrBeyondStage} equipos en esta etapa o
                                mÃ¡s adelante.
                              </span>
                              <p>{missionStage.prompt}</p>
                              <div className="row-sm">
                                <button
                                  className="btn btn-danger btn-sm"
                                  disabled={disableDeactivation}
                                  onClick={() => void handleDeactivateStage(missionStage.missionStageId)}
                                  type="button"
                                >
                                  {deactivatingStageId === missionStage.missionStageId ? "Desactivando..." : "Desactivar Etapa"}
                                </button>
                              </div>
                            </div>
                          );
                        })}
                      </div>
                    </>
                  ) : null}

                  {isSelectedLiveSessionActive ? (
                    <section className="card-section">
                      <div className="card-header">
                        <div className="stack-sm">
                          <span className="eyebrow">Scoring y AuditorÃ­a</span>
                          <h3>Ranking de Equipos</h3>
                        </div>
                        {isLoadingRanking ? <span className="badge badge-amber">Sincronizando</span> : null}
                      </div>

                      {selectedRankingItems.length > 0 ? (
                        <div className="table-wrap">
                          <table className="ranking-table">
                            <thead>
                              <tr>
                                <th>Puesto</th>
                                <th>Equipo</th>
                                <th>Puntaje</th>
                                <th>Tiempo de resoluciÃ³n</th>
                              </tr>
                            </thead>
                            <tbody>
                              {selectedRankingItems.map((entry, index) => {
                                const previousEntry = selectedRankingItems[index - 1];
                                const isSharedRank = previousEntry?.rank === entry.rank;

                                return (
                                  <tr key={entry.sessionTeamId}>
                                    <td>
                                      <span className="rank-number">#{entry.rank}</span>
                                    </td>
                                    <td>
                                      <strong>{findRankingTeamName(entry.sessionTeamId, selectedLiveSessionOverviewTeams)}</strong>
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
                      ) : (
                        <div className="empty-state">
                          <strong>Sin entradas de puntuaciÃ³n todavÃ­a.</strong>
                          <p>El Ranking aparecerÃ¡ cuando Scoring y AuditorÃ­a registre crÃ©dito de etapa.</p>
                        </div>
                      )}
                    </section>
                  ) : null}

                  {isSelectedLiveSessionActive ? (
                    <section className="card-section">
                      <div className="card-header">
                        <div className="stack-sm">
                          <span className="eyebrow">AplicaciÃ³n de Penalizaciones</span>
                          <h3>Penalizar Equipo de la SesiÃ³n</h3>
                        </div>
                        <span className="badge badge-amber">SincronizaciÃ³n de Ranking</span>
                      </div>

                      <form className="stack" onSubmit={handleApplyPenalty}>
                        <div className="form-group">
                          <label className="form-label">Equipo de la sesiÃ³n</label>
                          <select
                            className="form-select"
                            onChange={(event) =>
                              setPenaltyDraft((current) => ({
                                ...current,
                                sessionTeamId: event.target.value
                              }))
                            }
                            required
                            value={effectivePenaltySessionTeamId}
                          >
                            <option value="" disabled>
                              Seleccionar equipo de la sesiÃ³n
                            </option>
                            {selectedLiveSessionOverviewTeams.map((team) => (
                              <option key={team.sessionTeamId} value={team.sessionTeamId}>
                                {team.teamName}
                              </option>
                            ))}
                          </select>
                        </div>

                        <div className="form-group">
                          <label className="form-label">Severidad</label>
                          <select
                            className="form-select"
                            onChange={(event) =>
                              setPenaltyDraft((current) => ({
                                ...current,
                                severity: event.target.value as PenaltySeverity
                              }))
                            }
                            value={penaltyDraft.severity}
                          >
                            <option value="Minor">Menor (-50)</option>
                            <option value="Major">Mayor (-100)</option>
                            <option value="Critical">CrÃ­tica (-200)</option>
                          </select>
                        </div>

                        <div className="form-group">
                          <label className="form-label">Motivo obligatorio</label>
                          <textarea
                            className="form-textarea"
                            maxLength={500}
                            onChange={(event) =>
                              setPenaltyDraft((current) => ({
                                ...current,
                                reason: event.target.value
                              }))
                            }
                            required
                            value={penaltyDraft.reason}
                          />
                        </div>

                        <p className="form-hint">
                          ID del comando de penalizaciÃ³n: <code>{penaltyDraft.commandId}</code>
                        </p>

                        <div className="form-actions">
                          <button
                            className="btn btn-danger"
                            disabled={isSubmittingPenalty || !isSelectedLiveSessionOverviewCurrent}
                            type="submit"
                          >
                            {isSubmittingPenalty ? "Aplicando..." : "Aplicar penalizaciÃ³n"}
                          </button>
                        </div>
                      </form>
                    </section>
                  ) : null}

                  {isSelectedLiveSessionActive ? (
                    <section className="card-section">
                      <div className="card-header">
                        <div className="stack-sm">
                          <span className="eyebrow">OperaciÃ³n en vivo</span>
                          <h3>GestiÃ³n de Pistas (Hints)</h3>
                        </div>
                        {isLoadingLiveSessionOverview ? <span className="badge badge-green">Sincronizando</span> : null}
                      </div>

                      <section className="card-section">
                        <div className="stack-sm">
                          <span className="eyebrow">Equipos</span>
                          <h4>Pistas liberadas</h4>
                        </div>

                        {selectedLiveSessionOverview?.sessionTeams.length ? (
                          <div className="stack-sm">
                            {selectedLiveSessionOverview.sessionTeams.map((team) => {
                              const releasedHints = getReleasedHintsForTeam(team);

                              return (
                                <div className="card card-compact" key={team.sessionTeamId}>
                                  <div className="row-between">
                                    <div className="stack-sm">
                                      <strong>{team.teamName}</strong>
                                      <span className="text-muted text-xs">
                                        {team.currentStage?.name ?? "Sin etapa actual"} Â· {team.progressState}
                                      </span>
                                    </div>
                                    <span className="badge badge-green">{releasedHints.length} pistas</span>
                                  </div>

                                  {releasedHints.length === 0 ? (
                                    <p className="text-muted text-xs">Sin pistas liberadas para este equipo.</p>
                                  ) : (
                                    <div className="detail-panel">
                                      {releasedHints.map((hint) => (
                                        <div className="detail-row" key={`${team.sessionTeamId}-${hint.hintId}`}>
                                          <span className="detail-label">{hint.unlockReason}</span>
                                          <span className="detail-value">
                                            {hint.content} Â· {formatTimestamp(hint.unlockedAtUtc)}
                                          </span>
                                        </div>
                                      ))}
                                    </div>
                                  )}
                                </div>
                              );
                            })}
                          </div>
                        ) : (
                          <div className="empty-state">
                            <strong>No hay equipos cargados.</strong>
                            <p>Refresca la vista general de la LiveSession para ver estados de pistas por equipo.</p>
                          </div>
                        )}
                      </section>

                      <section className="card-section">
                        <div className="stack-sm">
                          <span className="eyebrow">LiberaciÃ³n</span>
                          <h4>Pistas disponibles por etapa actual</h4>
                        </div>

                        <div className="stage-flow">
                          {selectedLiveSession.sessionStageFlow.map((missionStage) => {
                            const eligibleTeams =
                              selectedLiveSessionOverview?.sessionTeams.filter(
                                (team) => team.currentStage?.missionStageId === missionStage.missionStageId
                              ) ?? [];

                            return (
                              <div className="stage-item" key={`hint-release-${missionStage.missionStageId}`}>
                                <div className="stage-item-info">
                                  <div className="stack-sm">
                                    <strong className="stage-name">{missionStage.name}</strong>
                                    <span className="stage-meta">
                                      {eligibleTeams.length} equipos elegibles Â· {missionStage.hints.length} pistas
                                    </span>
                                  </div>
                                  <span className="badge badge-blue">{translateGameType(missionStage.gameType)}</span>
                                </div>

                                {missionStage.hints.length === 0 ? (
                                  <p className="text-muted text-xs">Esta etapa aÃºn no tiene pistas disponibles.</p>
                                ) : (
                                  <div className="stack-sm">
                                    {missionStage.hints.map((hint) => (
                                      <div className="card card-compact" key={hint.id}>
                                        <div className="stack-sm">
                                          <strong>{hint.content}</strong>
                                          <span className="text-muted text-xs">
                                            {hint.latitude !== null && hint.longitude !== null
                                              ? `${hint.latitude}, ${hint.longitude}`
                                              : "Sin coordenadas"}
                                          </span>
                                        </div>

                                        <div className="row-sm row-wrap">
                                          <button
                                            className="btn btn-ghost btn-sm"
                                            disabled={isSubmittingHint || eligibleTeams.length === 0}
                                            onClick={() => void handleReleaseHint(hint.id, null)}
                                            type="button"
                                          >
                                            Liberar a todos los elegibles
                                          </button>

                                          {eligibleTeams.map((team) => {
                                            const alreadyReleased = getReleasedHintsForTeam(team).some(
                                              (releasedHint) => releasedHint.hintId === hint.id
                                            );

                                            return (
                                              <button
                                                className="btn btn-ghost btn-sm"
                                                disabled={isSubmittingHint || alreadyReleased}
                                                key={`${hint.id}-${team.sessionTeamId}`}
                                                onClick={() => void handleReleaseHint(hint.id, team.sessionTeamId)}
                                                type="button"
                                              >
                                                Liberar a Equipo {team.teamName}
                                              </button>
                                            );
                                          })}
                                        </div>
                                      </div>
                                    ))}
                                  </div>
                                )}
                              </div>
                            );
                          })}
                        </div>
                      </section>

                      <form className="stack" onSubmit={handleCreateOperationalHint}>
                        <div className="stack-sm">
                          <span className="eyebrow">Pista en vivo</span>
                          <h4>AÃ±adir Pista Operativa</h4>
                        </div>

                        <div className="form-group">
                          <label className="form-label">Etapa de sesiÃ³n</label>
                          <select
                            className="form-select"
                            onChange={(event) =>
                              setOperationalHintDraft((current) => ({
                                ...current,
                                missionStageId: event.target.value
                              }))
                            }
                            value={operationalHintStageId}
                          >
                            {selectedLiveSession.sessionStageFlow.map((missionStage) => (
                              <option key={missionStage.missionStageId} value={missionStage.missionStageId}>
                                #{missionStage.sessionStageOrder} {missionStage.name}
                              </option>
                            ))}
                          </select>
                        </div>

                        <div className="form-group">
                          <label className="form-label">Texto de la pista</label>
                          <textarea
                            className="form-textarea"
                            maxLength={500}
                            onChange={(event) =>
                              setOperationalHintDraft((current) => ({
                                ...current,
                                content: event.target.value
                              }))
                            }
                            required
                            value={operationalHintDraft.content}
                          />
                        </div>

                        <div className="form-row">
                          <div className="form-group">
                            <label className="form-label">Latitud</label>
                            <input
                              className="form-input"
                              inputMode="decimal"
                              onChange={(event) =>
                                setOperationalHintDraft((current) => ({
                                  ...current,
                                  latitude: event.target.value
                                }))
                              }
                              value={operationalHintDraft.latitude}
                            />
                          </div>

                          <div className="form-group">
                            <label className="form-label">Longitud</label>
                            <input
                              className="form-input"
                              inputMode="decimal"
                              onChange={(event) =>
                                setOperationalHintDraft((current) => ({
                                  ...current,
                                  longitude: event.target.value
                                }))
                              }
                              value={operationalHintDraft.longitude}
                            />
                          </div>
                        </div>

                        <div className="form-actions">
                          <button className="btn btn-primary" disabled={isSubmittingHint} type="submit">
                            {isSubmittingHint ? "Guardando..." : "Guardar pista operativa"}
                          </button>
                        </div>
                      </form>
                    </section>
                  ) : null}
                </>
              ) : (
                <div className="empty-state">
                  <strong>Seleccione una LiveSession programada.</strong>
                  <p>El detalle de la captura muestra el flujo de etapas de la sesiÃ³n copiado de la misiÃ³n de origen.</p>
                </div>
              )}
            </section>
          </div>
        </section>
      </div>
    </div>
  );
}
