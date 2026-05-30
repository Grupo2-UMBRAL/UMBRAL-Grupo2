"use client";

import { type FormEvent, useCallback, useEffect, useMemo, useState } from "react";
import { getClientConfig } from "@/lib/config";

type MissionSummary = {
  id: string;
  name: string;
  difficulty: string;
  maximumDurationMinutes: number;
  gameType: string;
  isActive: boolean;
};

type MissionDetail = MissionSummary & {
  description: string;
};

type MissionDraft = {
  name: string;
  description: string;
  difficulty: string;
  maximumDurationMinutes: string;
  gameType: string;
};

type MissionsAdminWorkspaceProps = {
  accessToken: string;
};

const gameTypeOptions = ["Treasure Hunt", "Trivia"] as const;

const emptyDraft: MissionDraft = {
  name: "",
  description: "",
  difficulty: "",
  maximumDurationMinutes: "60",
  gameType: gameTypeOptions[0]
};

function createAuthorizedHeaders(accessToken: string) {
  return {
    Authorization: `Bearer ${accessToken}`
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

        if ("message" in payload && typeof payload.message === "string" && payload.message.trim()) {
          return payload.message;
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

function toDraft(mission: MissionDetail): MissionDraft {
  return {
    name: mission.name,
    description: mission.description,
    difficulty: mission.difficulty,
    maximumDurationMinutes: String(mission.maximumDurationMinutes),
    gameType: mission.gameType
  };
}

function summarizeSelection(missions: MissionSummary[]) {
  const active = missions.filter((mission) => mission.isActive).length;

  return {
    total: missions.length,
    active,
    inactive: missions.length - active
  };
}

export function MissionsAdminWorkspace({ accessToken }: MissionsAdminWorkspaceProps) {
  const config = getClientConfig();
  const missionsUrl = `${config.edgeProxyPublicBaseUrl}/mission-design/api/mission-design/missions`;
  const [missions, setMissions] = useState<MissionSummary[]>([]);
  const [selectedMissionId, setSelectedMissionId] = useState<string | null>(null);
  const [selectedMission, setSelectedMission] = useState<MissionDetail | null>(null);
  const [draft, setDraft] = useState<MissionDraft>(emptyDraft);
  const [editorMode, setEditorMode] = useState<"create" | "edit">("create");
  const [isLoadingList, setIsLoadingList] = useState(true);
  const [isLoadingDetail, setIsLoadingDetail] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [feedback, setFeedback] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const selectionSummary = useMemo(() => summarizeSelection(missions), [missions]);

  const loadMissions = useCallback(async (preferredMissionId?: string | null) => {
    setIsLoadingList(true);
    setErrorMessage(null);

    try {
      const response = await fetch(missionsUrl, {
        headers: createAuthorizedHeaders(accessToken)
      });

      if (!response.ok) {
        throw new Error(await readFailureDetail(response));
      }

      const payload = (await response.json()) as MissionSummary[];
      setMissions(payload);

      const nextMissionId =
        preferredMissionId && payload.some((mission) => mission.id === preferredMissionId)
          ? preferredMissionId
          : payload[0]?.id ?? null;

      setSelectedMissionId(nextMissionId);

      if (!nextMissionId) {
        setEditorMode("create");
        setSelectedMission(null);
        setDraft(emptyDraft);
      }
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "Could not load Missions.");
    } finally {
      setIsLoadingList(false);
    }
  }, [accessToken, missionsUrl]);

  const loadMissionDetail = useCallback(async (missionId: string) => {
    setIsLoadingDetail(true);
    setErrorMessage(null);

    try {
      const response = await fetch(`${missionsUrl}/${missionId}`, {
        headers: createAuthorizedHeaders(accessToken)
      });

      if (!response.ok) {
        throw new Error(await readFailureDetail(response));
      }

      const mission = (await response.json()) as MissionDetail;
      setSelectedMission(mission);
      setDraft(toDraft(mission));
      setEditorMode("edit");
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "Could not load Mission detail.");
    } finally {
      setIsLoadingDetail(false);
    }
  }, [accessToken, missionsUrl]);

  useEffect(() => {
    queueMicrotask(() => {
      void loadMissions();
    });
  }, [loadMissions]);

  useEffect(() => {
    if (!selectedMissionId) {
      return;
    }

    queueMicrotask(() => {
      void loadMissionDetail(selectedMissionId);
    });
  }, [loadMissionDetail, selectedMissionId]);

  function handleDraftChange(field: keyof MissionDraft, value: string) {
    setDraft((current) => ({
      ...current,
      [field]: value
    }));
  }

  function handleCreateMode() {
    setEditorMode("create");
    setSelectedMissionId(null);
    setSelectedMission(null);
    setDraft(emptyDraft);
    setFeedback(null);
    setErrorMessage(null);
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSubmitting(true);
    setFeedback(null);
    setErrorMessage(null);

    const maximumDurationMinutes = Number.parseInt(draft.maximumDurationMinutes, 10);
    const body =
      editorMode === "create"
        ? {
            name: draft.name,
            description: draft.description,
            difficulty: draft.difficulty,
            maximumDurationMinutes,
            gameType: draft.gameType
          }
        : {
            name: draft.name,
            description: draft.description,
            difficulty: draft.difficulty,
            maximumDurationMinutes
          };

    const requestUrl =
      editorMode === "create" ? missionsUrl : `${missionsUrl}/${selectedMissionId}`;
    const method = editorMode === "create" ? "POST" : "PUT";

    try {
      const response = await fetch(requestUrl, {
        method,
        headers: {
          ...createAuthorizedHeaders(accessToken),
          "Content-Type": "application/json"
        },
        body: JSON.stringify(body)
      });

      if (!response.ok) {
        throw new Error(await readFailureDetail(response));
      }

      const mission = (await response.json()) as MissionDetail;
      setSelectedMission(mission);
      setSelectedMissionId(mission.id);
      setDraft(toDraft(mission));
      setEditorMode("edit");
      setFeedback(editorMode === "create" ? "Mission created." : "Mission updated.");
      await loadMissions(mission.id);
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "Could not save Mission.");
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleDeactivate() {
    if (!selectedMissionId) {
      return;
    }

    setIsSubmitting(true);
    setFeedback(null);
    setErrorMessage(null);

    try {
      const response = await fetch(`${missionsUrl}/${selectedMissionId}/deactivate`, {
        method: "POST",
        headers: createAuthorizedHeaders(accessToken)
      });

      if (!response.ok) {
        throw new Error(await readFailureDetail(response));
      }

      const mission = (await response.json()) as MissionDetail;
      setSelectedMission(mission);
      setDraft(toDraft(mission));
      setFeedback("Mission deactivated.");
      await loadMissions(mission.id);
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "Could not deactivate Mission.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <section className="panel stack-gap">
      <div className="section-heading">
        <div>
          <p className="eyebrow">Mission design</p>
          <h2>Missions workspace</h2>
        </div>
        <p className="section-copy">
          Create, inspect, edit, and deactivate reusable Missions without leaving the administrator shell.
        </p>
      </div>

      <div className="mission-summary-grid">
        <article className="signal-card">
          <strong>Total Missions</strong>
          <p className="metric-value">{selectionSummary.total}</p>
        </article>
        <article className="signal-card">
          <strong>Active</strong>
          <p className="metric-value metric-success">{selectionSummary.active}</p>
        </article>
        <article className="signal-card">
          <strong>Inactive</strong>
          <p className="metric-value metric-danger">{selectionSummary.inactive}</p>
        </article>
      </div>

      {errorMessage ? <p className="banner-error">{errorMessage}</p> : null}
      {feedback ? <p className="banner-success">{feedback}</p> : null}

      <div className="mission-workspace-grid">
        <section className="mission-list-panel">
          <div className="mission-list-header">
            <div>
              <p className="eyebrow">Catalog</p>
              <h3>Missions</h3>
            </div>
            <button className="ghost-button" onClick={handleCreateMode} type="button">
              New Mission
            </button>
          </div>

          {isLoadingList ? <p className="muted-copy">Loading Missions.</p> : null}

          {!isLoadingList && missions.length === 0 ? (
            <div className="empty-state">
              <strong>No Missions yet.</strong>
              <p>Create the first reusable Mission for the administrator catalog.</p>
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
                  <span className={mission.isActive ? "status-pill status-ok" : "status-pill status-error"}>
                    {mission.isActive ? "active" : "inactive"}
                  </span>
                </div>
                <p>{mission.difficulty}</p>
                <dl className="mission-meta-grid">
                  <div>
                    <dt>Game Type</dt>
                    <dd>{mission.gameType}</dd>
                  </div>
                  <div>
                    <dt>Duration</dt>
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
              <p className="eyebrow">{editorMode === "create" ? "Create" : "Selected Mission"}</p>
              <h3>{editorMode === "create" ? "New Mission" : selectedMission?.name ?? "Mission detail"}</h3>
            </div>
            {selectedMission ? (
              <span className={selectedMission.isActive ? "status-pill status-ok" : "status-pill status-error"}>
                {selectedMission.isActive ? "active" : "inactive"}
              </span>
            ) : null}
          </div>

          {isLoadingDetail ? <p className="muted-copy">Loading Mission detail.</p> : null}

          <form className="auth-form" onSubmit={handleSubmit}>
            <label className="field">
              <span>Name</span>
              <input
                className="input"
                maxLength={120}
                onChange={(event) => handleDraftChange("name", event.target.value)}
                required
                value={draft.name}
              />
            </label>

            <label className="field">
              <span>Description</span>
              <textarea
                className="input textarea-input"
                maxLength={1024}
                onChange={(event) => handleDraftChange("description", event.target.value)}
                required
                rows={5}
                value={draft.description}
              />
            </label>

            <div className="form-grid-two">
              <label className="field">
                <span>Difficulty</span>
                <input
                  className="input"
                  maxLength={60}
                  onChange={(event) => handleDraftChange("difficulty", event.target.value)}
                  required
                  value={draft.difficulty}
                />
              </label>

              <label className="field">
                <span>Maximum duration</span>
                <input
                  className="input"
                  max={1440}
                  min={1}
                  onChange={(event) => handleDraftChange("maximumDurationMinutes", event.target.value)}
                  required
                  type="number"
                  value={draft.maximumDurationMinutes}
                />
              </label>
            </div>

            <label className="field">
              <span>Game Type</span>
              <select
                className="input"
                disabled={editorMode === "edit"}
                onChange={(event) => handleDraftChange("gameType", event.target.value)}
                value={draft.gameType}
              >
                {gameTypeOptions.map((gameType) => (
                  <option key={gameType} value={gameType}>
                    {gameType}
                  </option>
                ))}
              </select>
              {editorMode === "edit" ? (
                <span className="field-hint">Current backend contract only supports changing Game Type during creation.</span>
              ) : null}
            </label>

            <div className="mission-action-row">
              <button className="primary-button" disabled={isSubmitting} type="submit">
                {editorMode === "create" ? "Create Mission" : "Save changes"}
              </button>

              {selectedMission?.isActive ? (
                <button
                  className="ghost-button danger-button"
                  disabled={isSubmitting}
                  onClick={handleDeactivate}
                  type="button"
                >
                  Deactivate
                </button>
              ) : null}
            </div>
          </form>

          {selectedMission ? (
            <dl className="definition-grid mission-detail-grid">
              <div>
                <dt>Mission ID</dt>
                <dd>{selectedMission.id}</dd>
              </div>
              <div>
                <dt>Game Type</dt>
                <dd>{selectedMission.gameType}</dd>
              </div>
              <div>
                <dt>Current status</dt>
                <dd>{selectedMission.isActive ? "Active" : "Inactive"}</dd>
              </div>
            </dl>
          ) : null}
        </section>
      </div>
    </section>
  );
}
