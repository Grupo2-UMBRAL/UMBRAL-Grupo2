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
  type MissionItemDraft,
  type SectionDraft,
} from "./mission-authoring-types";
import {
  createEmptyChallengeDraft,
  createEmptySectionDraft,
  gameTypeBadgeClass,
  summarizeItems,
} from "./mission-authoring-model";
import { ChallengeEditor } from "./challenge-editor";

type MissionItemListProps = {
  items: MissionItemDraft[];
  onChange: (items: MissionItemDraft[]) => void;
  depth: number;
};

type SortableItemProps = {
  item: MissionItemDraft;
  depth: number;
  onChangeItem: (next: MissionItemDraft) => void;
  onRemove: () => void;
};

function itemTitle(item: MissionItemDraft) {
  const trimmed = item.title.trim();
  if (trimmed) {
    return trimmed;
  }
  return item.kind === "Section" ? "Sección sin título" : "Reto sin título";
}

function SortableItem({ item, depth, onChangeItem, onRemove }: SortableItemProps) {
  const {
    attributes,
    listeners,
    setNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({ id: item.clientId });
  const [collapsed, setCollapsed] = useState(false);

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.6 : 1,
  };

  const isSection = item.kind === "Section";

  return (
    <div
      className={depth > 0 ? "node-item node-item-nested" : "node-item"}
      ref={setNodeRef}
      style={style}
    >
      <div className="node-item-header row-between">
        <div className="row-sm">
          <button
            aria-label="Arrastrar para reordenar"
            className="drag-handle"
            type="button"
            {...attributes}
            {...listeners}
          >
            ⠿
          </button>
          {isSection ? (
            <button
              aria-expanded={!collapsed}
              className="btn btn-ghost btn-sm"
              onClick={() => setCollapsed((value) => !value)}
              type="button"
            >
              {collapsed ? "▸" : "▾"}
            </button>
          ) : null}
          <div className="stack-sm">
            <div className="row-sm">
              {isSection ? (
                <span className="badge badge-muted">📋 Sección</span>
              ) : (
                <span className={gameTypeBadgeClass((item as ChallengeDraft).gameType)}>
                  {(item as ChallengeDraft).gameType === "Trivia"
                    ? "🎯 Trivia"
                    : "🗺️ Treasure Hunt"}
                </span>
              )}
              {!isSection ? (
                <span
                  className={
                    (item as ChallengeDraft).isActive
                      ? "badge badge-green"
                      : "badge badge-red"
                  }
                >
                  {(item as ChallengeDraft).isActive ? "Activo" : "Inactivo"}
                </span>
              ) : null}
            </div>
            <strong>{itemTitle(item)}</strong>
          </div>
        </div>

        <button
          className="btn btn-danger btn-sm"
          onClick={onRemove}
          type="button"
        >
          Eliminar
        </button>
      </div>

      {collapsed ? null : isSection ? (
        <SectionBody
          depth={depth}
          onChange={onChangeItem}
          section={item as SectionDraft}
        />
      ) : (
        <ChallengeEditor
          challenge={item as ChallengeDraft}
          onChange={onChangeItem}
        />
      )}
    </div>
  );
}

type SectionBodyProps = {
  section: SectionDraft;
  depth: number;
  onChange: (next: MissionItemDraft) => void;
};

// SectionEditor: edits { title, children } only — inert container, no game
// fields. Add-child controls append a Section or Challenge to its own scope.
function SectionBody({ section, depth, onChange }: SectionBodyProps) {
  const childStats = summarizeItems(section.children);

  function setTitle(title: string) {
    onChange({ ...section, title });
  }

  function setChildren(children: MissionItemDraft[]) {
    onChange({ ...section, children });
  }

  function addSection() {
    setChildren([...section.children, createEmptySectionDraft()]);
  }

  function addChallenge() {
    setChildren([...section.children, createEmptyChallengeDraft()]);
  }

  return (
    <div className="stack">
      <div className="form-group">
        <label className="form-label">Título de la sección *</label>
        <input
          className="form-input"
          maxLength={120}
          onChange={(event) => setTitle(event.target.value)}
          placeholder="Ej: Fase 1 - Reconocimiento"
          required
          value={section.title}
        />
      </div>

      <div className="card-section stack">
        <div className="card-header card-header-actions">
          <div className="stack-sm">
            <span className="eyebrow">
              Contenido ({childStats.sections} secciones · {childStats.challenges} retos)
            </span>
            <span className="form-hint">
              Arrastra para reordenar. Las secciones pueden anidar más secciones y retos.
            </span>
          </div>
          <div className="row-sm">
            <button
              className="btn btn-ghost btn-sm"
              onClick={addSection}
              type="button"
            >
              Agregar sección
            </button>
            <button
              className="btn btn-ghost btn-sm"
              onClick={addChallenge}
              type="button"
            >
              Agregar reto
            </button>
          </div>
        </div>

        {section.children.length === 0 ? (
          <div className="empty-state">
            <strong>Sección vacía.</strong>
            <p>Agrega secciones anidadas o retos jugables.</p>
          </div>
        ) : (
          <MissionItemList
            depth={depth + 1}
            items={section.children}
            onChange={setChildren}
          />
        )}
      </div>
    </div>
  );
}

// Recursive, drag-sortable list. Each instance owns its OWN DndContext so a
// drag is scoped to a single sibling list — items never jump across parents.
export function MissionItemList({ items, onChange, depth }: MissionItemListProps) {
  const sensors = useSensors(
    useSensor(PointerSensor, {
      activationConstraint: { distance: 6 },
    }),
    useSensor(KeyboardSensor, {
      coordinateGetter: sortableKeyboardCoordinates,
    }),
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

  function changeItem(clientId: string, next: MissionItemDraft) {
    onChange(items.map((item) => (item.clientId === clientId ? next : item)));
  }

  function removeItem(clientId: string) {
    onChange(items.filter((item) => item.clientId !== clientId));
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
        <div className="node-tree">
          {items.map((item) => (
            <SortableItem
              depth={depth}
              item={item}
              key={item.clientId}
              onChangeItem={(next) => changeItem(item.clientId, next)}
              onRemove={() => removeItem(item.clientId)}
            />
          ))}
        </div>
      </SortableContext>
    </DndContext>
  );
}
