import type { MissionSummary } from "./mission-authoring-types";

type MissionListColumnProps = {
  missions: MissionSummary[];
  selectedMissionId: string | null;
  isLoading: boolean;
  collapsed: boolean;
  onToggleCollapse: () => void;
  onSelect: (id: string) => void;
  onCreate: () => void;
  // Optional stage (section) count per mission, keyed by id. Not part of the
  // list contract yet — see docs/endpoints_faltantes.md. When absent we show
  // the duration only.
  stageCounts?: Record<string, number>;
};

export function MissionListColumn({
  missions,
  selectedMissionId,
  isLoading,
  collapsed,
  onToggleCollapse,
  onSelect,
  onCreate,
  stageCounts,
}: MissionListColumnProps) {
  return (
    <aside className={`mb-list ${collapsed ? "is-collapsed" : ""}`}>
      <div className="mb-list-header">
        <span className="mb-label">Catálogo</span>
        <div className="mb-list-header-actions">
          <span className="text-muted text-xs">
            {missions.length} {missions.length === 1 ? "misión" : "misiones"}
          </span>
          <button
            className="mb-icon-btn"
            onClick={onCreate}
            type="button"
            title="Nueva misión"
            aria-label="Nueva misión"
          >
            <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
              <line x1="12" y1="5" x2="12" y2="19" />
              <line x1="5" y1="12" x2="19" y2="12" />
            </svg>
          </button>
          <button
            className="mb-icon-btn"
            onClick={onToggleCollapse}
            type="button"
            title="Ocultar catálogo"
            aria-label="Ocultar catálogo de misiones"
          >
            <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
              <polyline points="15 18 9 12 15 6" />
            </svg>
          </button>
        </div>
      </div>

      <div className="mb-list-scroll">
        {isLoading ? (
          <div className="loading-center">Cargando misiones…</div>
        ) : null}

        {!isLoading && missions.length === 0 ? (
          <div className="empty-state">
            <strong>Aún no hay misiones.</strong>
            <p>Creá la primera misión para el catálogo.</p>
          </div>
        ) : null}

        {missions.map((mission) => {
          const stages = stageCounts?.[mission.id];
          return (
            <button
              className={`mb-mission-card ${
                mission.id === selectedMissionId ? "is-selected" : ""
              }`}
              key={mission.id}
              onClick={() => onSelect(mission.id)}
              type="button"
            >
              <div className="mb-mission-card-top">
                <span
                  className={`mb-mission-dot ${
                    mission.isActive ? "is-active" : "is-inactive"
                  }`}
                />
                <span className="mb-mission-card-name">{mission.name}</span>
              </div>
              <div className="mb-mission-card-meta">
                <span>
                  {typeof stages === "number"
                    ? `${stages} ${stages === 1 ? "etapa" : "etapas"} · `
                    : ""}
                  {mission.maximumDurationMinutes}m
                </span>
                <span
                  className={`badge ${
                    mission.isActive ? "badge-green" : "badge-muted"
                  }`}
                  style={{ marginLeft: "auto" }}
                >
                  {mission.isActive ? "Activa" : "Inactiva"}
                </span>
              </div>
            </button>
          );
        })}
      </div>
    </aside>
  );
}
