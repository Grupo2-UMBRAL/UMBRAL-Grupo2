import {
  type FormEvent,
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import { getClientConfig } from "@/lib/config";
import {
  type ChallengeDraft,
  type MissionDetail,
  type MissionDraft,
  type MissionItemDraft,
  type MissionSummary,
  type SectionDraft,
  difficultyOptions,
} from "./mission-authoring-types";
import {
  createEmptyChallengeDraft,
  createEmptyMissionDraft,
  createEmptySectionDraft,
  findMissionValidationIssue,
  serializeMissionDraft,
  toMissionDraft,
  cloneChallengeAsDraft,
  cloneSectionAsDraft,
} from "./mission-authoring-model";
import { MissionBuilderFull } from "./mission-builder-full";
import { ReuseItemModal } from "./reuse-item-modal";

type MissionsAdminWorkspaceProps = {
  accessToken: string;
};

function createAuthorizedHeaders(accessToken: string) {
  return {
    Authorization: `Bearer ${accessToken}`,
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
        if (
          "detail" in payload &&
          typeof payload.detail === "string" &&
          payload.detail.trim()
        ) {
          return payload.detail;
        }

        if (
          "title" in payload &&
          typeof payload.title === "string" &&
          payload.title.trim()
        ) {
          return payload.title;
        }

        if (
          "message" in payload &&
          typeof payload.message === "string" &&
          payload.message.trim()
        ) {
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

function summarizeSelection(missions: MissionSummary[]) {
  const active = missions.filter((mission) => mission.isActive).length;

  return {
    total: missions.length,
    active,
    inactive: missions.length - active,
  };
}

export function MissionsAdminWorkspace({
  accessToken,
}: MissionsAdminWorkspaceProps) {
  const config = getClientConfig();
  const missionsUrl = `${config.edgeProxyPublicBaseUrl}/mission-management/api/mission-management/missions`;
  const listRequestSequenceRef = useRef(0);
  const detailRequestSequenceRef = useRef(0);
  const [missions, setMissions] = useState<MissionSummary[]>([]);
  const [selectedMissionId, setSelectedMissionId] = useState<string | null>(
    null,
  );
  const [selectedMission, setSelectedMission] = useState<MissionDetail | null>(
    null,
  );
  const [draft, setDraft] = useState<MissionDraft>(createEmptyMissionDraft);
  const [editorMode, setEditorMode] = useState<"create" | "edit">("create");
  const [isLoadingList, setIsLoadingList] = useState(true);
  const [isLoadingDetail, setIsLoadingDetail] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [feedback, setFeedback] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const selectionSummary = useMemo(
    () => summarizeSelection(missions),
    [missions],
  );
  const [isEditorOpen, setIsEditorOpen] = useState(false);
  const [reuseModalKind, setReuseModalKind] = useState<"section" | "challenge" | null>(null);

  const addRootSection = useCallback(() => {
    setDraft((current) => ({
      ...current,
      items: [...current.items, createEmptySectionDraft()],
    }));
  }, []);

  const addRootChallenge = useCallback(() => {
    setDraft((current) => ({
      ...current,
      items: [...current.items, createEmptyChallengeDraft()],
    }));
  }, []);

  const createEmptyFromReuseModal = useCallback(() => {
    if (reuseModalKind === "section") {
      addRootSection();
    } else if (reuseModalKind === "challenge") {
      addRootChallenge();
    }
    setReuseModalKind(null);
  }, [addRootChallenge, addRootSection, reuseModalKind]);

  const addReusedItem = useCallback((source: MissionItemDraft) => {
    const cloned =
      source.kind === "Section"
        ? cloneSectionAsDraft(source as SectionDraft)
        : cloneChallengeAsDraft(source as ChallengeDraft);
    setDraft((current) => ({ ...current, items: [...current.items, cloned] }));
    setReuseModalKind(null);
  }, []);

  // Bare fetch of one mission's detail (no state side effects) so the reuse
  // modal can read other missions' content without touching the editor.
  const fetchMissionDetailById = useCallback(
    async (missionId: string): Promise<MissionDetail> => {
      const response = await fetch(`${missionsUrl}/${missionId}`, {
        headers: createAuthorizedHeaders(accessToken),
      });

      if (!response.ok) {
        throw new Error(await readFailureDetail(response));
      }

      return (await response.json()) as MissionDetail;
    },
    [accessToken, missionsUrl],
  );

  const loadMissions = useCallback(
    async (preferredMissionId?: string | null) => {
      const requestSequence = ++listRequestSequenceRef.current;
      setIsLoadingList(true);
      setErrorMessage(null);

      try {
        const response = await fetch(missionsUrl, {
          headers: createAuthorizedHeaders(accessToken),
        });

        if (!response.ok) {
          throw new Error(await readFailureDetail(response));
        }

        const payload = (await response.json()) as MissionSummary[];
        if (requestSequence !== listRequestSequenceRef.current) {
          return;
        }

        setMissions(payload);
      } catch (error) {
        if (requestSequence !== listRequestSequenceRef.current) {
          return;
        }

        setErrorMessage(
          error instanceof Error
            ? error.message
            : "No se pudieron cargar las Misiones.",
        );
      } finally {
        if (requestSequence === listRequestSequenceRef.current) {
          setIsLoadingList(false);
        }
      }
    },
    [accessToken, missionsUrl],
  );

  const loadMissionDetail = useCallback(
    async (missionId: string) => {
      const requestSequence = ++detailRequestSequenceRef.current;
      setIsLoadingDetail(true);
      setErrorMessage(null);

      try {
        const response = await fetch(`${missionsUrl}/${missionId}`, {
          headers: createAuthorizedHeaders(accessToken),
        });

        if (!response.ok) {
          throw new Error(await readFailureDetail(response));
        }

        const mission = (await response.json()) as MissionDetail;
        if (
          requestSequence !== detailRequestSequenceRef.current ||
          mission.id !== missionId
        ) {
          return;
        }

        setSelectedMission(mission);
        setDraft(toMissionDraft(mission));
        setEditorMode("edit");
      } catch (error) {
        if (requestSequence !== detailRequestSequenceRef.current) {
          return;
        }

        setErrorMessage(
          error instanceof Error
            ? error.message
            : "No se pudieron cargar los detalles de la Misión.",
        );
      } finally {
        if (requestSequence === detailRequestSequenceRef.current) {
          setIsLoadingDetail(false);
        }
      }
    },
    [accessToken, missionsUrl],
  );

  useEffect(() => {
    queueMicrotask(() => {
      void loadMissions();
    });
  }, [loadMissions]);

  const updateDraftField = useCallback(
    (field: "name" | "description" | "maximumDurationMinutes", value: string) => {
      setDraft((current) => ({
        ...current,
        [field]: value,
      }));
    },
    [],
  );

  const setItems = useCallback((items: MissionItemDraft[]) => {
    setDraft((current) => ({ ...current, items }));
  }, []);

  function handleCreateMode() {
    listRequestSequenceRef.current += 1;
    detailRequestSequenceRef.current += 1;
    setEditorMode("create");
    setIsLoadingList(false);
    setIsLoadingDetail(false);
    setSelectedMissionId(null);
    setSelectedMission(null);
    setDraft(createEmptyMissionDraft());
    setFeedback(null);
    setErrorMessage(null);
    setIsEditorOpen(true);
  }

  function handleOpenMission(id: string) {
    setFeedback(null);
    setSelectedMissionId(id);
    setIsEditorOpen(true);
    void loadMissionDetail(id);
  }

  function handleCloseEditor() {
    setIsEditorOpen(false);
    setSelectedMissionId(null);
    void loadMissions();
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSubmitting(true);
    setFeedback(null);
    setErrorMessage(null);

    const validationIssue = findMissionValidationIssue(draft);
    if (validationIssue) {
      setErrorMessage(validationIssue);
      setIsSubmitting(false);
      return;
    }

    const body = serializeMissionDraft(draft);
    const requestUrl =
      editorMode === "create"
        ? missionsUrl
        : `${missionsUrl}/${selectedMissionId}`;
    const method = editorMode === "create" ? "POST" : "PUT";

    try {
      const response = await fetch(requestUrl, {
        method,
        headers: {
          ...createAuthorizedHeaders(accessToken),
          "Content-Type": "application/json",
        },
        body: JSON.stringify(body),
      });

      if (!response.ok) {
        throw new Error(await readFailureDetail(response));
      }

      const mission = (await response.json()) as MissionDetail;
      setSelectedMission(mission);
      setSelectedMissionId(mission.id);
      setDraft(toMissionDraft(mission));
      setEditorMode("edit");
      setFeedback(
        editorMode === "create" ? "Misión creada." : "Misión actualizada.",
      );
      await loadMissions(mission.id);
    } catch (error) {
      setErrorMessage(
        error instanceof Error ? error.message : "No se pudo guardar la Misión.",
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleActivationToggle(action: "activate" | "deactivate") {
    if (!selectedMissionId) {
      return;
    }

    setIsSubmitting(true);
    setFeedback(null);
    setErrorMessage(null);

    try {
      const response = await fetch(
        `${missionsUrl}/${selectedMissionId}/${action}`,
        {
          method: "POST",
          headers: createAuthorizedHeaders(accessToken),
        },
      );

      if (!response.ok) {
        throw new Error(await readFailureDetail(response));
      }

      const mission = (await response.json()) as MissionDetail;
      setSelectedMission(mission);
      setDraft(toMissionDraft(mission));
      setFeedback(
        action === "activate" ? "Misión activada." : "Misión desactivada.",
      );
      await loadMissions(mission.id);
    } catch (error) {
      setErrorMessage(
        error instanceof Error
          ? error.message
          : "No se pudo cambiar el estado de la Misión.",
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  if (isEditorOpen) {
    return (
      <div style={{ height: "100%", display: "flex", flexDirection: "column" }}>
        <MissionBuilderFull
          draft={draft}
          onUpdateField={updateDraftField}
          onItemsChange={setItems}
          onSubmit={handleSubmit}
          onClose={handleCloseEditor}
          isSubmitting={isSubmitting}
          isEditMode={editorMode === "edit"}
          validationIssue={errorMessage}
          feedback={feedback}
          onToggleActivation={handleActivationToggle}
          missionIsActive={selectedMission?.isActive}
          onReuseItem={setReuseModalKind}
          missions={missions}
          selectedMissionId={editorMode === "edit" ? selectedMissionId : null}
          isLoadingList={isLoadingList || isLoadingDetail}
          onSelectMission={handleOpenMission}
          onCreateMission={handleCreateMode}
        />

        {reuseModalKind ? (
          <ReuseItemModal
            currentMissionId={editorMode === "edit" ? selectedMissionId : null}
            fetchMissionDetail={fetchMissionDetailById}
            kind={reuseModalKind}
            missions={missions}
            onAddReused={addReusedItem}
            onClose={() => setReuseModalKind(null)}
            onCreateEmpty={createEmptyFromReuseModal}
          />
        ) : null}
      </div>
    );
  }

  return (
    <div className="workspace-section">
      <div className="workspace-section-header">
        <div className="stack-sm">
          <span className="eyebrow">Diseño de misiones</span>
          <h2>Espacio de trabajo de Misiones</h2>
          <p className="text-muted">
            Catálogo global de misiones estructuradas. Abre el editor para crear
            o editar la jerarquía de secciones y retos.
          </p>
        </div>
      </div>

      <div className="workspace-section-body stack">
        <div className="row-wrap" style={{ gap: "0.75rem" }}>
          <div className="card-compact">
            <span className="text-muted text-sm">Total de Misiones</span>
            <strong>{selectionSummary.total}</strong>
          </div>
          <div className="card-compact">
            <span className="text-muted text-sm">Activas</span>
            <strong>
              <span className="badge badge-green">
                {selectionSummary.active}
              </span>
            </strong>
          </div>
          <div className="card-compact">
            <span className="text-muted text-sm">Inactivas</span>
            <strong>
              <span className="badge badge-red">
                {selectionSummary.inactive}
              </span>
            </strong>
          </div>
        </div>

        {errorMessage ? (
          <div className="error-banner">{errorMessage}</div>
        ) : null}
        {feedback ? <div className="success-banner">{feedback}</div> : null}

        <section className="card stack">
          <div className="card-header card-header-actions">
            <div className="stack-sm">
              <span className="eyebrow">Catálogo</span>
              <h3>Misiones</h3>
            </div>
            <button
              className="btn btn-ghost btn-sm"
              onClick={handleCreateMode}
              type="button"
            >
              Nueva Misión
            </button>
          </div>

          {isLoadingList ? (
            <div className="loading-center">Cargando Misiones…</div>
          ) : null}

          {!isLoadingList && missions.length === 0 ? (
            <div className="empty-state">
              <strong>Aún no hay Misiones.</strong>
              <p>
                Cree la primera Misión reutilizable para el catálogo del
                administrador.
              </p>
            </div>
          ) : null}

          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Nombre</th>
                  <th>Estado</th>
                  <th>Duración</th>
                </tr>
              </thead>
              <tbody>
                {missions.map((mission) => (
                  <tr
                    className="clickable"
                    key={mission.id}
                    onClick={() => handleOpenMission(mission.id)}
                  >
                    <td>
                      <strong>{mission.name}</strong>
                    </td>
                    <td>
                      <span
                        className={
                          mission.isActive
                            ? "badge badge-green"
                            : "badge badge-red"
                        }
                      >
                        {mission.isActive ? "Activa" : "Inactiva"}
                      </span>
                    </td>
                    <td className="mono text-sm">
                      {mission.maximumDurationMinutes} min
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      </div>
    </div>
  );
}
