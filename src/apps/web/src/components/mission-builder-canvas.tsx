import { useEffect } from "react";
import {
  type ChallengeDraft,
  type MissionDraft,
  type MissionItemDraft,
  type SectionDraft,
} from "./mission-authoring-types";
import { ChallengeEditor } from "./challenge-editor";
import { createEmptySectionDraft, createEmptyChallengeDraft } from "./mission-authoring-model";

type MissionBuilderCanvasProps = {
  draft: MissionDraft;
  onUpdateField: (field: "name" | "description" | "maximumDurationMinutes", value: string) => void;
  items: MissionItemDraft[];
  onItemsChange: (items: MissionItemDraft[]) => void;
  selectedItemId: string;
  validationIssue: string | null;
  feedback: string | null;
};

function RecursiveItemRender({ items, onItemsChange }: { items: MissionItemDraft[], onItemsChange: (items: MissionItemDraft[]) => void }) {
  return (
    <>
      {items.map((item, i) => {
        const setItem = (next: MissionItemDraft) => {
          const nextItems = [...items];
          nextItems[i] = next;
          onItemsChange(nextItems);
        };
        const removeItem = () => {
          const nextItems = [...items];
          nextItems.splice(i, 1);
          onItemsChange(nextItems);
        };

        if (item.kind === "Section") {
          const section = item as SectionDraft;
          return (
            <div key={section.clientId} id={section.clientId} className="mb-card mb-8" style={{ marginLeft: "1rem", borderLeft: "4px solid var(--border-strong)" }}>
              <div className="mb-card-header" style={{ flexDirection: "row", justifyContent: "space-between", alignItems: "flex-start" }}>
                <div>
                  <h3>Sección</h3>
                  <p>Agrupa retos u otras secciones.</p>
                </div>
                <button className="btn btn-danger btn-sm" type="button" onClick={removeItem}>
                  Eliminar Sección
                </button>
              </div>

              <div className="form-group mb-6">
                <label className="form-label">Título de la sección *</label>
                <input
                  className="form-input"
                  maxLength={120}
                  onChange={(e) => setItem({ ...section, title: e.target.value })}
                  placeholder="Ej: Fase 1 - Reconocimiento"
                  required
                  value={section.title}
                />
              </div>

              <div className="row-sm mb-6">
                <button
                  className="btn btn-ghost btn-sm"
                  onClick={() => setItem({ ...section, children: [...section.children, createEmptySectionDraft()] })}
                  type="button"
                >
                  + Agregar Sub-sección
                </button>
                <button
                  className="btn btn-ghost btn-sm"
                  onClick={() => setItem({ ...section, children: [...section.children, createEmptyChallengeDraft()] })}
                  type="button"
                >
                  + Agregar Reto
                </button>
              </div>

              {section.children.length > 0 && (
                <div className="stack" style={{ marginTop: "1rem" }}>
                  <RecursiveItemRender 
                    items={section.children} 
                    onItemsChange={(nextChildren) => setItem({ ...section, children: nextChildren })} 
                  />
                </div>
              )}
            </div>
          );
        }

        const challenge = item as ChallengeDraft;
        return (
          <div key={challenge.clientId} id={challenge.clientId} className="mb-card mb-8" style={{ marginLeft: "1rem", borderLeft: "4px solid var(--accent)" }}>
            <div className="mb-card-header" style={{ flexDirection: "row", justifyContent: "space-between", alignItems: "flex-start" }}>
              <div>
                <h3>Configuración del Reto</h3>
                <p>Define las reglas y características del reto.</p>
              </div>
              <button className="btn btn-danger btn-sm" type="button" onClick={removeItem}>
                Eliminar Reto
              </button>
            </div>
            <ChallengeEditor challenge={challenge} onChange={setItem as (c: ChallengeDraft) => void} />
          </div>
        );
      })}
    </>
  );
}

export function MissionBuilderCanvas({
  draft,
  onUpdateField,
  items,
  onItemsChange,
  selectedItemId,
  validationIssue,
  feedback,
}: MissionBuilderCanvasProps) {

  // Efecto para hacer scroll suave al elemento seleccionado
  useEffect(() => {
    if (selectedItemId) {
      setTimeout(() => {
        const element = document.getElementById(selectedItemId);
        if (element) {
          element.scrollIntoView({ behavior: "smooth", block: "start" });
        }
      }, 50);
    }
  }, [selectedItemId]);

  return (
    <main className="mb-canvas">
      <div className="mb-bento">
        {validationIssue && <div className="error-banner mb-4">{validationIssue}</div>}
        {feedback && <div className="success-banner mb-4">{feedback}</div>}

        <div id="mission_config" className="mb-card mb-8">
          <div className="mb-card-header">
            <h3>Configuración de la Misión</h3>
            <p>Define los parámetros globales para esta misión.</p>
          </div>
          
          <div className="form-group mb-4">
            <label className="form-label">Nombre</label>
            <input
              className="form-input"
              maxLength={120}
              onChange={(e) => onUpdateField("name", e.target.value)}
              required
              value={draft.name}
            />
          </div>
          
          <div className="form-group mb-4">
            <label className="form-label">Descripción</label>
            <textarea
              className="form-textarea"
              maxLength={1024}
              onChange={(e) => onUpdateField("description", e.target.value)}
              required
              rows={4}
              value={draft.description}
            />
          </div>
          
          <div className="form-group">
            <label className="form-label">Duración máxima (minutos)</label>
            <input
              className="form-input"
              max={1440}
              min={1}
              onChange={(e) => onUpdateField("maximumDurationMinutes", e.target.value)}
              required
              type="number"
              value={draft.maximumDurationMinutes}
              style={{ maxWidth: "200px" }}
            />
          </div>
        </div>

        <RecursiveItemRender items={items} onItemsChange={onItemsChange} />

      </div>
    </main>
  );
}
