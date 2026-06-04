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
    return "Not scheduled";
  }

  const timestamp = new Date(value);
  if (Number.isNaN(timestamp.getTime())) {
    return value;
  }

  return timestamp.toISOString().replace("T", " ").replace(/\.\d{3}Z$/, " UTC");
}

function formatShortTimestamp(value: string | null | undefined) {
  if (!value) {
    return "No sync yet";
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
    return "Now";
  }

  if (elapsedSeconds < 60) {
    return `${elapsedSeconds}s ago`;
  }

  const elapsedMinutes = Math.floor(elapsedSeconds / 60);
  if (elapsedMinutes < 60) {
    return `${elapsedMinutes}m ago`;
  }

  const elapsedHours = Math.floor(elapsedMinutes / 60);
  if (elapsedHours < 24) {
    return `${elapsedHours}h ago`;
  }

  const elapsedDays = Math.floor(elapsedHours / 24);
  return `${elapsedDays}d ago`;
}

function formatRemainingSeconds(value: number | null | undefined) {
  if (value === null || value === undefined) {
    return "No timer";
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
    return "Unknown";
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
    `Session Team ${sessionTeamId.slice(0, 8)}`
  );
}

function parseOptionalCoordinate(value: string) {
  if (!value.trim()) {
    return null;
  }

  const parsed = Number(value);
  if (!Number.isFinite(parsed)) {
    throw new Error("Coordinates must be numeric values.");
  }

  return parsed;
}

function normalizeInactivityThresholdMinutes(value: number) {
  return Number.isFinite(value) ? Math.max(1, Math.floor(value)) : 10;
}

function getReleasedHintsForTeam(team: LiveSessionOverviewTeam) {
  return team.releasedHints ?? [];
}

function getConnectionSignalClass(connectionState: RealtimeConnectionState) {
  if (connectionState.kind === "connected") {
    return "status-pill status-ok";
  }

  if (connectionState.kind === "connecting" || connectionState.kind === "reconnecting") {
    return "status-pill status-loading";
  }

  return "status-pill status-error";
}

function getSessionStatePillClass(sessionState: string) {
  if (["Active", "Running"].includes(sessionState)) {
    return "status-pill status-ok";
  }

  if (sessionState === "Paused") {
    return "status-pill status-loading";
  }

  if (["Cancelled", "Canceled"].includes(sessionState)) {
    return "status-pill status-error";
  }

  return "status-pill status-ok";
}

function getProgressPillClass(progressState: string) {
  if (progressState === "Completed") {
    return "status-pill status-ok";
  }

  if (progressState === "NotStarted") {
    return "status-pill status-loading";
  }

  return "status-pill status-ok";
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
    <section className="overview-slot scoring-widget">
      <div className="widget-header">
        <div>
          <p className="eyebrow">Scoring and Audit</p>
          <h4>Ranking</h4>
        </div>
        <div className="widget-header-actions">
          <span className={getConnectionSignalClass(connectionState)}>Scoring: {connectionState.label}</span>
          <button className="ghost-button compact-button" disabled={isLoading} onClick={onRefresh} type="button">
            {isLoading ? "Syncing" : "Refresh"}
          </button>
        </div>
      </div>

      {error ? (
        <div className="empty-state degraded-state">
          <strong>Ranking unavailable.</strong>
          <p>{error}</p>
        </div>
      ) : null}

      {!error && rankingItems.length > 0 ? (
        <div className="ranking-table-wrap">
          <table className="ranking-table">
            <thead>
              <tr>
                <th>Rank</th>
                <th>Team</th>
                <th>Points</th>
                <th>Time</th>
              </tr>
            </thead>
            <tbody>
              {rankingItems.map((entry, index) => {
                const previousEntry = rankingItems[index - 1];
                const isSharedRank = previousEntry?.rank === entry.rank;

                return (
                  <tr key={entry.sessionTeamId}>
                    <td>
                      <span className="status-pill status-ok">#{entry.rank}</span>
                    </td>
                    <td>
                      <strong>{findRankingTeamName(entry.sessionTeamId, teams)}</strong>
                      {isSharedRank ? <p className="field-hint">Shared rank</p> : null}
                    </td>
                    <td>{entry.visibleScore} pts</td>
                    <td>{formatResolutionTime(entry.resolutionTime)}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      ) : null}

      {!error && !isLoading && rankingItems.length === 0 ? (
        <div className="empty-state">
          <strong>No Score Entries yet.</strong>
          <p>Ranking appears when Scoring and Audit records Stage Credit for a Session Team.</p>
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
    <section className="overview-slot scoring-widget">
      <div className="widget-header">
        <div>
          <p className="eyebrow">Audit Log</p>
          <h4>Timeline</h4>
        </div>
        <div className="widget-header-actions">
          <span className={getConnectionSignalClass(connectionState)}>Events: {connectionState.label}</span>
          <button className="ghost-button compact-button" disabled={isLoading} onClick={onRefresh} type="button">
            {isLoading ? "Syncing" : "Refresh"}
          </button>
        </div>
      </div>

      {error ? (
        <div className="empty-state degraded-state">
          <strong>Timeline unavailable.</strong>
          <p>{error}</p>
        </div>
      ) : null}

      {!error && eventLogItems.length > 0 ? (
        <ol className="timeline-list">
          {eventLogItems.map((eventLog) => (
            <li className="timeline-item" key={eventLog.id}>
              <span className="timeline-marker" aria-hidden="true" />
              <div className="timeline-content">
                <div className="timeline-meta">
                  <span className="node-chip">{eventLog.eventType}</span>
                  <time dateTime={eventLog.timestamp}>{formatRelativeTimestamp(eventLog.timestamp)}</time>
                </div>
                <p>{eventLog.description}</p>
                <span className="field-hint">{formatTimestamp(eventLog.timestamp)}</span>
              </div>
            </li>
          ))}
        </ol>
      ) : null}

      {!error && !isLoading && eventLogItems.length === 0 ? (
        <div className="empty-state">
          <strong>No auditable events yet.</strong>
          <p>The timeline remains ready while the Session Event Log waits for scoring or operator events.</p>
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
      <aside className="team-detail-panel">
        <div className="empty-state">
          <strong>Select a Session Team.</strong>
          <p>Operator detail opens here with activity, hints, submissions and inactivity state.</p>
        </div>
        {error ? (
          <div className="empty-state degraded-state">
            <strong>Team detail unavailable.</strong>
            <p>{error}</p>
          </div>
        ) : null}
      </aside>
    );
  }

  return (
    <aside className="team-detail-panel">
      <div className="team-detail-header">
        <div>
          <p className="eyebrow">Session Team detail</p>
          <h4>{detail.teamName}</h4>
          <p className="field-hint">{detail.participantCount} participant(s)</p>
        </div>
        <span className={detail.isInactive ? "status-pill status-error" : "status-pill status-ok"}>
          {detail.isInactive ? "Inactive" : "Active"}
        </span>
      </div>

      {detail.isInactive ? (
        <div className="team-inactivity-alert">
          <strong>No recent Evidence Submissions.</strong>
          <p>Last activity is older than the configured threshold.</p>
        </div>
      ) : null}

      <div className="team-detail-controls">
        <label className="field">
          <span>Inactivity threshold</span>
          <input
            className="input"
            min={1}
            onChange={(event) => onInactivityThresholdChange(Number(event.target.value))}
            type="number"
            value={inactivityThresholdMinutes}
          />
        </label>
        <button className="ghost-button compact-button" disabled={isLoading} onClick={onRefresh} type="button">
          {isLoading ? "Refreshing" : "Refresh"}
        </button>
      </div>

      {error ? (
        <div className="empty-state degraded-state">
          <strong>Team detail stale.</strong>
          <p>{error}</p>
        </div>
      ) : null}

      <div className="team-detail-stage-grid">
        <article className="team-detail-stat">
          <strong>Current Stage</strong>
          <p className="team-detail-value">{getStageLabel(detail.currentStage)}</p>
        </article>
        <article className="team-detail-stat">
          <strong>Time in stage</strong>
          <p className="team-detail-value">{formatElapsedDuration(detail.currentStageStartedAtUtc, nowMs)}</p>
        </article>
        <article className="team-detail-stat">
          <strong>Progress</strong>
          <p className="team-detail-value">{detail.progressState}</p>
        </article>
      </div>

      <section className="node-subsection">
        <div className="team-detail-subheader">
          <div>
            <p className="eyebrow">Activity</p>
            <h5>Hints and Evidence Submissions</h5>
          </div>
          <label className="inline-toggle trivia-filter-toggle">
            <input
              checked={showTriviaOnly}
              onChange={(event) => setShowTriviaOnly(event.target.checked)}
              type="checkbox"
            />
            <span>Mostrar Solo Respuestas Trivia</span>
          </label>
        </div>

        {timelineItems.length > 0 ? (
          <ol className="timeline-list team-detail-timeline">
            {timelineItems.map((item) => (
              <li className="timeline-item" key={item.id}>
                <span className="timeline-marker" aria-hidden="true" />
                <div className="timeline-content team-detail-timeline-content">
                  <div className="timeline-meta">
                    <span className={item.kind === "hint" ? "node-chip is-composite" : "node-chip is-leaf"}>
                      {item.kind === "hint" ? "Hint" : item.submission.gameType}
                    </span>
                    <time dateTime={item.occurredAtUtc}>{formatRelativeTimestamp(item.occurredAtUtc)}</time>
                  </div>

                  {item.kind === "hint" ? (
                    <>
                      <p>{item.hint.content}</p>
                      <span className="field-hint">
                        {item.stageName} · {item.hint.unlockReason} · {formatTimestamp(item.hint.releasedAtUtc)}
                      </span>
                    </>
                  ) : (
                    <>
                      <div className="team-submission-header">
                        <strong>{item.submission.submittedText ?? item.submission.submittedHash ?? "Evidence submitted"}</strong>
                        <span
                          className={
                            item.submission.validationOutcome === "Accepted"
                              ? "status-pill status-ok"
                              : "status-pill status-error"
                          }
                        >
                          {item.submission.validationOutcome}
                        </span>
                      </div>
                      <span className="field-hint">
                        {item.stageName} · {item.submission.difficulty} · {formatTimestamp(item.submission.submittedAtUtc)}
                      </span>
                      {item.submission.failureReason ? <p className="field-hint">{item.submission.failureReason}</p> : null}

                      {item.submission.isTriviaCorrectionEligible ? (
                        <div className="override-control">
                          {overrideSubmissionId === item.submission.id ? (
                            <form
                              className="override-reason-panel"
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
                              aria-label="Validation Override reason"
                            >
                              <label className="field">
                                <span>Operator reason</span>
                                <textarea
                                  className="input textarea-input"
                                  maxLength={500}
                                  onChange={(event) => setOverrideReason(event.target.value)}
                                  required
                                  value={overrideReason}
                                />
                              </label>
                              <div className="mission-action-row">
                                <button
                                  className="primary-button"
                                  disabled={overridePendingSubmissionId === item.submission.id}
                                  type="submit"
                                >
                                  {overridePendingSubmissionId === item.submission.id ? "Sending" : "Force acceptance"}
                                </button>
                                <button
                                  className="ghost-button"
                                  onClick={() => {
                                    setOverrideSubmissionId(null);
                                    setOverrideReason("");
                                  }}
                                  type="button"
                                >
                                  Cancel
                                </button>
                              </div>
                            </form>
                          ) : (
                            <button
                              className="ghost-button"
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
              </li>
            ))}
          </ol>
        ) : (
          <div className="empty-state">
            <strong>No matching team activity.</strong>
            <p>Clear the filter or wait for hints and Evidence Submissions to arrive.</p>
          </div>
        )}
      </section>
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
    <div className="live-session-overview-dashboard stack-gap">
      <section className="overview-command-bar">
        <div>
          <p className="eyebrow">Live operation</p>
          <h3>{overview?.name ?? liveSession.name}</h3>
          <p className="muted-copy">{overview?.missionName ?? liveSession.missionName}</p>
        </div>

        <div className="overview-command-actions">
          <span className={getConnectionSignalClass(connectionState)}>
            Realtime Sync: {connectionState.label}
          </span>
          <button className="ghost-button" disabled={isLoadingOverview} onClick={onRefreshOverview} type="button">
            {isLoadingOverview ? "Refreshing..." : "Refresh overview"}
          </button>
        </div>
      </section>

      <div className="overview-metric-grid">
        <article className="signal-card">
          <strong>Session State</strong>
          <p className="metric-value overview-metric-value">{sessionState}</p>
        </article>
        <article className="signal-card">
          <strong>Remaining time</strong>
          <p className="metric-value overview-metric-value">{formatRemainingSeconds(remainingSeconds)}</p>
        </article>
        <article className="signal-card">
          <strong>Active teams</strong>
          <p className="metric-value overview-metric-value">{overview ? activeTeamCount : liveSession.registeredSessionTeamCount}</p>
        </article>
        <article className="signal-card">
          <strong>Last sync</strong>
          <p className="metric-value overview-metric-value">{lastSyncLabel}</p>
        </article>
      </div>

      <section className="operator-detail-card">
        <div className="mission-list-header">
          <div>
            <p className="eyebrow">Lifecycle</p>
            <h4>Control de sesion</h4>
          </div>
          <span className={getSessionStatePillClass(sessionState)}>{sessionState}</span>
        </div>

        <div className="mission-action-row">
          {lifecycleActions.map((action) => (
            <button
              className={action.action === "cancel" ? "ghost-button danger-button" : "ghost-button"}
              disabled={action.disabled || lifecycleActionPending !== null}
              key={action.action}
              onClick={() => onLifecycleAction(action.action)}
              type="button"
            >
              {lifecycleActionPending === action.action ? "Updating..." : action.label}
            </button>
          ))}
        </div>

        {overviewIsStale ? (
          <p className="field-hint">
            Realtime connection is not fully connected. Snapshot remains visible and manual refresh is available.
          </p>
        ) : (
          <p className="field-hint">{connectionState.detail}</p>
        )}
      </section>

      <section className="operator-detail-card">
        <div className="mission-list-header">
          <div>
            <p className="eyebrow">Session Teams</p>
            <h4>Operational progress</h4>
          </div>
          {isLoadingOverview ? <span className="status-pill status-loading">Syncing</span> : null}
        </div>

        {teams.length > 0 ? (
          <div className="session-team-inspection-layout">
            <div className="session-team-grid">
              {teams.map((team) => {
                const isSelected = team.sessionTeamId === selectedSessionTeamId;

                return (
                  <button
                    className={isSelected ? "session-team-card session-team-card-button is-active" : "session-team-card session-team-card-button"}
                    key={team.sessionTeamId}
                    onClick={() => onSelectSessionTeam(team.sessionTeamId)}
                    type="button"
                  >
                    <div className="mission-list-item-top">
                      <div>
                        <strong>{team.teamName}</strong>
                        <p className="field-hint">{team.participantCount} participant(s)</p>
                      </div>
                      <span className={getProgressPillClass(team.progressState)}>{team.progressState}</span>
                    </div>

                    <dl className="definition-grid session-team-definition-grid">
                      <div>
                        <dt>Current Stage</dt>
                        <dd>{getStageLabel(team.currentStage)}</dd>
                      </div>
                      <div>
                        <dt>Difficulty</dt>
                        <dd>{team.currentStage?.difficulty ?? "N/A"}</dd>
                      </div>
                      <div>
                        <dt>Game Type</dt>
                        <dd>{team.currentStage?.gameType ?? "N/A"}</dd>
                      </div>
                      <div>
                        <dt>Visible hints</dt>
                        <dd>{getReleasedHintsForTeam(team).length}</dd>
                      </div>
                    </dl>
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
            <strong>No Session Teams loaded.</strong>
            <p>Use manual refresh if enrollment changed while realtime was reconnecting.</p>
          </div>
        )}
      </section>

      <div className="overview-secondary-grid">
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
    detail: "SignalR waiting for selected LiveSession."
  });
  const [scoringRealtimeConnection, setScoringRealtimeConnection] = useState<RealtimeConnectionState>({
    kind: "disconnected",
    label: "Desconectado",
    detail: "SignalR waiting for selected LiveSession."
  });
  const [rankingError, setRankingError] = useState<string | null>(null);
  const [eventLogError, setEventLogError] = useState<string | null>(null);
  const [sessionTeamDetailError, setSessionTeamDetailError] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<string | null>(null);

  const eligibleMissionsUrl = useMemo(
    () => `${getClientConfig().edgeProxyPublicBaseUrl}/mission-design/api/mission-design/missions/eligible-for-live-session`,
    []
  );
  const sessionOperationsUrl = useMemo(
    () => `${getClientConfig().edgeProxyPublicBaseUrl}/session-operations/api/session-operations`,
    []
  );
  const liveSessionsUrl = useMemo(
    () => `${sessionOperationsUrl}/live-sessions`,
    [sessionOperationsUrl]
  );
  const sessionHubUrl = useMemo(() => getClientConfig().sessionHubUrl, []);
  const scoringAuditSessionsUrl = useMemo(
    () => `${getClientConfig().edgeProxyPublicBaseUrl}/scoring-audit/api/scoring-audit/sessions`,
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
        label: "Start session",
        requiresConfirmation: true,
        disabled: selectedLiveSession.state !== "Scheduled" || selectedLiveSession.registeredSessionTeamCount === 0
      },
      {
        action: "pause",
        label: "Pause session",
        requiresConfirmation: true,
        disabled: selectedLiveSession.state !== "Active"
      },
      {
        action: "resume",
        label: "Resume session",
        requiresConfirmation: false,
        disabled: selectedLiveSession.state !== "Paused"
      },
      {
        action: "finalize",
        label: "Finalize session",
        requiresConfirmation: true,
        disabled: !["Active", "Paused"].includes(selectedLiveSession.state)
      },
      {
        action: "cancel",
        label: "Cancel session",
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
        setErrorMessage(error instanceof Error ? error.message : "Could not load eligible Missions.");
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
        setErrorMessage(error instanceof Error ? error.message : "Could not load LiveSessions.");
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
        setErrorMessage(error instanceof Error ? error.message : "Could not load LiveSession overview.");
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
        setSessionTeamDetailError(error instanceof Error ? error.message : "Could not load Session Team detail.");
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
        setRankingError(error instanceof Error ? error.message : "Could not load Ranking.");
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
        setEventLogError(error instanceof Error ? error.message : "Could not load Session Event Log.");
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
          name: current.name.trim() ? current.name : `${mission.name} / Scheduled run`,
          scheduledStartAtLocal: current.scheduledStartAtLocal
        }));
      } catch (error) {
        setErrorMessage(error instanceof Error ? error.message : "Could not load eligible Mission detail.");
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
            detail: "SignalR waiting for selected LiveSession."
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
        detail: "Realtime stream dropped. Snapshot remains visible."
      });
    });

    connection.onreconnected(() => {
      if (!active) {
        return;
      }

      setSessionRealtimeConnection({
        kind: "connected",
        label: "Conectado",
        detail: "Realtime session stream restored."
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
        detail: error ? `SignalR closed: ${error.message}` : "Realtime session stream closed."
      });
    });

    queueMicrotask(() => {
      if (active) {
        setSessionRealtimeConnection({
          kind: "connecting",
          label: "Reconectando",
          detail: "Opening realtime session stream."
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
          detail: "Realtime session stream connected."
        });
      },
      (error: unknown) => {
        if (!active) {
          return;
        }

        setSessionRealtimeConnection({
          kind: "error",
          label: "Desconectado",
          detail: error instanceof Error ? error.message : "Could not connect realtime session stream."
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
            detail: "SignalR waiting for selected LiveSession."
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
        detail: "Scoring Audit stream dropped. Widgets keep the latest snapshot."
      });
    });

    connection.onreconnected(() => {
      if (active) {
        setScoringRealtimeConnection({
          kind: "connected",
          label: "Conectado",
          detail: "Scoring Audit stream restored."
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
        detail: error ? `Scoring Audit closed: ${error.message}` : "Scoring Audit stream closed."
      });
    });

    queueMicrotask(() => {
      if (active) {
        setScoringRealtimeConnection({
          kind: "connecting",
          label: "Reconectando",
          detail: "Opening Scoring Audit stream."
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
          detail: "Scoring Audit stream connected."
        });
      },
      (error: unknown) => {
        if (!active) {
          return;
        }

        setScoringRealtimeConnection({
          kind: "error",
          label: "Desconectado",
          detail: error instanceof Error ? error.message : "Could not connect to Scoring Audit realtime."
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
      setErrorMessage("Select an active Mission first.");
      return;
    }

    if (!draft.name.trim()) {
      setErrorMessage("LiveSession name is required.");
      return;
    }

    if (selectedMissionStageIds.length === 0) {
      setErrorMessage("Session Stage Flow must keep at least one active Mission Stage.");
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
        name: `${selectedMission.name} / Follow-up run`,
        scheduledStartAtLocal: ""
      });
      setFeedback("LiveSession scheduled from active Mission snapshot.");
      await loadLiveSessions(liveSession.id);
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "Could not create LiveSession.");
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
      !window.confirm(`Confirm ${selectedAction.label.toLowerCase()} for "${selectedLiveSession.name}"?`)
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
      setFeedback(`LiveSession moved through lifecycle action: ${action}.`);
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "Could not update Session Lifecycle.");
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
      setFeedback("Session Stage deactivated for this LiveSession.");
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "Could not deactivate Session Stage.");
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
      setErrorMessage("Select a Session Team for the Penalty.");
      return;
    }

    if (!penaltyDraft.reason.trim()) {
      setErrorMessage("Penalty reason is required.");
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
          ? `Penalty applied with severity ${penaltyDraft.severity}.`
          : "Duplicate penalty command ignored. Ranking unchanged."
      );
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "Could not apply Penalty.");
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
      setFeedback("Hint released for eligible Session Team(s).");
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "Could not release Hint.");
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
      setErrorMessage("Hint content is required.");
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
      setFeedback("Operational Hint added to the LiveSession snapshot.");
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "Could not create operational Hint.");
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
      const response = await fetch(`${sessionOperationsUrl}/submissions/${submissionId}/override`, {
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
      setFeedback("Validation Override accepted the Trivia Evidence Submission.");
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "Could not apply Validation Override.");
    } finally {
      setOverridePendingSubmissionId(null);
    }
  }

  return (
    <section className="panel stack-gap">
      <div className="section-heading">
        <div>
          <p className="eyebrow">Session operations</p>
          <h2>LiveSession scheduling workspace</h2>
        </div>
        <p className="section-copy">
          Operator selects an active Mission, trims Session Stage Flow to active stages, reorders the effective run,
          and persists a Scheduled LiveSession snapshot without mutating Mission Design.
        </p>
      </div>

      <div className="mission-summary-grid">
        <article className="signal-card">
          <strong>Eligible Missions</strong>
          <p className="metric-value">{missionSummary.totalMissions}</p>
        </article>
        <article className="signal-card">
          <strong>Active Mission Stages</strong>
          <p className="metric-value">{missionSummary.totalActiveStages}</p>
        </article>
        <article className="signal-card">
          <strong>Scheduled LiveSessions</strong>
          <p className="metric-value">{missionSummary.totalLiveSessions}</p>
        </article>
      </div>

      {errorMessage ? <p className="banner-error">{errorMessage}</p> : null}
      {feedback ? <p className="banner-success">{feedback}</p> : null}

      <div className="operator-workspace-grid">
        <section className="mission-list-panel">
          <div className="mission-list-header">
            <div>
              <p className="eyebrow">Mission source</p>
              <h3>Eligible Missions</h3>
            </div>
            <button
              className="ghost-button"
              onClick={() => {
                setIsLoadingMissions(true);
                void loadMissions(selectedMissionId ?? undefined);
              }}
              type="button"
            >
              Refresh
            </button>
          </div>

          {isLoadingMissions ? <p className="muted-copy">Loading eligible Missions.</p> : null}

          {!isLoadingMissions && missions.length === 0 ? (
            <div className="empty-state">
              <strong>No active Mission can seed a LiveSession yet.</strong>
              <p>Mission Design must expose at least one active Mission Stage before scheduling is possible.</p>
            </div>
          ) : null}

          <div className="mission-list">
            {missions.map((mission) => (
              <button
                className={mission.id === selectedMissionId ? "mission-list-item is-active" : "mission-list-item"}
                key={mission.id}
                onClick={() => {
                  setFeedback(null);
                  setSelectedMissionId(mission.id);
                }}
                type="button"
              >
                <div className="mission-list-item-top">
                  <strong>{mission.name}</strong>
                  <span className="status-pill status-ok">{mission.activeMissionStageCount} stages</span>
                </div>
                <p>{mission.difficulty}</p>
                <dl className="mission-meta-grid">
                  <div>
                    <dt>Catalog type</dt>
                    <dd>{mission.gameType}</dd>
                  </div>
                  <div>
                    <dt>Budget</dt>
                    <dd>{mission.maximumDurationMinutes} min</dd>
                  </div>
                </dl>
              </button>
            ))}
          </div>
        </section>

        <section className="mission-editor-panel">
          <div className="mission-list-header">
            <div>
              <p className="eyebrow">Create</p>
              <h3>{selectedMission?.name ?? "Scheduled LiveSession"}</h3>
            </div>
            {selectedMission ? <span className="status-pill status-ok">{draftPreview.length} selected</span> : null}
          </div>

          {isLoadingMissionDetail ? <p className="muted-copy">Loading Mission stage flow.</p> : null}

          {selectedMission ? (
            <form className="auth-form" onSubmit={handleCreateLiveSession}>
              <label className="field">
                <span>LiveSession name</span>
                <input
                  className="input"
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
              </label>

              <div className="form-grid-two">
                <label className="field">
                  <span>Scheduled start</span>
                  <input
                    className="input"
                    onChange={(event) =>
                      setDraft((current) => ({
                        ...current,
                        scheduledStartAtLocal: event.target.value
                      }))
                    }
                    type="datetime-local"
                    value={draft.scheduledStartAtLocal}
                  />
                </label>

                <label className="field">
                  <span>Mission snapshot</span>
                  <input className="input" disabled value={`${selectedMission.missionStages.length} active stages`} />
                  <span className="field-hint">
                    Mission remains reusable. LiveSession stores its own effective Session Stage Flow snapshot.
                  </span>
                </label>
              </div>

              <section className="node-subsection">
                <div className="mission-list-header">
                  <div>
                    <p className="eyebrow">Session Stage Flow</p>
                    <h4>Eligible Mission Stages</h4>
                  </div>
                </div>

                <div className="live-session-flow-list">
                  {selectedMission.missionStages.map((missionStage) => {
                    const isSelected = selectedMissionStageIds.includes(missionStage.id);
                    const selectedIndex = selectedMissionStageIds.indexOf(missionStage.id);

                    return (
                      <article className="live-session-flow-item" key={missionStage.id}>
                        <div className="mission-list-item-top">
                          <div>
                            <strong>{missionStage.name}</strong>
                            <p className="muted-copy">
                              Source order {missionStage.sourceOrder}. {missionStage.gameType}.{" "}
                              {missionStage.resolvedTimeBudgetMinutes} min.
                            </p>
                            <p>{missionStage.prompt}</p>
                          </div>
                          <span className={isSelected ? "status-pill status-ok" : "status-pill status-error"}>
                            {isSelected ? `selected #${selectedIndex + 1}` : "excluded"}
                          </span>
                        </div>

                        <div className="mission-action-row">
                          <button
                            className={isSelected ? "ghost-button danger-button" : "ghost-button"}
                            onClick={() => toggleMissionStageSelection(missionStage.id)}
                            type="button"
                          >
                            {isSelected ? "Remove from flow" : "Add to flow"}
                          </button>
                          <button
                            className="ghost-button"
                            disabled={!isSelected || selectedIndex <= 0}
                            onClick={() => moveSelectedMissionStage(missionStage.id, -1)}
                            type="button"
                          >
                            Move up
                          </button>
                          <button
                            className="ghost-button"
                            disabled={!isSelected || selectedIndex === -1 || selectedIndex >= selectedMissionStageIds.length - 1}
                            onClick={() => moveSelectedMissionStage(missionStage.id, 1)}
                            type="button"
                          >
                            Move down
                          </button>
                        </div>
                      </article>
                    );
                  })}
                </div>
              </section>

              <section className="operator-detail-card">
                <div className="mission-list-header">
                  <div>
                    <p className="eyebrow">Preview</p>
                    <h3>Effective Session Stage Flow</h3>
                  </div>
                </div>

                {draftPreview.length === 0 ? (
                  <div className="empty-state">
                    <strong>No active Mission Stage selected.</strong>
                    <p>Keep at least one stage in flow before scheduling the LiveSession.</p>
                  </div>
                ) : (
                  <div className="live-session-flow-list">
                    {draftPreview.map((missionStage) => (
                      <article className="live-session-flow-item" key={missionStage.id}>
                        <div className="mission-list-item-top">
                          <strong>
                            #{missionStage.draftSessionStageOrder} {missionStage.name}
                          </strong>
                          <span className="status-pill status-ok">{missionStage.gameType}</span>
                        </div>
                        <p className="muted-copy">
                          Source order {missionStage.sourceOrder}. {missionStage.resolvedTimeBudgetMinutes} min.{" "}
                          {missionStage.hints.length} hints copied to session snapshot.
                        </p>
                        <p>{missionStage.prompt}</p>
                      </article>
                    ))}
                  </div>
                )}
              </section>

              <div className="mission-action-row">
                <button className="primary-button" disabled={isSubmitting} type="submit">
                  {isSubmitting ? "Scheduling..." : "Create Scheduled LiveSession"}
                </button>
              </div>
            </form>
          ) : (
            <div className="empty-state">
              <strong>Select an eligible Mission.</strong>
              <p>Operator scheduling starts from an active Mission snapshot exposed by Mission Design.</p>
            </div>
          )}
        </section>
      </div>

      <section className="panel stack-gap">
        <div className="mission-list-header">
          <div>
            <p className="eyebrow">Scheduled output</p>
            <h3>Persisted LiveSessions</h3>
          </div>
          <button
            className="ghost-button"
            onClick={() => {
              setIsLoadingLiveSessions(true);
              void loadLiveSessions(selectedLiveSessionId ?? undefined);
            }}
            type="button"
          >
            Refresh
          </button>
        </div>

        {isLoadingLiveSessions ? <p className="muted-copy">Loading LiveSessions.</p> : null}

        {!isLoadingLiveSessions && liveSessions.length === 0 ? (
          <div className="empty-state">
            <strong>No LiveSession scheduled yet.</strong>
            <p>First successful creation will appear here with the persisted Session Stage Flow snapshot.</p>
          </div>
        ) : null}

        <div className="mission-workspace-grid">
          <section className="mission-list-panel">
            <div className="mission-list">
              {liveSessions.map((liveSession) => (
                <button
                  className={liveSession.id === selectedLiveSession?.id ? "mission-list-item is-active" : "mission-list-item"}
                  key={liveSession.id}
                  onClick={() => setSelectedLiveSessionId(liveSession.id)}
                  type="button"
                >
                  <div className="mission-list-item-top">
                    <strong>{liveSession.name}</strong>
                    <span className="status-pill status-ok">{liveSession.state}</span>
                  </div>
                  <p>{liveSession.missionName}</p>
                  <dl className="mission-meta-grid">
                  <div>
                    <dt>Scheduled</dt>
                    <dd>{formatTimestamp(liveSession.scheduledStartAtUtc)}</dd>
                  </div>
                  <div>
                    <dt>Teams</dt>
                    <dd>{liveSession.registeredSessionTeamCount}</dd>
                  </div>
                </dl>
              </button>
            ))}
            </div>
          </section>

          <section className="mission-editor-panel">
            <div className="mission-list-header">
              <div>
                <p className="eyebrow">Snapshot detail</p>
                <h3>{selectedLiveSession?.name ?? "Scheduled LiveSession"}</h3>
              </div>
              {selectedLiveSession ? <span className="status-pill status-ok">{selectedLiveSession.state}</span> : null}
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
                    <dl className="definition-grid">
                      <div>
                        <dt>Mission</dt>
                        <dd>{selectedLiveSession.missionName}</dd>
                      </div>
                      <div>
                        <dt>Scheduled start</dt>
                        <dd>{formatTimestamp(selectedLiveSession.scheduledStartAtUtc)}</dd>
                      </div>
                      <div>
                        <dt>Created at</dt>
                        <dd>{formatTimestamp(selectedLiveSession.createdAtUtc)}</dd>
                      </div>
                      <div>
                        <dt>Join code</dt>
                        <dd>{selectedLiveSession.joinCode ?? "Not generated yet"}</dd>
                      </div>
                      <div>
                        <dt>Enrollment window</dt>
                        <dd>
                          {selectedLiveSession.enrollmentWindowOpenedAtUtc
                            ? selectedLiveSession.enrollmentWindowClosedAtUtc
                              ? `Closed at ${formatTimestamp(selectedLiveSession.enrollmentWindowClosedAtUtc)}`
                              : `Open since ${formatTimestamp(selectedLiveSession.enrollmentWindowOpenedAtUtc)}`
                            : "Not opened"}
                        </dd>
                      </div>
                      <div>
                        <dt>Registered teams</dt>
                        <dd>{selectedLiveSession.registeredSessionTeamCount}</dd>
                      </div>
                    </dl>

                <div className="mission-action-row">
                  {lifecycleActions.map((action) => (
                    <button
                      className={action.action === "cancel" ? "ghost-button danger-button" : "ghost-button"}
                      disabled={action.disabled || lifecycleActionPending !== null}
                      key={action.action}
                      onClick={() => {
                        void handleLifecycleAction(action.action);
                      }}
                      type="button"
                    >
                      {lifecycleActionPending === action.action ? "Updating..." : action.label}
                    </button>
                  ))}
                </div>

                {selectedLiveSession.state === "Scheduled" && selectedLiveSession.registeredSessionTeamCount === 0 ? (
                  <p className="muted-copy">
                    Start stays blocked until Session Enrollment registers at least one Session Team.
                  </p>
                ) : null}

                    <div className="live-session-flow-list">
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
                          <article className="live-session-flow-item" key={missionStage.missionStageId}>
                            <div className="mission-list-item-top">
                              <strong>
                                #{missionStage.sessionStageOrder} {missionStage.name}
                              </strong>
                              <div className="mission-action-row">
                                <span className="status-pill status-ok">{missionStage.gameType}</span>
                                <span className={isStageCompleted ? "status-pill status-error" : "status-pill status-loading"}>
                                  {isStageCompleted ? "Completada" : "Pendiente"}
                                </span>
                              </div>
                            </div>
                            <p className="muted-copy">
                              Source order {missionStage.sourceOrder}. {missionStage.resolvedTimeBudgetMinutes} min.{" "}
                              {missionStage.hints.length} hints in snapshot. {teamsAtOrBeyondStage} equipos en esta etapa o
                              mas adelante.
                            </p>
                            <p>{missionStage.prompt}</p>
                            <div className="mission-action-row">
                              <button
                                className="ghost-button danger-button"
                                disabled={disableDeactivation}
                                onClick={() => void handleDeactivateStage(missionStage.missionStageId)}
                                type="button"
                              >
                                {deactivatingStageId === missionStage.missionStageId ? "Desactivando..." : "Desactivar Etapa"}
                              </button>
                            </div>
                          </article>
                        );
                      })}
                    </div>
                  </>
                ) : null}

                {isSelectedLiveSessionActive ? (
                  <section className="operator-detail-card">
                    <div className="mission-list-header">
                      <div>
                        <p className="eyebrow">Scoring and Audit</p>
                        <h3>Ranking de Equipos</h3>
                      </div>
                      {isLoadingRanking ? <span className="status-pill status-loading">Syncing</span> : null}
                    </div>

                    {selectedRankingItems.length > 0 ? (
                      <div className="ranking-table-wrap">
                        <table className="ranking-table">
                          <thead>
                            <tr>
                              <th>Rank</th>
                              <th>Equipo</th>
                              <th>Puntaje</th>
                              <th>Resolution Time</th>
                            </tr>
                          </thead>
                          <tbody>
                            {selectedRankingItems.map((entry, index) => {
                              const previousEntry = selectedRankingItems[index - 1];
                              const isSharedRank = previousEntry?.rank === entry.rank;

                              return (
                                <tr key={entry.sessionTeamId}>
                                  <td>
                                    <span className="status-pill status-ok">#{entry.rank}</span>
                                  </td>
                                  <td>
                                    <strong>{findRankingTeamName(entry.sessionTeamId, selectedLiveSessionOverviewTeams)}</strong>
                                    {isSharedRank ? <p className="field-hint">Empate conservado</p> : null}
                                  </td>
                                  <td>{entry.visibleScore} pts</td>
                                  <td>{formatResolutionTime(entry.resolutionTime)}</td>
                                </tr>
                              );
                            })}
                          </tbody>
                        </table>
                      </div>
                    ) : (
                      <div className="empty-state">
                        <strong>Sin Score Entries todavia.</strong>
                        <p>El Ranking aparecera cuando Scoring and Audit registre credito de etapa.</p>
                      </div>
                    )}
                  </section>
                ) : null}

                {isSelectedLiveSessionActive ? (
                  <section className="operator-detail-card">
                    <div className="mission-list-header">
                      <div>
                        <p className="eyebrow">Penalty Application</p>
                        <h3>Penalizar Session Team</h3>
                      </div>
                      <span className="status-pill status-loading">Ranking sync</span>
                    </div>

                    <form className="auth-form" onSubmit={handleApplyPenalty}>
                      <label className="field">
                        <span>Session Team</span>
                        <select
                          className="input"
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
                            Select Session Team
                          </option>
                          {selectedLiveSessionOverviewTeams.map((team) => (
                            <option key={team.sessionTeamId} value={team.sessionTeamId}>
                              {team.teamName}
                            </option>
                          ))}
                        </select>
                      </label>

                      <label className="field">
                        <span>Severity</span>
                        <select
                          className="input"
                          onChange={(event) =>
                            setPenaltyDraft((current) => ({
                              ...current,
                              severity: event.target.value as PenaltySeverity
                            }))
                          }
                          value={penaltyDraft.severity}
                        >
                          <option value="Minor">Minor (-50)</option>
                          <option value="Major">Major (-100)</option>
                          <option value="Critical">Critical (-200)</option>
                        </select>
                      </label>

                      <label className="field">
                        <span>Motivo obligatorio</span>
                        <textarea
                          className="input"
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
                      </label>

                      <p className="field-hint">
                        Penalty command id: <code>{penaltyDraft.commandId}</code>
                      </p>

                      <div className="mission-action-row">
                        <button
                          className="ghost-button danger-button"
                          disabled={isSubmittingPenalty || !isSelectedLiveSessionOverviewCurrent}
                          type="submit"
                        >
                          {isSubmittingPenalty ? "Aplicando..." : "Aplicar Penalty"}
                        </button>
                      </div>
                    </form>
                  </section>
                ) : null}

                {isSelectedLiveSessionActive ? (
                  <section className="operator-detail-card">
                    <div className="mission-list-header">
                      <div>
                        <p className="eyebrow">Live operation</p>
                        <h3>Gestión de Pistas (Hints)</h3>
                      </div>
                      {isLoadingLiveSessionOverview ? <span className="status-pill status-ok">Syncing</span> : null}
                    </div>

                    <section className="node-subsection">
                      <div>
                        <p className="eyebrow">Equipos</p>
                        <h4>Pistas liberadas</h4>
                      </div>

                      {selectedLiveSessionOverview?.sessionTeams.length ? (
                        <div className="hint-editor-list">
                          {selectedLiveSessionOverview.sessionTeams.map((team) => {
                            const releasedHints = getReleasedHintsForTeam(team);

                            return (
                              <article className="hint-card" key={team.sessionTeamId}>
                                <div className="mission-list-item-top">
                                  <div>
                                    <strong>{team.teamName}</strong>
                                    <p className="muted-copy">
                                      {team.currentStage?.name ?? "Sin etapa actual"} · {team.progressState}
                                    </p>
                                  </div>
                                  <span className="status-pill status-ok">{releasedHints.length} hints</span>
                                </div>

                                {releasedHints.length === 0 ? (
                                  <p className="field-hint">Sin pistas liberadas para este equipo.</p>
                                ) : (
                                  <dl className="definition-grid">
                                    {releasedHints.map((hint) => (
                                      <div key={`${team.sessionTeamId}-${hint.hintId}`}>
                                        <dt>{hint.unlockReason}</dt>
                                        <dd>
                                          {hint.content} · {formatTimestamp(hint.unlockedAtUtc)}
                                        </dd>
                                      </div>
                                    ))}
                                  </dl>
                                )}
                              </article>
                            );
                          })}
                        </div>
                      ) : (
                        <div className="empty-state">
                          <strong>No hay equipos cargados.</strong>
                          <p>Refresca el overview de la LiveSession para ver estados de pistas por equipo.</p>
                        </div>
                      )}
                    </section>

                    <section className="node-subsection">
                      <div>
                        <p className="eyebrow">Liberación</p>
                        <h4>Pistas disponibles por etapa actual</h4>
                      </div>

                      <div className="live-session-flow-list">
                        {selectedLiveSession.sessionStageFlow.map((missionStage) => {
                          const eligibleTeams =
                            selectedLiveSessionOverview?.sessionTeams.filter(
                              (team) => team.currentStage?.missionStageId === missionStage.missionStageId
                            ) ?? [];

                          return (
                            <article className="live-session-flow-item" key={`hint-release-${missionStage.missionStageId}`}>
                              <div className="mission-list-item-top">
                                <div>
                                  <strong>{missionStage.name}</strong>
                                  <p className="muted-copy">
                                    {eligibleTeams.length} equipos elegibles · {missionStage.hints.length} pistas
                                  </p>
                                </div>
                                <span className="status-pill status-ok">{missionStage.gameType}</span>
                              </div>

                              {missionStage.hints.length === 0 ? (
                                <p className="field-hint">Esta etapa aún no tiene pistas disponibles.</p>
                              ) : (
                                <div className="hint-editor-list">
                                  {missionStage.hints.map((hint) => (
                                    <article className="hint-card" key={hint.id}>
                                      <div>
                                        <strong>{hint.content}</strong>
                                        <p className="field-hint">
                                          {hint.latitude !== null && hint.longitude !== null
                                            ? `${hint.latitude}, ${hint.longitude}`
                                            : "Sin coordenadas"}
                                        </p>
                                      </div>

                                      <div className="mission-action-row">
                                        <button
                                          className="ghost-button"
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
                                              className="ghost-button"
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
                                    </article>
                                  ))}
                                </div>
                              )}
                            </article>
                          );
                        })}
                      </div>
                    </section>

                    <form className="auth-form" onSubmit={handleCreateOperationalHint}>
                      <div>
                        <p className="eyebrow">Pista en vivo</p>
                        <h4>Añadir Pista Operativa</h4>
                      </div>

                      <label className="field">
                        <span>Etapa de sesión</span>
                        <select
                          className="input"
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
                      </label>

                      <label className="field">
                        <span>Texto de la pista</span>
                        <textarea
                          className="input"
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
                      </label>

                      <div className="form-grid-two">
                        <label className="field">
                          <span>Latitud</span>
                          <input
                            className="input"
                            inputMode="decimal"
                            onChange={(event) =>
                              setOperationalHintDraft((current) => ({
                                ...current,
                                latitude: event.target.value
                              }))
                            }
                            value={operationalHintDraft.latitude}
                          />
                        </label>

                        <label className="field">
                          <span>Longitud</span>
                          <input
                            className="input"
                            inputMode="decimal"
                            onChange={(event) =>
                              setOperationalHintDraft((current) => ({
                                ...current,
                                longitude: event.target.value
                              }))
                            }
                            value={operationalHintDraft.longitude}
                          />
                        </label>
                      </div>

                      <div className="mission-action-row">
                        <button className="primary-button" disabled={isSubmittingHint} type="submit">
                          {isSubmittingHint ? "Guardando..." : "Guardar pista operativa"}
                        </button>
                      </div>
                    </form>
                  </section>
                ) : null}
              </>
            ) : (
              <div className="empty-state">
                <strong>Select a scheduled LiveSession.</strong>
                <p>Snapshot detail shows persisted Session Stage Flow copied from the source Mission.</p>
              </div>
            )}
          </section>
        </div>
      </section>
    </section>
  );
}
