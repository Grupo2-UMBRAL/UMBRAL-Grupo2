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
  isLoadingOverview: boolean;
  lifecycleActions: LifecycleActionViewModel[];
  lifecycleActionPending: LiveSessionLifecycleAction | null;
  rankingItemCount: number;
  isLoadingRanking: boolean;
  onLifecycleAction: (action: LiveSessionLifecycleAction) => void;
  onRefreshOverview: () => void;
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

function LiveSessionOverviewDashboard({
  liveSession,
  overview,
  connectionState,
  isLoadingOverview,
  lifecycleActions,
  lifecycleActionPending,
  rankingItemCount,
  isLoadingRanking,
  onLifecycleAction,
  onRefreshOverview
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
          <div className="session-team-grid">
            {teams.map((team) => (
              <article className="session-team-card" key={team.sessionTeamId}>
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
              </article>
            ))}
          </div>
        ) : (
          <div className="empty-state">
            <strong>No Session Teams loaded.</strong>
            <p>Use manual refresh if enrollment changed while realtime was reconnecting.</p>
          </div>
        )}
      </section>

      <div className="overview-secondary-grid">
        <section className="overview-slot">
          <div>
            <p className="eyebrow">Scoring and Audit</p>
            <h4>Ranking widget</h4>
          </div>
          <p className="muted-copy">
            {isLoadingRanking
              ? "Ranking sync is running in background."
              : `${rankingItemCount} ranking item(s) available. Widget remains out of this slice.`}
          </p>
        </section>
        <section className="overview-slot">
          <div>
            <p className="eyebrow">Audit Log</p>
            <h4>Timeline</h4>
          </div>
          <p className="muted-copy">
            Event timeline is intentionally absent for UMB-25. Overview keeps rendering without it.
          </p>
        </section>
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
  const [draft, setDraft] = useState<LiveSessionDraft>(createEmptyDraft);
  const [operationalHintDraft, setOperationalHintDraft] = useState<OperationalHintDraft>(
    createEmptyOperationalHintDraft
  );
  const [isLoadingMissions, setIsLoadingMissions] = useState(true);
  const [isLoadingMissionDetail, setIsLoadingMissionDetail] = useState(false);
  const [isLoadingLiveSessions, setIsLoadingLiveSessions] = useState(true);
  const [isLoadingLiveSessionOverview, setIsLoadingLiveSessionOverview] = useState(false);
  const [isLoadingRanking, setIsLoadingRanking] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isSubmittingHint, setIsSubmittingHint] = useState(false);
  const [deactivatingStageId, setDeactivatingStageId] = useState<string | null>(null);
  const [lifecycleActionPending, setLifecycleActionPending] = useState<LiveSessionLifecycleAction | null>(null);
  const [sessionRealtimeConnection, setSessionRealtimeConnection] = useState<RealtimeConnectionState>({
    kind: "disconnected",
    label: "Desconectado",
    detail: "SignalR waiting for selected LiveSession."
  });
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<string | null>(null);

  const eligibleMissionsUrl = useMemo(
    () => `${getClientConfig().edgeProxyPublicBaseUrl}/mission-design/api/mission-design/missions/eligible-for-live-session`,
    []
  );
  const liveSessionsUrl = useMemo(
    () => `${getClientConfig().edgeProxyPublicBaseUrl}/session-operations/api/session-operations/live-sessions`,
    []
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
  const selectedLiveSessionOverviewTeams = isSelectedLiveSessionOverviewCurrent
    ? selectedLiveSessionOverview.sessionTeams
    : [];
  const selectedRankingItems =
    selectedLiveSessionRanking?.liveSessionId === selectedLiveSession?.id
      ? selectedLiveSessionRanking.items
      : [];
  const pendingLiveSessionStageCount = selectedLiveSessionStages.filter(
    (sessionStage) => getSessionStageOperationalStatus(sessionStage, selectedLiveSessionOverviewTeams) === "Pending"
  ).length;
  const operationalHintStageId =
    operationalHintDraft.missionStageId || selectedLiveSessionStages[0]?.missionStageId || "";

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

  const loadLiveSessionRanking = useCallback(
    async (liveSessionId: string) => {
      setIsLoadingRanking(true);

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
      } catch {
        setSelectedLiveSessionRanking(null);
      } finally {
        setIsLoadingRanking(false);
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
        skipNegotiation: false,
        transport: HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents
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
      });
      return;
    }

    queueMicrotask(() => {
      void loadLiveSessionOverview(selectedLiveSessionId);
      void loadLiveSessionRanking(selectedLiveSessionId);
    });
  }, [loadLiveSessionOverview, loadLiveSessionRanking, selectedLiveSessionId]);

  useEffect(() => {
    if (!selectedLiveSessionId) {
      return;
    }

    let active = true;
    const connection = new HubConnectionBuilder()
      .withUrl(scoringAuditHubUrl, {
        accessTokenFactory: () => accessToken,
        skipNegotiation: false,
        transport: HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on("ReceiveRankingUpdated", (payload: RankingPayload) => {
      if (payload.liveSessionId !== selectedLiveSessionId) {
        return;
      }

      setSelectedLiveSessionRanking(payload);
    });

    connection.onreconnected(() => {
      if (active) {
        void loadLiveSessionRanking(selectedLiveSessionId);
      }
    });

    connection.onclose((error) => {
      if (active && error) {
        setErrorMessage(`Scoring Audit realtime closed: ${error.message}`);
      }
    });

    void connection.start().catch((error: unknown) => {
      if (active) {
        setErrorMessage(error instanceof Error ? error.message : "Could not connect to Scoring Audit realtime.");
      }
    });

    return () => {
      active = false;
      void connection.stop();
    };
  }, [accessToken, loadLiveSessionRanking, scoringAuditHubUrl, selectedLiveSessionId]);

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
                    isLoadingOverview={isLoadingLiveSessionOverview}
                    isLoadingRanking={isLoadingRanking}
                    lifecycleActionPending={lifecycleActionPending}
                    lifecycleActions={lifecycleActions}
                    liveSession={selectedLiveSession}
                    onLifecycleAction={(action) => {
                      void handleLifecycleAction(action);
                    }}
                    onRefreshOverview={refreshSelectedOverview}
                    overview={isSelectedLiveSessionOverviewCurrent ? selectedLiveSessionOverview : null}
                    rankingItemCount={selectedRankingItems.length}
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
