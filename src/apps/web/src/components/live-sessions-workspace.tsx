"use client";

import { type FormEvent, useCallback, useEffect, useMemo, useState } from "react";
import { getClientConfig } from "@/lib/config";
import { useSessionOperationsConnection } from "@/hooks/use-session-operations-connection";

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

export function LiveSessionsWorkspace({ accessToken }: LiveSessionsWorkspaceProps) {
  const [missions, setMissions] = useState<EligibleMissionSummary[]>([]);
  const [selectedMissionId, setSelectedMissionId] = useState<string | null>(null);
  const [selectedMission, setSelectedMission] = useState<EligibleMissionDetail | null>(null);
  const [selectedMissionStageIds, setSelectedMissionStageIds] = useState<string[]>([]);
  const [liveSessions, setLiveSessions] = useState<LiveSession[]>([]);
  const [selectedLiveSessionId, setSelectedLiveSessionId] = useState<string | null>(null);
  const [selectedLiveSessionOverview, setSelectedLiveSessionOverview] = useState<LiveSessionOverview | null>(null);
  const [draft, setDraft] = useState<LiveSessionDraft>(createEmptyDraft);
  const [operationalHintDraft, setOperationalHintDraft] = useState<OperationalHintDraft>(
    createEmptyOperationalHintDraft
  );
  const [isLoadingMissions, setIsLoadingMissions] = useState(true);
  const [isLoadingMissionDetail, setIsLoadingMissionDetail] = useState(false);
  const [isLoadingLiveSessions, setIsLoadingLiveSessions] = useState(true);
  const [isLoadingLiveSessionOverview, setIsLoadingLiveSessionOverview] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [lifecycleActionPending, setLifecycleActionPending] = useState<LiveSessionLifecycleAction | null>(null);
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

  const selectedLiveSession =
    liveSessions.find((liveSession) => liveSession.id === selectedLiveSessionId) ?? liveSessions[0] ?? null;
  const isSelectedLiveSessionActive =
    selectedLiveSession !== null && ["Running", "Paused"].includes(selectedLiveSession.state);
  const isSelectedLiveSessionStageDeactivationAllowed =
    selectedLiveSession !== null && ["Scheduled", "Running", "Paused"].includes(selectedLiveSession.state);

  const lifecycleActions = useMemo(() => {
    if (!selectedLiveSession) {
      return [];
    }

    const actions: Array<{
      action: LiveSessionLifecycleAction;
      label: string;
      requiresConfirmation: boolean;
      disabled: boolean;
    }> = [
      {
        action: "start",
        label: "Start session",
        requiresConfirmation: false,
        disabled: selectedLiveSession.state !== "Scheduled" || selectedLiveSession.registeredSessionTeamCount === 0
      },
      {
        action: "pause",
        label: "Pause session",
        requiresConfirmation: false,
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

  useSessionOperationsConnection({
    accessToken,
    hubUrl: sessionHubUrl,
    onResync: () => {
      setIsLoadingLiveSessions(true);
      void loadLiveSessions(selectedLiveSessionId ?? undefined);
    }
  });

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
      });
      return;
    }

    queueMicrotask(() => {
      void loadLiveSessionOverview(selectedLiveSessionId);
    });
  }, [loadLiveSessionOverview, selectedLiveSessionId]);

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
      setFeedback(`LiveSession moved through lifecycle action: ${action}.`);
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "Could not update Session Lifecycle.");
    } finally {
      setLifecycleActionPending(null);
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
