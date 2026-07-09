import { type FormEvent, useState } from "react";
import {
  type MissionDraft,
  type MissionItemDraft,
} from "./mission-authoring-types";
import { MissionBuilderSidebar } from "./mission-builder-sidebar";
import { MissionBuilderCanvas } from "./mission-builder-canvas";
import "./mission-builder.css";

type MissionBuilderFullProps = {
  draft: MissionDraft;
  onUpdateField: (field: "name" | "description" | "maximumDurationMinutes", value: string) => void;
  onItemsChange: (items: MissionItemDraft[]) => void;
  onSubmit: (e: FormEvent<HTMLFormElement>) => void;
  onCancel: () => void;
  isSubmitting: boolean;
  isEditMode: boolean;
  validationIssue: string | null;
  feedback: string | null;
  onToggleActivation?: (action: "activate" | "deactivate") => void;
  missionIsActive?: boolean;
  onReuseItem?: (kind: "section" | "challenge") => void;
};

export function MissionBuilderFull({
  draft,
  onUpdateField,
  onItemsChange,
  onSubmit,
  onCancel,
  isSubmitting,
  isEditMode,
  validationIssue,
  feedback,
  onToggleActivation,
  missionIsActive,
  onReuseItem,
}: MissionBuilderFullProps) {
  // State to track which item is currently selected in the sidebar
  const [selectedItemId, setSelectedItemId] = useState<string | "mission_config">("mission_config");

  return (
    <div className="mb-root">
      {/* Top Header */}
      <header className="mb-header">
        <div className="mb-header-title">
          <span className="text-muted">Misiones</span>
          <span className="text-muted">/</span>
          <strong>{draft.name || "Nueva Misión"}</strong>
        </div>
        <div className="mb-header-actions">
          <button
            className="btn btn-ghost btn-sm"
            onClick={onCancel}
            type="button"
            disabled={isSubmitting}
          >
            Volver al Catálogo
          </button>
          
          {isEditMode && onToggleActivation && (
            missionIsActive ? (
              <button
                className="btn btn-danger btn-sm"
                onClick={() => onToggleActivation("deactivate")}
                disabled={isSubmitting}
                type="button"
              >
                Desactivar
              </button>
            ) : (
              <button
                className="btn btn-success btn-sm"
                onClick={() => onToggleActivation("activate")}
                disabled={isSubmitting}
                type="button"
              >
                Activar
              </button>
            )
          )}

          <button
            className="btn btn-primary btn-sm"
            disabled={isSubmitting}
            form="mission-builder-form"
            type="submit"
          >
            {isEditMode ? "Guardar Cambios" : "Crear Misión"}
          </button>
        </div>
      </header>

      {/* Main Form Container */}
      <form
        id="mission-builder-form"
        className="mb-container"
        onSubmit={onSubmit}
      >
        <MissionBuilderSidebar
          items={draft.items}
          onItemsChange={onItemsChange}
          selectedItemId={selectedItemId}
          onSelect={setSelectedItemId}
          onReuseItem={onReuseItem}
        />
        <MissionBuilderCanvas
          draft={draft}
          onUpdateField={onUpdateField}
          items={draft.items}
          onItemsChange={onItemsChange}
          selectedItemId={selectedItemId}
          validationIssue={validationIssue}
          feedback={feedback}
        />
      </form>
    </div>
  );
}
