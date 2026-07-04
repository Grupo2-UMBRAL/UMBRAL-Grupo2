import type { SessionTeamSnapshot } from "./api-client";
import type { StageNode } from "../components/progress-track";

export function formatGameType(gameType: string | undefined) {
  switch (gameType) {
    case "Trivia":
      return "Trivia";
    case "TreasureHunt":
    case "Treasure Hunt":
      return "Búsqueda de tesoro";
    default:
      return gameType ?? "Etapa";
  }
}

export function isTreasureHunt(gameType: string | undefined) {
  return gameType === "TreasureHunt" || gameType === "Treasure Hunt";
}

/** Player-friendly label for how a hint became visible (backend enum -> Spanish). */
export function formatUnlockReason(reason: string | undefined) {
  switch (reason) {
    case "Manual":
      return "Del operador";
    case "Rule":
      return "Automática";
    default:
      return reason ?? "Pista";
  }
}

export function formatDifficulty(difficulty: string | undefined) {
  switch (difficulty) {
    case "Easy":
      return "Fácil";
    case "Medium":
      return "Media";
    case "Hard":
      return "Difícil";
    default:
      return difficulty ?? "—";
  }
}

/** Base score table for first release (ERS): Easy=100, Medium=200, Hard=300. */
export function basePointsForDifficulty(difficulty: string | undefined) {
  switch (difficulty) {
    case "Easy":
      return 100;
    case "Medium":
      return 200;
    case "Hard":
      return 300;
    default:
      return null;
  }
}

export type StageProgress = {
  completed: number;
  total: number;
  nodes: StageNode[];
};

/**
 * Builds the guided stage path. When the snapshot exposes the full stage list
 * (revealed at finalization) we render every node with its real state. During
 * live play only the current stage is known, so we draw the completed run plus
 * the current stage and a single locked "next" marker without faking content.
 */
export function buildStageProgress(snapshot: SessionTeamSnapshot | null): StageProgress {
  if (!snapshot) {
    return { completed: 0, total: 0, nodes: [] };
  }

  const isFinalized = snapshot.sessionState === "Finalized";
  const currentOrder = snapshot.currentStage?.sessionStageOrder ?? 0;

  if (snapshot.allStages && snapshot.allStages.length > 0) {
    const ordered = [...snapshot.allStages].sort(
      (left, right) => left.sessionStageOrder - right.sessionStageOrder
    );

    const nodes = ordered.map<StageNode>((stage) => {
      const state = isFinalized || stage.sessionStageOrder < currentOrder
        ? "completed"
        : stage.sessionStageOrder === currentOrder
          ? "current"
          : "locked";

      return {
        key: stage.missionStageId,
        label: `Etapa ${stage.sessionStageOrder} · ${stage.name}`,
        sublabel: formatGameType(stage.gameType),
        state
      };
    });

    return {
      completed: nodes.filter((node) => node.state === "completed").length,
      total: ordered.length,
      nodes
    };
  }

  const completed = Math.max(0, currentOrder - 1);

  // During play the full stage list is hidden, but the snapshot exposes the stage COUNT — enough to
  // draw the whole linear path (completed / current / locked) without leaking future stage content.
  const totalStages = snapshot.totalStages ?? 0;
  if (totalStages > 0) {
    const nodes = Array.from({ length: totalStages }, (_, index) => {
      const order = index + 1;
      const isCurrent = order === currentOrder && Boolean(snapshot.currentStage);
      const state: StageNode["state"] =
        order < currentOrder ? "completed" : order === currentOrder ? "current" : "locked";

      return {
        key: isCurrent ? snapshot.currentStage!.missionStageId : `stage-${order}`,
        label: isCurrent
          ? `Etapa ${order} · ${snapshot.currentStage!.name}`
          : `Etapa ${order}`,
        sublabel: isCurrent
          ? formatGameType(snapshot.currentStage!.gameType)
          : order < currentOrder
            ? "Superada"
            : "Bloqueada",
        state
      };
    });

    return { completed, total: totalStages, nodes };
  }

  const nodes: StageNode[] = [];

  for (let order = 1; order <= completed; order += 1) {
    nodes.push({
      key: `done-${order}`,
      label: `Etapa ${order}`,
      sublabel: "Superada",
      state: "completed"
    });
  }

  if (snapshot.currentStage) {
    nodes.push({
      key: snapshot.currentStage.missionStageId,
      label: `Etapa ${snapshot.currentStage.sessionStageOrder} · ${snapshot.currentStage.name}`,
      sublabel: formatGameType(snapshot.currentStage.gameType),
      state: "current"
    });

    nodes.push({
      key: "next-locked",
      label: "Próxima etapa",
      sublabel: "Se revela al avanzar",
      state: "locked"
    });
  }

  return { completed, total: 0, nodes };
}
