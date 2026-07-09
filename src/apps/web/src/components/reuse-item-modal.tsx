import { useEffect, useMemo, useState } from "react";
import {
  type ChallengeDraft,
  type MissionDetail,
  type MissionItemDraft,
  type MissionSummary,
  type SectionDraft,
} from "./mission-authoring-types";
import { toMissionDraft } from "./mission-authoring-model";

type ReuseItemModalProps = {
  kind: "section" | "challenge";
  missions: MissionSummary[];
  currentMissionId: string | null;
  fetchMissionDetail: (missionId: string) => Promise<MissionDetail>;
  onClose: () => void;
  onCreateEmpty: () => void;
  onAddReused: (item: MissionItemDraft) => void;
};

type ReuseEntry = {
  key: string;
  item: MissionItemDraft;
  missionName: string;
  name: string;
  meta: string;
};

// Walks the (already draft-mapped) item tree of one mission, collecting every
// item that matches the requested kind — sections nest, so we recurse.
function collectEntries(
  items: MissionItemDraft[],
  missionName: string,
  kind: "section" | "challenge",
  out: ReuseEntry[],
) {
  for (const item of items) {
    if (item.kind === "Section") {
      if (kind === "section") {
        out.push(buildSectionEntry(item, missionName));
      }
      collectEntries(item.children, missionName, kind, out);
    } else if (kind === "challenge") {
      out.push(buildChallengeEntry(item, missionName));
    }
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

function buildSectionEntry(
  section: SectionDraft,
  missionName: string,
): ReuseEntry {
  const challengeCount = countChildChallenges(section);
  return {
    key: section.clientId,
    item: section,
    missionName,
    name: section.title || "Sección sin título",
    meta: `${challengeCount} ${challengeCount === 1 ? "reto" : "retos"}`,
  };
}

function translateGameType(gameType: string) {
  return gameType === "Treasure Hunt" ? "Tesoro" : gameType;
}

function translateDifficulty(difficulty: string) {
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

function buildChallengeEntry(
  challenge: ChallengeDraft,
  missionName: string,
): ReuseEntry {
  const isTrivia = challenge.gameType === "Trivia";
  const count = isTrivia
    ? challenge.questions.length
    : challenge.searches.length;
  const countLabel = isTrivia
    ? `${count} ${count === 1 ? "pregunta" : "preguntas"}`
    : `${count} ${count === 1 ? "búsqueda" : "búsquedas"}`;

  return {
    key: challenge.clientId,
    item: challenge,
    missionName,
    name: challenge.title || "Reto sin título",
    meta: `${translateGameType(challenge.gameType)} · ${translateDifficulty(
      challenge.difficulty,
    )} · ${countLabel}`,
  };
}

function ItemGlyph({ item }: { item: MissionItemDraft }) {
  if (item.kind === "Section") {
    return <span className="reuse-item-glyph reuse-item-glyph-section">▤</span>;
  }
  if (item.gameType === "Trivia") {
    return <span className="reuse-item-glyph reuse-item-glyph-trivia">?</span>;
  }
  return <span className="reuse-item-glyph reuse-item-glyph-treasure">◈</span>;
}

export function ReuseItemModal({
  kind,
  missions,
  currentMissionId,
  fetchMissionDetail,
  onClose,
  onCreateEmpty,
  onAddReused,
}: ReuseItemModalProps) {
  const [entries, setEntries] = useState<ReuseEntry[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const copy = useMemo(
    () =>
      kind === "section"
        ? {
            title: "Agregar sección",
            subtitle:
              "Reutilizá una sección de otra misión o creá una nueva vacía.",
            create: "Crear sección vacía",
            divider: "O reutilizar secciones existentes",
          }
        : {
            title: "Agregar reto",
            subtitle: "Reutilizá un reto de otra misión o creá uno nuevo vacío.",
            create: "Crear reto vacío",
            divider: "O reutilizar retos existentes",
          },
    [kind],
  );

  // Close on Escape.
  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        onClose();
      }
    }
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [onClose]);

  // Load reusable items from every OTHER mission on open.
  useEffect(() => {
    let cancelled = false;
    const sources = missions.filter(
      (mission) => mission.id !== currentMissionId,
    );

    if (sources.length === 0) {
      setEntries([]);
      setIsLoading(false);
      return;
    }

    setIsLoading(true);
    setError(null);

    void Promise.allSettled(
      sources.map((mission) => fetchMissionDetail(mission.id)),
    ).then((results) => {
      if (cancelled) {
        return;
      }

      const collected: ReuseEntry[] = [];
      let anyFailure = false;

      results.forEach((result) => {
        if (result.status === "fulfilled") {
          const draft = toMissionDraft(result.value);
          collectEntries(draft.items, result.value.name, kind, collected);
        } else {
          anyFailure = true;
        }
      });

      setEntries(collected);
      setError(
        anyFailure && collected.length === 0
          ? "No se pudieron cargar los elementos reutilizables."
          : null,
      );
      setIsLoading(false);
    });

    return () => {
      cancelled = true;
    };
  }, [missions, currentMissionId, fetchMissionDetail, kind]);

  return (
    <div
      className="reuse-modal-overlay"
      onClick={onClose}
      role="presentation"
    >
      <div
        aria-labelledby="reuse-modal-title"
        aria-modal="true"
        className="reuse-modal"
        onClick={(event) => event.stopPropagation()}
        role="dialog"
      >
        <div className="reuse-modal-header">
          <span className="reuse-modal-icon">
            {kind === "section" ? "▤" : "◈"}
          </span>
          <div className="stack-sm">
            <h3 className="reuse-modal-title" id="reuse-modal-title">
              {copy.title}
            </h3>
            <p className="reuse-modal-subtitle">{copy.subtitle}</p>
          </div>
          <button
            aria-label="Cerrar"
            className="reuse-modal-close"
            onClick={onClose}
            type="button"
          >
            ✕
          </button>
        </div>

        <div className="reuse-modal-body">
          <button
            className="reuse-create-row"
            onClick={onCreateEmpty}
            type="button"
          >
            <span className="reuse-create-icon">+</span>
            <span className="stack-sm">
              <span className="reuse-create-label">{copy.create}</span>
              <span className="reuse-create-sub">
                Empezá desde cero y configurá el contenido después.
              </span>
            </span>
          </button>

          <div className="reuse-divider">
            <span>{copy.divider}</span>
          </div>

          {isLoading ? (
            <div className="loading-center">Cargando reutilizables…</div>
          ) : null}

          {!isLoading && error ? (
            <div className="error-banner">{error}</div>
          ) : null}

          {!isLoading && !error && entries.length === 0 ? (
            <div className="empty-state">
              <strong>No hay elementos para reutilizar.</strong>
              <p>Creá contenido en otras misiones para poder reutilizarlo aquí.</p>
            </div>
          ) : null}

          {!isLoading && entries.length > 0 ? (
            <div className="reuse-list">
              {entries.map((entry) => (
                <button
                  className="reuse-item-row"
                  key={entry.key}
                  onClick={() => onAddReused(entry.item)}
                  type="button"
                >
                  <ItemGlyph item={entry.item} />
                  <span className="reuse-item-main">
                    <span className="reuse-item-name">{entry.name}</span>
                    <span className="reuse-item-detail">
                      <span className="reuse-item-origin">
                        {entry.missionName}
                      </span>
                      <span className="reuse-item-meta">{entry.meta}</span>
                    </span>
                  </span>
                  <span className="reuse-item-add">Agregar →</span>
                </button>
              ))}
            </div>
          ) : null}
        </div>
      </div>
    </div>
  );
}
