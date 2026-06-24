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
  type MissionDetail,
  type MissionDraft,
  type MissionItemDraft,
  type MissionSummary,
  difficultyOptions,
} from "./mission-authoring-types";
import {
  createEmptyChallengeDraft,
  createEmptyMissionDraft,
  createEmptySectionDraft,
  findMissionValidationIssue,
  serializeMissionDraft,
  summarizeItems,
  toMissionDraft,
} from "./mission-authoring-model";
import { MissionItemList } from "./mission-item-list";

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
  const itemStats = useMemo(() => summarizeItems(draft.items), [draft.items]);

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

        const nextMissionId =
          preferredMissionId &&
          payload.some((mission) => mission.id === preferredMissionId)
            ? preferredMissionId
            : (payload[0]?.id ?? null);

        setSelectedMissionId(nextMissionId);

        if (!nextMissionId) {
          setEditorMode("create");
          setSelectedMission(null);
          setDraft(createEmptyMissionDraft());
        }
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

  useEffect(() => {
    if (!selectedMissionId) {
      return;
    }

    queueMicrotask(() => {
      void loadMissionDetail(selectedMissionId);
    });
  }, [loadMissionDetail, selectedMissionId]);

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

  return (
    <div className="workspace-section">
      {/* Shared datalist so per-item difficulty overrides can suggest the
          canonical values without forcing them. */}
      <datalist id="difficulty-options">
        {difficultyOptions.map((option) => (
          <option key={option} value={option} />
        ))}
      </datalist>

      <div className="workspace-section-header">
        <div className="stack-sm">
          <span className="eyebrow">Diseño de misiones</span>
          <h2>Espacio de trabajo de Misiones</h2>
          <p className="text-muted">
            Editor lineal de secciones y retos. Las secciones agrupan contenido
            (pueden anidarse); los retos son Trivia o Treasure Hunt con sus
            preguntas o búsquedas. Arrastra para reordenar.
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

        <div className="split-layout-wide">
          {/* ── Mission list panel ── */}
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
                      className={
                        mission.id === selectedMissionId
                          ? "clickable is-selected"
                          : "clickable"
                      }
                      key={mission.id}
                      onClick={() => {
                        setFeedback(null);
                        setSelectedMissionId(mission.id);
                      }}
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

          {/* ── Mission editor panel ── */}
          <section className="card stack">
            <div className="card-header card-header-actions">
              <div className="stack-sm">
                <span className="eyebrow">
                  {editorMode === "create" ? "Crear" : "Misión seleccionada"}
                </span>
                <h3>
                  {editorMode === "create"
                    ? "Nueva Misión"
                    : (selectedMission?.name ?? "Detalles de la misión")}
                </h3>
              </div>
              {selectedMission ? (
                <span
                  className={
                    selectedMission.isActive
                      ? "badge badge-green"
                      : "badge badge-red"
                  }
                >
                  {selectedMission.isActive ? "Activa" : "Inactiva"}
                </span>
              ) : null}
            </div>

            {isLoadingDetail ? (
              <div className="loading-center">
                Cargando detalles de la misión…
              </div>
            ) : null}

            <form className="stack" onSubmit={handleSubmit}>
              {/* Item stats */}
              <div className="info-banner">
                <div className="row-wrap" style={{ gap: "1rem" }}>
                  <span>
                    <strong>Secciones:</strong> {itemStats.sections}
                  </span>
                  <span>
                    <strong>Retos:</strong> {itemStats.challenges}
                  </span>
                  <span>
                    <strong>Preguntas:</strong> {itemStats.questions}
                  </span>
                  <span>
                    <strong>Búsquedas:</strong> {itemStats.searches}
                  </span>
                </div>
              </div>

              <div className="form-group">
                <label className="form-label">Nombre</label>
                <input
                  className="form-input"
                  maxLength={120}
                  onChange={(event) =>
                    updateDraftField("name", event.target.value)
                  }
                  required
                  value={draft.name}
                />
              </div>

              <div className="form-group">
                <label className="form-label">Descripción</label>
                <textarea
                  className="form-textarea"
                  maxLength={1024}
                  onChange={(event) =>
                    updateDraftField("description", event.target.value)
                  }
                  required
                  rows={4}
                  value={draft.description}
                />
              </div>

              <div className="form-row">
                <div className="form-group">
                  <label className="form-label">Duración máxima (minutos)</label>
                  <input
                    className="form-input"
                    max={1440}
                    min={1}
                    onChange={(event) =>
                      updateDraftField(
                        "maximumDurationMinutes",
                        event.target.value,
                      )
                    }
                    required
                    type="number"
                    value={draft.maximumDurationMinutes}
                  />
                </div>
              </div>

              {/* ── Item list ── */}
              <div className="card-section stack">
                <div className="card-header card-header-actions">
                  <div className="stack-sm">
                    <span className="eyebrow">Estructura</span>
                    <h4>Secciones y retos</h4>
                  </div>
                  <div className="row-sm">
                    <button
                      className="btn btn-ghost btn-sm"
                      onClick={addRootSection}
                      type="button"
                    >
                      Agregar sección
                    </button>
                    <button
                      className="btn btn-ghost btn-sm"
                      onClick={addRootChallenge}
                      type="button"
                    >
                      Agregar reto
                    </button>
                  </div>
                </div>

                {draft.items.length === 0 ? (
                  <div className="empty-state">
                    <strong>Aún no hay contenido.</strong>
                    <p>
                      Comienza con una sección para agrupar, o un reto jugable
                      directo.
                    </p>
                  </div>
                ) : (
                  <MissionItemList
                    depth={0}
                    items={draft.items}
                    onChange={setItems}
                  />
                )}
              </div>

              <div className="form-actions">
                <button
                  className="btn btn-primary"
                  disabled={isSubmitting}
                  type="submit"
                >
                  {editorMode === "create" ? "Crear Misión" : "Guardar cambios"}
                </button>

                {selectedMission ? (
                  selectedMission.isActive ? (
                    <button
                      className="btn btn-danger"
                      disabled={isSubmitting}
                      onClick={() => handleActivationToggle("deactivate")}
                      type="button"
                    >
                      Desactivar
                    </button>
                  ) : (
                    <button
                      className="btn btn-success"
                      disabled={isSubmitting}
                      onClick={() => handleActivationToggle("activate")}
                      type="button"
                    >
                      Activar
                    </button>
                  )
                ) : null}
              </div>
            </form>

            {selectedMission ? (
              <div className="detail-panel">
                <div className="detail-row">
                  <span className="detail-label">ID de la misión</span>
                  <span className="detail-value mono">
                    {selectedMission.id}
                  </span>
                </div>
                <div className="detail-row">
                  <span className="detail-label">Estado actual</span>
                  <span className="detail-value">
                    <span
                      className={
                        selectedMission.isActive
                          ? "badge badge-green"
                          : "badge badge-red"
                      }
                    >
                      {selectedMission.isActive ? "Activa" : "Inactiva"}
                    </span>
                  </span>
                </div>
              </div>
            ) : null}
          </section>
        </div>
      </div>
    </div>
  );
}
