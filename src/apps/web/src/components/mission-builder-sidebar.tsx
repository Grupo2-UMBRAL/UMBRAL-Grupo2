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
import { createEmptySectionDraft, createEmptyChallengeDraft, gameTypeBadgeClass } from "./mission-authoring-model";

type MissionBuilderSidebarProps = {
  items: MissionItemDraft[];
  onItemsChange: (items: MissionItemDraft[]) => void;
  selectedItemId: string;
  onSelect: (id: string) => void;
  onReuseItem?: (kind: "section" | "challenge") => void;
};

export function MissionBuilderSidebar({
  items,
  onItemsChange,
  selectedItemId,
  onSelect,
  onReuseItem,
}: MissionBuilderSidebarProps) {
  function addRootSection() {
    onItemsChange([...items, createEmptySectionDraft()]);
  }

  function addRootChallenge() {
    onItemsChange([...items, createEmptyChallengeDraft()]);
  }

  return (
    <aside className="mb-sidebar">
      <div className="mb-sidebar-content">
        {/* Mission Root Node */}
        <div 
          className={`mb-tree-row ${selectedItemId === "mission_config" ? "is-selected" : ""}`}
          onClick={() => onSelect("mission_config")}
        >
          <div className="mb-tree-content">
            <span className="badge badge-muted">⚙️</span>
            <span className="mb-tree-title">Configuración Principal</span>
          </div>
        </div>

        {/* Tree Items */}
        <SidebarItemList
          items={items}
          onChange={onItemsChange}
          selectedItemId={selectedItemId}
          onSelect={onSelect}
          depth={0}
        />
      </div>

      <div className="mb-sidebar-footer flex flex-col" style={{ gap: "0.5rem" }}>
        <button
          className="btn btn-ghost btn-block btn-sm"
          onClick={() => onReuseItem ? onReuseItem("section") : addRootSection()}
          type="button"
        >
          + Agregar Sección
        </button>
        <button
          className="btn btn-ghost btn-block btn-sm"
          onClick={() => onReuseItem ? onReuseItem("challenge") : addRootChallenge()}
          type="button"
        >
          + Agregar Reto
        </button>
      </div>
    </aside>
  );
}

// -----------------------------------------------------------------------------
// Recursive List and Items
// -----------------------------------------------------------------------------

type SidebarItemListProps = {
  items: MissionItemDraft[];
  onChange: (items: MissionItemDraft[]) => void;
  selectedItemId: string;
  onSelect: (id: string) => void;
  depth: number;
};

function SidebarItemList({ items, onChange, selectedItemId, onSelect, depth }: SidebarItemListProps) {
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
        <div className="mb-tree-list">
          {items.map((item) => (
            <SidebarSortableItem
              key={item.clientId}
              item={item}
              onChangeItem={(next) => changeItem(item.clientId, next)}
              selectedItemId={selectedItemId}
              onSelect={onSelect}
              depth={depth}
            />
          ))}
        </div>
      </SortableContext>
    </DndContext>
  );
}

type SidebarSortableItemProps = {
  item: MissionItemDraft;
  onChangeItem: (next: MissionItemDraft) => void;
  selectedItemId: string;
  onSelect: (id: string) => void;
  depth: number;
};

function SidebarSortableItem({
  item,
  onChangeItem,
  selectedItemId,
  onSelect,
  depth,
}: SidebarSortableItemProps) {
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
  const title = item.title.trim() || (isSection ? "Sección sin título" : "Reto sin título");

  return (
    <div className="mb-tree-item" ref={setNodeRef} style={style}>
      <div 
        className={`mb-tree-row ${selectedItemId === item.clientId ? "is-selected" : ""}`}
        onClick={() => onSelect(item.clientId)}
      >
        <div className="mb-tree-content">
          {isSection && (
            <button
              className="btn-icon"
              style={{ padding: "2px", width: "20px", height: "20px", display: "flex", alignItems: "center", justifyContent: "center" }}
              onClick={(e) => {
                e.stopPropagation();
                setCollapsed((c) => !c);
              }}
              type="button"
            >
              {collapsed ? "▸" : "▾"}
            </button>
          )}
          {!isSection && <span style={{ width: "20px", display: "inline-block" }}></span>}
          
          <span className="text-xs">
            {isSection ? "📋" : ((item as ChallengeDraft).gameType === "Trivia" ? "🎯" : "🗺️")}
          </span>
          <span className="mb-tree-title">{title}</span>
        </div>
        
        <div className="mb-tree-actions">
          <div
            className="mb-tree-drag"
            aria-label="Arrastrar para reordenar"
            {...attributes}
            {...listeners}
          >
            ⠿
          </div>
        </div>
      </div>

      {isSection && !collapsed && (
        <div className="mb-tree-children">
          <SidebarItemList
            items={(item as SectionDraft).children}
            onChange={(children) => onChangeItem({ ...item, children })}
            selectedItemId={selectedItemId}
            onSelect={onSelect}
            depth={depth + 1}
          />
        </div>
      )}
    </div>
  );
}
