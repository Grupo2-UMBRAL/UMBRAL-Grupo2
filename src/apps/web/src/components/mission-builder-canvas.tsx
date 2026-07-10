import { useState } from "react";
import {
  DndContext,
  type DragEndEvent,
  KeyboardSensor,
  PointerSensor,
  closestCenter,
  useSensor,
  useSensors,
} from "@dnd-kit/core";
import {
  SortableContext,
  arrayMove,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import {
  type ChallengeDraft,
  type MissionDraft,
  type MissionItemDraft,
  type SectionDraft,
} from "./mission-authoring-types";
import { ChallengeEditor } from "./challenge-editor";
import {
  createEmptySectionDraft,
  createEmptyChallengeDraft,
  summarizeItems,
} from "./mission-authoring-model";

type MissionBuilderCanvasProps = {
  draft: MissionDraft;
  onUpdateField: (
    field: "name" | "description" | "maximumDurationMinutes",
    value: string,
  ) => void;
  items: MissionItemDraft[];
  onItemsChange: (items: MissionItemDraft[]) => void;
  validationIssue: string | null;
  feedback: string | null;
  isEditMode: boolean;
  missionIsActive?: boolean;
  onAddSection: () => void;
  onAddChallenge: () => void;
};

// --- presentation helpers --------------------------------------------------

function gameLabel(gameType: string) {
  return gameType === "Treasure Hunt" ? "Tesoro" : "Trivia";
}

function difficultyLabel(difficulty: string) {
  switch (difficulty) {
    case "Easy":
      return "Fácil";
    case "Medium":
      return "Medio";
    case "Hard":
      return "Difícil";
    default:
      return difficulty;
  }
}

function countChildChallenges(section: SectionDraft): number {
  return section.children.reduce(
    (total, child) =>
      child.kind === "Challenge"
        ? total + 1
        : total + countChildChallenges(child),
    0,
  );
}

function challengeSubtitle(challenge: ChallengeDraft): string {
  const parts = [gameLabel(challenge.gameType), difficultyLabel(challenge.difficulty)];
  if (challenge.timeLimitMinutes.trim()) {
    parts.push(`${challenge.timeLimitMinutes}m`);
  }
  if (challenge.gameType === "Treasure Hunt") {
    const n = challenge.searches.length;
    parts.push(`${n} ${n === 1 ? "búsqueda" : "búsquedas"}`, "QR");
  } else {
    const n = challenge.questions.length;
    parts.push(`${n} ${n === 1 ? "pregunta" : "preguntas"}`);
  }
  return parts.join(" · ");
}

function sectionSubtitle(section: SectionDraft): string {
  const n = countChildChallenges(section);
  return `Sección · ${n} ${n === 1 ? "reto" : "retos"}`;
}

// --- recursive structure ---------------------------------------------------

type NodeListProps = {
  items: MissionItemDraft[];
  onChange: (items: MissionItemDraft[]) => void;
  depth: number;
};

function NodeList({ items, onChange, depth }: NodeListProps) {
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );

  function handleDragEnd(event: DragEndEvent) {
    const { active, over } = event;
    if (!over || active.id === over.id) {
      return;
    }
    const oldIndex = items.findIndex((item) => item.clientId === active.id);
    const newIndex = items.findIndex((item) => item.clientId === over.id);
    if (oldIndex < 0 || newIndex < 0) {
      return;
    }
    onChange(arrayMove(items, oldIndex, newIndex));
  }

  function setItem(index: number, next: MissionItemDraft) {
    const nextItems = [...items];
    nextItems[index] = next;
    onChange(nextItems);
  }

  function removeItem(index: number) {
    const nextItems = [...items];
    nextItems.splice(index, 1);
    onChange(nextItems);
  }

  return (
    <DndContext
      collisionDetection={closestCenter}
      onDragEnd={handleDragEnd}
      sensors={sensors}
    >
      <SortableContext
        items={items.map((item) => item.clientId)}
        strategy={verticalListSortingStrategy}
      >
        <div className="mb-structure-list">
          {items.map((item, index) => (
            <SortableNode
              key={item.clientId}
              item={item}
              depth={depth}
              onChange={(next) => setItem(index, next)}
              onRemove={() => removeItem(index)}
            />
          ))}
        </div>
      </SortableContext>
    </DndContext>
  );
}

type SortableNodeProps = {
  item: MissionItemDraft;
  depth: number;
  onChange: (next: MissionItemDraft) => void;
  onRemove: () => void;
};

function SortableNode({ item, depth, onChange, onRemove }: SortableNodeProps) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } =
    useSortable({ id: item.clientId });

  const isSection = item.kind === "Section";
  // New / untitled nodes open by default so they are immediately editable;
  // existing, titled challenges start collapsed (overview). Sections stay open.
  const [open, setOpen] = useState(
    isSection || !item.id || !item.title.trim(),
  );

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.6 : 1,
  };

  const glyphClass = isSection
    ? "is-section"
    : (item as ChallengeDraft).gameType === "Trivia"
      ? "is-trivia"
      : "is-treasure";
  const glyphChar = isSection
    ? "▤"
    : (item as ChallengeDraft).gameType === "Trivia"
      ? "?"
      : "◈";

  const title =
    item.title.trim() || (isSection ? "Sección sin título" : "Reto sin título");
  const subtitle = isSection
    ? sectionSubtitle(item as SectionDraft)
    : challengeSubtitle(item as ChallengeDraft);

  // Colour follows the game type; root-level challenges read "Reto", nested
  // ones read their type (matches the design handoff prototype).
  const challengeBadgeClass =
    (item as ChallengeDraft).gameType === "Trivia" ? "badge badge-blue" : "badge badge-amber";
  const badge = isSection
    ? { className: "badge badge-muted", label: "Sección" }
    : {
        className: challengeBadgeClass,
        label:
          depth === 0
            ? "Reto"
            : (item as ChallengeDraft).gameType === "Trivia"
              ? "Trivia"
              : "Tesoro",
      };

  return (
    <div
      className={`mb-node ${isSection ? "is-section" : ""}`}
      ref={setNodeRef}
      style={style}
    >
      <div className="mb-node-head" onClick={() => setOpen((o) => !o)}>
        <span
          className="mb-node-drag"
          aria-label="Arrastrar para reordenar"
          onClick={(e) => e.stopPropagation()}
          {...attributes}
          {...listeners}
        >
          ⠿
        </span>
        <span className={`mb-node-glyph ${glyphClass}`}>{glyphChar}</span>
        <span className="mb-node-heading">
          <span className="mb-node-title">{title}</span>
          <span className="mb-node-sub">{subtitle}</span>
        </span>
        <span className="mb-node-badges">
          <span className={badge.className}>{badge.label}</span>
          <span className={`mb-node-chevron ${open ? "is-open" : ""}`}>
            <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <polyline points="9 6 15 12 9 18" />
            </svg>
          </span>
        </span>
      </div>

      {open ? (
        <div className="mb-node-body">
          <div className="mb-node-body-inner">
            {isSection ? (
              <SectionBody
                section={item as SectionDraft}
                depth={depth}
                onChange={onChange as (s: SectionDraft) => void}
                onRemove={onRemove}
              />
            ) : (
              <>
                <ChallengeEditor
                  challenge={item as ChallengeDraft}
                  onChange={onChange as (c: ChallengeDraft) => void}
                />
                <div>
                  <button
                    className="btn btn-danger btn-sm"
                    onClick={onRemove}
                    type="button"
                  >
                    Eliminar reto
                  </button>
                </div>
              </>
            )}
          </div>
        </div>
      ) : null}
    </div>
  );
}

type SectionBodyProps = {
  section: SectionDraft;
  depth: number;
  onChange: (next: SectionDraft) => void;
  onRemove: () => void;
};

function SectionBody({ section, depth, onChange, onRemove }: SectionBodyProps) {
  return (
    <>
      <div className="form-group">
        <label className="form-label">Título de la sección *</label>
        <input
          className="form-input"
          maxLength={120}
          onChange={(e) => onChange({ ...section, title: e.target.value })}
          placeholder="Ej: Fase 1 · Reconocimiento"
          required
          value={section.title}
        />
      </div>

      <div className="mb-node-child-actions">
        <button
          className="btn btn-ghost btn-sm"
          onClick={() =>
            onChange({
              ...section,
              children: [...section.children, createEmptySectionDraft()],
            })
          }
          type="button"
        >
          + Sub-sección
        </button>
        <button
          className="btn btn-ghost btn-sm"
          onClick={() =>
            onChange({
              ...section,
              children: [...section.children, createEmptyChallengeDraft()],
            })
          }
          type="button"
        >
          + Reto
        </button>
        <button
          className="btn btn-danger btn-sm"
          onClick={onRemove}
          type="button"
          style={{ marginLeft: "auto" }}
        >
          Eliminar sección
        </button>
      </div>

      {section.children.length > 0 ? (
        <div className="mb-node-children">
          <NodeList
            items={section.children}
            depth={depth + 1}
            onChange={(children) => onChange({ ...section, children })}
          />
        </div>
      ) : null}
    </>
  );
}

// --- canvas ----------------------------------------------------------------

export function MissionBuilderCanvas({
  draft,
  onUpdateField,
  items,
  onItemsChange,
  validationIssue,
  feedback,
  isEditMode,
  missionIsActive,
  onAddSection,
  onAddChallenge,
}: MissionBuilderCanvasProps) {
  const stats = summarizeItems(items);

  return (
    <main className="mb-main">
      <div className="mb-main-inner">
        {validationIssue ? (
          <div className="error-banner">{validationIssue}</div>
        ) : null}
        {feedback ? <div className="success-banner">{feedback}</div> : null}

        <div className="mb-mission-heading">
          <div className="mb-heading-top">
            <span className="eyebrow">
              {isEditMode ? "Misión seleccionada" : "Nueva misión"}
            </span>
            {isEditMode ? (
              <span
                className={`badge ${missionIsActive ? "badge-green" : "badge-muted"}`}
              >
                {missionIsActive ? "Activa" : "Inactiva"}
              </span>
            ) : null}
          </div>
          <input
            className="mb-title-input"
            maxLength={120}
            onChange={(e) => onUpdateField("name", e.target.value)}
            placeholder="Nombre de la misión"
            value={draft.name}
          />
          <textarea
            className="mb-desc-input"
            maxLength={1024}
            onChange={(e) => onUpdateField("description", e.target.value)}
            placeholder="Descripción de la misión…"
            rows={2}
            value={draft.description}
          />
        </div>

        <div className="mb-stats">
          <div className="mb-stat">
            <span className="mb-stat-label">Secciones</span>
            <span className="mb-stat-value">{stats.sections}</span>
          </div>
          <div className="mb-stat">
            <span className="mb-stat-label">Retos</span>
            <span className="mb-stat-value">{stats.challenges}</span>
          </div>
          <div className="mb-stat">
            <span className="mb-stat-label">Preguntas</span>
            <span className="mb-stat-value is-blue">{stats.questions}</span>
          </div>
          <div className="mb-stat">
            <span className="mb-stat-label">Búsquedas</span>
            <span className="mb-stat-value is-amber">{stats.searches}</span>
          </div>
          <div className="mb-stat is-duration">
            <span className="mb-stat-label">Duración máx (min)</span>
            <input
              className="mb-stat-input"
              max={1440}
              min={1}
              onChange={(e) =>
                onUpdateField("maximumDurationMinutes", e.target.value)
              }
              type="number"
              value={draft.maximumDurationMinutes}
            />
          </div>
        </div>

        <div className="mb-structure-header">
          <span className="mb-label">Estructura · Secciones y retos</span>
          <div className="mb-structure-actions">
            <button
              className="btn btn-ghost btn-sm"
              onClick={onAddSection}
              type="button"
            >
              + Sección
            </button>
            <button
              className="btn btn-primary btn-sm"
              onClick={onAddChallenge}
              type="button"
            >
              + Reto
            </button>
          </div>
        </div>

        {items.length === 0 ? (
          <div className="mb-empty-structure">
            <strong>Esta misión no tiene contenido todavía.</strong>
            <p className="text-muted text-sm">
              Agregá una sección o un reto para empezar a construir la
              estructura.
            </p>
          </div>
        ) : (
          <NodeList items={items} onChange={onItemsChange} depth={0} />
        )}
      </div>
    </main>
  );
}
