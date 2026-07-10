import { type FormEvent, useState } from "react";
import {
  type MissionDraft,
  type MissionItemDraft,
  type MissionSummary,
} from "./mission-authoring-types";
import { MissionListColumn } from "./mission-list-column";
import { MissionBuilderCanvas } from "./mission-builder-canvas";
import "./mission-builder.css";

type MissionBuilderFullProps = {
  // Editor
  draft: MissionDraft;
  onUpdateField: (
    field: "name" | "description" | "maximumDurationMinutes",
    value: string,
  ) => void;
  onItemsChange: (items: MissionItemDraft[]) => void;
  onSubmit: (e: FormEvent<HTMLFormElement>) => void;
  onClose: () => void;
  isSubmitting: boolean;
  isEditMode: boolean;
  validationIssue: string | null;
  feedback: string | null;
  onToggleActivation?: (action: "activate" | "deactivate") => void;
  missionIsActive?: boolean;
  onReuseItem: (kind: "section" | "challenge") => void;
  // Missions list
  missions: MissionSummary[];
  selectedMissionId: string | null;
  isLoadingList: boolean;
  onSelectMission: (id: string) => void;
  onCreateMission: () => void;
  stageCounts?: Record<string, number>;
};

export function MissionBuilderFull({
  draft,
  onUpdateField,
  onItemsChange,
  onSubmit,
  onClose,
  isSubmitting,
  isEditMode,
  validationIssue,
  feedback,
  onToggleActivation,
  missionIsActive,
  onReuseItem,
  missions,
  selectedMissionId,
  isLoadingList,
  onSelectMission,
  onCreateMission,
  stageCounts,
}: MissionBuilderFullProps) {
  const [listCollapsed, setListCollapsed] = useState(false);

  return (
    <div className="mb-root">
      <header className="mb-header">
        <div className="mb-header-title">
          {listCollapsed ? (
            <button
              className="mb-icon-btn"
              onClick={() => setListCollapsed(false)}
              type="button"
              title="Mostrar lista de misiones"
              aria-label="Mostrar lista de misiones"
            >
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="9 6 15 12 9 18" />
              </svg>
            </button>
          ) : null}
          <h1>Diseño de misiones</h1>
        </div>
        <div className="mb-header-actions">
          <button
            className="btn btn-ghost btn-sm"
            onClick={onClose}
            type="button"
            disabled={isSubmitting}
          >
            Volver
          </button>
          {isEditMode && onToggleActivation ? (
            missionIsActive ? (
              <button
                className="btn btn-danger btn-sm"
                onClick={() => onToggleActivation("deactivate")}
                disabled={isSubmitting}
                type="button"
              >
                Desactivar misión
              </button>
            ) : (
              <button
                className="btn btn-success btn-sm"
                onClick={() => onToggleActivation("activate")}
                disabled={isSubmitting}
                type="button"
              >
                Activar misión
              </button>
            )
          ) : null}

          <button
            className="btn btn-primary btn-sm"
            disabled={isSubmitting}
            form="mission-builder-form"
            type="submit"
          >
            {isEditMode ? "Guardar cambios" : "Crear misión"}
          </button>
        </div>
      </header>

      <form id="mission-builder-form" className="mb-body" onSubmit={onSubmit}>
        <MissionListColumn
          missions={missions}
          selectedMissionId={selectedMissionId}
          isLoading={isLoadingList}
          collapsed={listCollapsed}
          onToggleCollapse={() => setListCollapsed((c) => !c)}
          onSelect={onSelectMission}
          onCreate={onCreateMission}
          stageCounts={stageCounts}
        />
        <MissionBuilderCanvas
          draft={draft}
          onUpdateField={onUpdateField}
          items={draft.items}
          onItemsChange={onItemsChange}
          validationIssue={validationIssue}
          feedback={feedback}
          isEditMode={isEditMode}
          missionIsActive={missionIsActive}
          onAddSection={() => onReuseItem("section")}
          onAddChallenge={() => onReuseItem("challenge")}
        />
      </form>
    </div>
  );
}
