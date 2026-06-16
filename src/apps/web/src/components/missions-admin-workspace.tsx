"use client";

import {
  type FormEvent,
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import { getClientConfig } from "@/lib/config";
import { HintEditor } from "./hint-editor";
import { GameTypeConfig } from "./game-type-config";

type MissionSummary = {
  id: string;
  name: string;
  difficulty: string;
  maximumDurationMinutes: number;
  gameType: string;
  isActive: boolean;
};

type MissionHint = {
  id: string;
  content: string;
  isSolution: boolean;
  latitude: number | null;
  longitude: number | null;
};

type MissionNode = {
  id: string;
  name: string;
  order: number;
  isActive: boolean;
  defaultTimeBudgetMinutes: number | null;
  timeBudgetMinutes: number | null;
  difficulty: string | null;
  resolvedTimeBudgetMinutes: number | null;
  gameType: string | null;
  prompt: string | null;
  expectedQrHash: string | null;
  triviaValidAnswer: string | null;
  triviaInitialValidationCriterion: string | null;
  isLeaf: boolean;
  hints: MissionHint[];
  children: MissionNode[];
};

type MissionDetail = MissionSummary & {
  description: string;
  nodes: MissionNode[];
};

type MissionHintDraft = {
  clientId: string;
  id?: string;
  content: string;
  isSolution: boolean;
  latitude: string;
  longitude: string;
};

type MissionNodeDraft = {
  clientId: string;
  id?: string;
  name: string;
  order: string;
  isActive: boolean;
  defaultTimeBudgetMinutes: string;
  timeBudgetMinutes: string;
  difficulty: string;
  gameType: string;
  prompt: string;
  expectedQrHash: string;
  triviaValidAnswer: string;
  triviaInitialValidationCriterion: string;
  hints: MissionHintDraft[];
  children: MissionNodeDraft[];
};

type MissionDraft = {
  name: string;
  description: string;
  difficulty: string;
  maximumDurationMinutes: string;
  gameType: string;
  nodes: MissionNodeDraft[];
};

type MissionTreeStats = {
  totalNodes: number;
  leafStages: number;
  compositeBlocks: number;
  hints: number;
};

type MissionHintPayload = {
  id?: string;
  content: string;
  isSolution: boolean;
  latitude: number | null;
  longitude: number | null;
};

type MissionNodePayload = {
  id?: string;
  name: string;
  order: number;
  isActive: boolean;
  defaultTimeBudgetMinutes: number | null;
  timeBudgetMinutes: number | null;
  difficulty: string | null;
  gameType: string | null;
  prompt: string | null;
  expectedQrHash: string | null;
  triviaValidAnswer: string | null;
  triviaInitialValidationCriterion: string | null;
  hints: MissionHintPayload[];
  children: MissionNodePayload[];
};

type MissionsAdminWorkspaceProps = {
  accessToken: string;
};

type MissionNodeEditorProps = {
  depth: number;
  node: MissionNodeDraft;
  onAddChild: (nodeClientId: string, kind: "leaf" | "composite") => void;
  onAddHint: (nodeClientId: string) => void;
  onMakeComposite: (nodeClientId: string) => void;
  onMakeLeaf: (nodeClientId: string) => void;
  onRemoveHint: (nodeClientId: string, hintClientId: string) => void;
  onRemoveNode: (nodeClientId: string) => void;
  onUpdateHint: (
    nodeClientId: string,
    hintClientId: string,
    field: keyof MissionHintDraft,
    value: string | boolean,
  ) => void;
  onUpdateNode: (
    nodeClientId: string,
    field: keyof MissionNodeDraft,
    value: string | boolean,
  ) => void;
};

const gameTypeOptions = ["Treasure Hunt", "Trivia"] as const;
const difficultyOptions = ["Easy", "Medium", "Hard"] as const;
const defaultDifficulty = "Medium";

function normalizeDifficulty(value: string | null | undefined) {
  const normalized = value?.trim().toLowerCase();

  return (
    difficultyOptions.find(
      (difficulty) => difficulty.toLowerCase() === normalized,
    ) ?? defaultDifficulty
  );
}

function isSupportedDifficulty(value: string) {
  return difficultyOptions.some((difficulty) => difficulty === value);
}

function createClientId() {
  if (
    typeof crypto !== "undefined" &&
    typeof crypto.randomUUID === "function"
  ) {
    return crypto.randomUUID();
  }

  return `draft-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

function createEmptyHintDraft(): MissionHintDraft {
  return {
    clientId: createClientId(),
    content: "",
    isSolution: false,
    latitude: "",
    longitude: "",
  };
}

function createEmptyNodeDraft(
  kind: "leaf" | "composite",
  order: number,
): MissionNodeDraft {
  return {
    clientId: createClientId(),
    name: "",
    order: String(order),
    isActive: true,
    defaultTimeBudgetMinutes: "",
    timeBudgetMinutes: "",
    difficulty: defaultDifficulty,
    gameType: gameTypeOptions[0],
    prompt: "",
    expectedQrHash: "",
    triviaValidAnswer: "",
    triviaInitialValidationCriterion: "",
    hints: [],
    children: kind === "composite" ? [createEmptyNodeDraft("leaf", 1)] : [],
  };
}

function createEmptyMissionDraft(): MissionDraft {
  return {
    name: "",
    description: "",
    difficulty: defaultDifficulty,
    maximumDurationMinutes: "60",
    gameType: gameTypeOptions[0],
    nodes: [],
  };
}

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

function toHintDraft(hint: MissionHint): MissionHintDraft {
  return {
    clientId: hint.id,
    id: hint.id,
    content: hint.content,
    isSolution: hint.isSolution,
    latitude: hint.latitude?.toString() ?? "",
    longitude: hint.longitude?.toString() ?? "",
  };
}

function toNodeDraft(node: MissionNode): MissionNodeDraft {
  return {
    clientId: node.id,
    id: node.id,
    name: node.name,
    order: String(node.order),
    isActive: node.isActive,
    defaultTimeBudgetMinutes: node.defaultTimeBudgetMinutes?.toString() ?? "",
    timeBudgetMinutes: node.timeBudgetMinutes?.toString() ?? "",
    difficulty: normalizeDifficulty(node.difficulty),
    gameType: node.gameType ?? gameTypeOptions[0],
    prompt: node.prompt ?? "",
    expectedQrHash: node.expectedQrHash ?? "",
    triviaValidAnswer: node.triviaValidAnswer ?? "",
    triviaInitialValidationCriterion:
      node.triviaInitialValidationCriterion ?? "",
    hints: node.hints.map(toHintDraft),
    children: node.children.map(toNodeDraft),
  };
}

function toDraft(mission: MissionDetail): MissionDraft {
  return {
    name: mission.name,
    description: mission.description,
    difficulty: normalizeDifficulty(mission.difficulty),
    maximumDurationMinutes: String(mission.maximumDurationMinutes),
    gameType: mission.gameType,
    nodes: mission.nodes.map(toNodeDraft),
  };
}

function summarizeSelection(missions: MissionSummary[]) {
  const active = missions.filter((mission) => mission.isActive).length;

  return {
    total: missions.length,
    active,
    inactive: missions.length - active,
  };
}

function summarizeMissionTree(nodes: MissionNodeDraft[]): MissionTreeStats {
  return nodes.reduce<MissionTreeStats>(
    (summary, node) => {
      const childSummary = summarizeMissionTree(node.children);

      return {
        totalNodes: summary.totalNodes + 1 + childSummary.totalNodes,
        leafStages:
          summary.leafStages +
          (node.children.length === 0 ? 1 : 0) +
          childSummary.leafStages,
        compositeBlocks:
          summary.compositeBlocks +
          (node.children.length > 0 ? 1 : 0) +
          childSummary.compositeBlocks,
        hints: summary.hints + node.hints.length + childSummary.hints,
      };
    },
    {
      totalNodes: 0,
      leafStages: 0,
      compositeBlocks: 0,
      hints: 0,
    },
  );
}

function getNextOrder(nodes: MissionNodeDraft[]) {
  return (
    nodes.reduce((maximum, node) => {
      const numericOrder = Number.parseInt(node.order, 10);
      return Number.isFinite(numericOrder)
        ? Math.max(maximum, numericOrder)
        : maximum;
    }, 0) + 1
  );
}

function updateNodeInTree(
  nodes: MissionNodeDraft[],
  targetClientId: string,
  updater: (node: MissionNodeDraft) => MissionNodeDraft,
): MissionNodeDraft[] {
  return nodes.map((node) => {
    if (node.clientId === targetClientId) {
      return updater(node);
    }

    if (node.children.length === 0) {
      return node;
    }

    return {
      ...node,
      children: updateNodeInTree(node.children, targetClientId, updater),
    };
  });
}

function removeNodeFromTree(
  nodes: MissionNodeDraft[],
  targetClientId: string,
): MissionNodeDraft[] {
  return nodes
    .filter((node) => node.clientId !== targetClientId)
    .map((node) => ({
      ...node,
      children: removeNodeFromTree(node.children, targetClientId),
    }));
}

function trimToNull(value: string) {
  const normalized = value.trim();
  return normalized ? normalized : null;
}

function parseOptionalInteger(value: string) {
  const normalized = value.trim();
  if (!normalized) {
    return null;
  }

  const parsed = Number.parseInt(normalized, 10);
  return Number.isFinite(parsed) ? parsed : Number.NaN;
}

function parseOptionalDecimal(value: string) {
  const normalized = value.trim();
  if (!normalized) {
    return null;
  }

  const parsed = Number.parseFloat(normalized);
  return Number.isFinite(parsed) ? parsed : Number.NaN;
}

function serializeHintDraft(hint: MissionHintDraft): MissionHintPayload {
  return {
    id: hint.id,
    content: hint.content,
    isSolution: hint.isSolution,
    latitude: parseOptionalDecimal(hint.latitude),
    longitude: parseOptionalDecimal(hint.longitude),
  };
}

function serializeNodeDraft(node: MissionNodeDraft): MissionNodePayload {
  const isLeaf = node.children.length === 0;

  return {
    id: node.id,
    name: node.name,
    order: Number.parseInt(node.order, 10),
    isActive: node.isActive,
    defaultTimeBudgetMinutes: isLeaf
      ? null
      : parseOptionalInteger(node.defaultTimeBudgetMinutes),
    timeBudgetMinutes: isLeaf
      ? parseOptionalInteger(node.timeBudgetMinutes)
      : null,
    difficulty: isLeaf ? normalizeDifficulty(node.difficulty) : null,
    gameType: isLeaf ? node.gameType : null,
    prompt: isLeaf ? trimToNull(node.prompt) : null,
    expectedQrHash:
      isLeaf && node.gameType === "Treasure Hunt"
        ? trimToNull(node.expectedQrHash)
        : null,
    triviaValidAnswer:
      isLeaf && node.gameType === "Trivia"
        ? trimToNull(node.triviaValidAnswer)
        : null,
    triviaInitialValidationCriterion:
      isLeaf && node.gameType === "Trivia"
        ? trimToNull(node.triviaInitialValidationCriterion)
        : null,
    hints: isLeaf ? node.hints.map(serializeHintDraft) : [],
    children: isLeaf ? [] : node.children.map(serializeNodeDraft),
  };
}

function findValidationIssue(
  nodes: MissionNodeDraft[],
  inheritedBudgetMinutes: number | null,
  pathPrefix: string,
): string | null {
  const orders = new Map<number, string>();

  for (const node of nodes) {
    const trimmedName = node.name.trim();
    if (!trimmedName) {
      return `${pathPrefix}: el nombre del nodo es obligatorio.`;
    }

    const order = Number.parseInt(node.order, 10);
    if (!Number.isFinite(order) || order <= 0) {
      return `${pathPrefix}: el nodo "${trimmedName || "Sin título"}" necesita un orden positivo.`;
    }

    if (orders.has(order)) {
      return `${pathPrefix}: el orden ${order} está duplicado entre hermanos.`;
    }

    orders.set(order, trimmedName);
  }

  for (const node of nodes) {
    const label = `${pathPrefix} / ${node.name.trim()}`;
    const isLeaf = node.children.length === 0;
    const defaultBudget = parseOptionalInteger(node.defaultTimeBudgetMinutes);
    const ownBudget = parseOptionalInteger(node.timeBudgetMinutes);

    if (defaultBudget !== null && !Number.isFinite(defaultBudget)) {
      return `${label}: el presupuesto de tiempo por defecto debe ser un número.`;
    }

    if (ownBudget !== null && !Number.isFinite(ownBudget)) {
      return `${label}: el presupuesto de tiempo debe ser un número.`;
    }

    if (isLeaf) {
      const effectiveBudget = ownBudget ?? inheritedBudgetMinutes;

      if (!effectiveBudget || effectiveBudget <= 0) {
        return `${label}: la etapa hoja necesita un presupuesto de tiempo local o heredado de la misión.`;
      }

      if (!node.gameType) {
        return `${label}: la etapa hoja necesita un tipo de juego.`;
      }

      if (!isSupportedDifficulty(node.difficulty)) {
        return `${label}: la etapa hoja necesita una dificultad válida.`;
      }

      if (!trimToNull(node.prompt)) {
        return `${label}: la etapa hoja necesita un enunciado visible para los participantes.`;
      }

      if (
        node.gameType === "Treasure Hunt" &&
        !trimToNull(node.expectedQrHash)
      ) {
        return `${label}: la etapa Treasure Hunt necesita el hash QR esperado.`;
      }

      if (
        node.gameType === "Trivia" &&
        !trimToNull(node.triviaValidAnswer) &&
        !trimToNull(node.triviaInitialValidationCriterion)
      ) {
        return `${label}: la etapa Trivia necesita una respuesta válida o un criterio de validación.`;
      }

      for (const hint of node.hints) {
        const hintLabel = `${label} / pista`;
        if (!hint.content.trim()) {
          return `${hintLabel}: el contenido es obligatorio.`;
        }

        const hasLatitude = hint.latitude.trim().length > 0;
        const hasLongitude = hint.longitude.trim().length > 0;
        if (hasLatitude !== hasLongitude) {
          return `${hintLabel}: las coordenadas necesitan tanto latitud como longitud.`;
        }

        if (
          hasLatitude &&
          !Number.isFinite(parseOptionalDecimal(hint.latitude))
        ) {
          return `${hintLabel}: la latitud debe ser numérica.`;
        }

        if (
          hasLongitude &&
          !Number.isFinite(parseOptionalDecimal(hint.longitude))
        ) {
          return `${hintLabel}: la longitud debe ser numérica.`;
        }
      }

      continue;
    }

    const nextInheritedBudget = defaultBudget ?? inheritedBudgetMinutes;
    const issue = findValidationIssue(
      node.children,
      nextInheritedBudget,
      label,
    );
    if (issue) {
      return issue;
    }
  }

  return null;
}

function difficultyBadgeClass(difficulty: string) {
  switch (difficulty) {
    case "Easy":
      return "badge badge-green";
    case "Medium":
      return "badge badge-amber";
    case "Hard":
      return "badge badge-red";
    default:
      return "badge badge-muted";
  }
}

function gameTypeBadgeClass(gameType: string) {
  switch (gameType) {
    case "Treasure Hunt":
      return "badge badge-blue";
    case "Trivia":
      return "badge badge-accent";
    default:
      return "badge badge-muted";
  }
}

function MissionNodeEditor({
  depth,
  node,
  onAddChild,
  onAddHint,
  onMakeComposite,
  onMakeLeaf,
  onRemoveHint,
  onRemoveNode,
  onUpdateHint,
  onUpdateNode,
}: MissionNodeEditorProps) {
  const isLeaf = node.children.length === 0;

  return (
    <div
      className={depth > 0 ? "node-item node-item-nested" : "node-item"}
      style={depth > 0 ? { marginLeft: `${depth * 16}px` } : undefined}
    >
      <div className="node-item-header row-between">
        <div className="stack-sm">
          <div className="row-sm">
            {isLeaf ? (
              <span className={gameTypeBadgeClass(node.gameType)}>
                {node.gameType === "Trivia"
                  ? "🎯 Trivia"
                  : node.gameType === "Treasure Hunt"
                    ? "🗺️ Treasure Hunt"
                    : "🎯 Actividad"}
              </span>
            ) : (
              <span className="badge badge-muted">📋 Etapa</span>
            )}
            <span
              className={
                node.isActive ? "badge badge-green" : "badge badge-red"
              }
            >
              {node.isActive ? "Activa" : "Inactiva"}
            </span>
          </div>
          <strong>
            {node.name.trim() ||
              (isLeaf ? "Actividad sin título" : "Etapa sin título")}
          </strong>
          <p className="text-muted text-sm">
            {isLeaf
              ? "Actividad jugable con validación, pistas y tiempo configurables."
              : "Etapa compuesta. Las actividades hijas heredan el tiempo de esta etapa salvo que definan el propio."}
          </p>
        </div>

        <div className="node-item-actions row-sm">
          {isLeaf ? (
            <button
              className="btn btn-ghost btn-sm"
              onClick={() => onMakeComposite(node.clientId)}
              type="button"
            >
              Hacer etapa compuesta
            </button>
          ) : (
            <button
              className="btn btn-ghost btn-sm"
              onClick={() => onMakeLeaf(node.clientId)}
              type="button"
            >
              Convertir en actividad
            </button>
          )}
          <button
            className="btn btn-danger btn-sm"
            onClick={() => onRemoveNode(node.clientId)}
            type="button"
          >
            Eliminar
          </button>
        </div>
      </div>

      <div className="form-row">
        <div className="form-group">
          <label className="form-label">Nombre *</label>
          <input
            className="form-input"
            maxLength={120}
            onChange={(event) =>
              onUpdateNode(node.clientId, "name", event.target.value)
            }
            placeholder={
              isLeaf
                ? "Ej: Curiosidades de Halloween"
                : "Ej: Fase 1 - Recolección de información"
            }
            required
            value={node.name}
          />
        </div>

        <div className="form-group">
          <label className="form-label">Orden</label>
          <input
            className="form-input"
            min={1}
            onChange={(event) =>
              onUpdateNode(node.clientId, "order", event.target.value)
            }
            required
            type="number"
            value={node.order}
          />
        </div>

        <div className="form-group">
          <label className="checkbox-label">
            <input
              checked={node.isActive}
              onChange={(event) =>
                onUpdateNode(node.clientId, "isActive", event.target.checked)
              }
              type="checkbox"
            />
            Activa
          </label>
        </div>
      </div>

      {isLeaf ? (
        <>
          <div className="form-row">
            <div className="form-group">
              <label className="form-label">Presupuesto de tiempo (min)</label>
              <input
                className="form-input"
                min={1}
                onChange={(event) =>
                  onUpdateNode(
                    node.clientId,
                    "timeBudgetMinutes",
                    event.target.value,
                  )
                }
                type="number"
                value={node.timeBudgetMinutes}
              />
              <span className="form-hint">
                Dejar vacío para heredar el presupuesto de la misión o del
                bloque padre.
              </span>
            </div>

            <div className="form-group">
              <label className="form-label">Dificultad</label>
              <select
                className="form-select"
                onChange={(event) =>
                  onUpdateNode(node.clientId, "difficulty", event.target.value)
                }
                required
                value={node.difficulty}
              >
                {difficultyOptions.map((difficulty) => (
                  <option key={difficulty} value={difficulty}>
                    {difficulty}
                  </option>
                ))}
              </select>
            </div>

            <div className="form-group">
              <label className="form-label">Tipo de juego</label>
              <select
                className="form-select"
                onChange={(event) =>
                  onUpdateNode(node.clientId, "gameType", event.target.value)
                }
                value={node.gameType}
              >
                {gameTypeOptions.map((gameType) => (
                  <option key={gameType} value={gameType}>
                    {gameType}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <GameTypeConfig
            gameType={node.gameType}
            prompt={node.prompt}
            expectedQrHash={node.expectedQrHash}
            triviaValidAnswer={node.triviaValidAnswer}
            triviaInitialValidationCriterion={
              node.triviaInitialValidationCriterion
            }
            onUpdate={(field, value) =>
              onUpdateNode(
                node.clientId,
                field as keyof MissionNodeDraft,
                value,
              )
            }
          />

          <div className="card-section stack-sm">
            <div className="card-header card-header-actions">
              <div className="stack-sm">
                <span className="eyebrow">Pistas</span>
                <strong>Claves y soluciones</strong>
              </div>
              <button
                className="btn btn-ghost btn-sm"
                onClick={() => onAddHint(node.clientId)}
                type="button"
              >
                Agregar pista
              </button>
            </div>

            {node.hints.length === 0 ? (
              <div className="empty-state">
                <strong>Aún no hay pistas.</strong>
                <p>
                  Las etapas Treasure Hunt y Trivia pueden llevar claves
                  visibles o soluciones finales.
                </p>
              </div>
            ) : null}

            <div className="stack-sm">
              {node.hints.map((hint, index) => (
                <HintEditor
                  key={hint.clientId}
                  hint={hint}
                  index={index}
                  onUpdate={(field, value) =>
                    onUpdateHint(node.clientId, hint.clientId, field, value)
                  }
                  onRemove={() => onRemoveHint(node.clientId, hint.clientId)}
                />
              ))}
            </div>
          </div>
        </>
      ) : (
        <>
          <div className="form-group">
            <label className="form-label">
              Presupuesto de tiempo por defecto (min)
            </label>
            <input
              className="form-input"
              min={1}
              onChange={(event) =>
                onUpdateNode(
                  node.clientId,
                  "defaultTimeBudgetMinutes",
                  event.target.value,
                )
              }
              type="number"
              value={node.defaultTimeBudgetMinutes}
            />
            <span className="form-hint">
              Sobreescritura opcional para todos los descendientes que no
              definan un presupuesto de hoja.
            </span>
          </div>

          <div className="card-section stack-sm">
            <div className="card-header card-header-actions">
              <div className="stack-sm">
                <span className="eyebrow">Hijos</span>
                <strong>Flujo anidado</strong>
              </div>
              <div className="row-sm">
                <button
                  className="btn btn-ghost btn-sm"
                  onClick={() => onAddChild(node.clientId, "leaf")}
                  type="button"
                >
                  Agregar etapa
                </button>
                <button
                  className="btn btn-ghost btn-sm"
                  onClick={() => onAddChild(node.clientId, "composite")}
                  type="button"
                >
                  Agregar bloque
                </button>
              </div>
            </div>

            <div className="node-tree">
              {node.children.map((child) => (
                <MissionNodeEditor
                  depth={depth + 1}
                  key={child.clientId}
                  node={child}
                  onAddChild={onAddChild}
                  onAddHint={onAddHint}
                  onMakeComposite={onMakeComposite}
                  onMakeLeaf={onMakeLeaf}
                  onRemoveHint={onRemoveHint}
                  onRemoveNode={onRemoveNode}
                  onUpdateHint={onUpdateHint}
                  onUpdateNode={onUpdateNode}
                />
              ))}
            </div>
          </div>
        </>
      )}
    </div>
  );
}

export function MissionsAdminWorkspace({
  accessToken,
}: MissionsAdminWorkspaceProps) {
  const config = getClientConfig();
  const missionsUrl = `${config.edgeProxyPublicBaseUrl}/mission-design/api/mission-design/missions`;
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
  const missionTreeStats = useMemo(
    () => summarizeMissionTree(draft.nodes),
    [draft.nodes],
  );

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
        setDraft(toDraft(mission));
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
    (field: keyof MissionDraft, value: string) => {
      setDraft((current) => ({
        ...current,
        [field]: value,
      }));
    },
    [],
  );

  const updateNodeField = useCallback(
    (
      nodeClientId: string,
      field: keyof MissionNodeDraft,
      value: string | boolean,
    ) => {
      setDraft((current) => ({
        ...current,
        nodes: updateNodeInTree(current.nodes, nodeClientId, (node) => ({
          ...node,
          [field]: value,
        })),
      }));
    },
    [],
  );

  const updateHintField = useCallback(
    (
      nodeClientId: string,
      hintClientId: string,
      field: keyof MissionHintDraft,
      value: string | boolean,
    ) => {
      setDraft((current) => ({
        ...current,
        nodes: updateNodeInTree(current.nodes, nodeClientId, (node) => ({
          ...node,
          hints: node.hints.map((hint) =>
            hint.clientId === hintClientId
              ? {
                  ...hint,
                  [field]: value,
                }
              : hint,
          ),
        })),
      }));
    },
    [],
  );

  const addRootNode = useCallback((kind: "leaf" | "composite") => {
    setDraft((current) => ({
      ...current,
      nodes: [
        ...current.nodes,
        createEmptyNodeDraft(kind, getNextOrder(current.nodes)),
      ],
    }));
  }, []);

  const addChildNode = useCallback(
    (nodeClientId: string, kind: "leaf" | "composite") => {
      setDraft((current) => ({
        ...current,
        nodes: updateNodeInTree(current.nodes, nodeClientId, (node) => ({
          ...node,
          children: [
            ...node.children,
            createEmptyNodeDraft(kind, getNextOrder(node.children)),
          ],
        })),
      }));
    },
    [],
  );

  const addHint = useCallback((nodeClientId: string) => {
    setDraft((current) => ({
      ...current,
      nodes: updateNodeInTree(current.nodes, nodeClientId, (node) => ({
        ...node,
        hints: [...node.hints, createEmptyHintDraft()],
      })),
    }));
  }, []);

  const removeNode = useCallback((nodeClientId: string) => {
    setDraft((current) => ({
      ...current,
      nodes: removeNodeFromTree(current.nodes, nodeClientId),
    }));
  }, []);

  const removeHint = useCallback(
    (nodeClientId: string, hintClientId: string) => {
      setDraft((current) => ({
        ...current,
        nodes: updateNodeInTree(current.nodes, nodeClientId, (node) => ({
          ...node,
          hints: node.hints.filter((hint) => hint.clientId !== hintClientId),
        })),
      }));
    },
    [],
  );

  const makeComposite = useCallback((nodeClientId: string) => {
    setDraft((current) => ({
      ...current,
      nodes: updateNodeInTree(current.nodes, nodeClientId, (node) => ({
        ...node,
        children:
          node.children.length > 0
            ? node.children
            : [createEmptyNodeDraft("leaf", 1)],
        hints: [],
        timeBudgetMinutes: "",
        prompt: "",
        expectedQrHash: "",
        triviaValidAnswer: "",
        triviaInitialValidationCriterion: "",
      })),
    }));
  }, []);

  const makeLeaf = useCallback((nodeClientId: string) => {
    setDraft((current) => ({
      ...current,
      nodes: updateNodeInTree(current.nodes, nodeClientId, (node) => ({
        ...node,
        children: [],
        defaultTimeBudgetMinutes: "",
        difficulty: normalizeDifficulty(node.difficulty),
        gameType: node.gameType || gameTypeOptions[0],
      })),
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

    const maximumDurationMinutes = Number.parseInt(
      draft.maximumDurationMinutes,
      10,
    );
    const validationIssue = findValidationIssue(
      draft.nodes,
      Number.isFinite(maximumDurationMinutes) ? maximumDurationMinutes : null,
      draft.name.trim() || "Misión",
    );
    if (validationIssue) {
      setErrorMessage(validationIssue);
      setIsSubmitting(false);
      return;
    }

    const serializedNodes = draft.nodes.map(serializeNodeDraft);
    const difficulty = normalizeDifficulty(draft.difficulty);
    const body =
      editorMode === "create"
        ? {
            name: draft.name,
            description: draft.description,
            difficulty,
            maximumDurationMinutes,
            gameType: draft.gameType,
            nodes: serializedNodes,
          }
        : {
            name: draft.name,
            description: draft.description,
            difficulty,
            maximumDurationMinutes,
            gameType: draft.gameType,
            nodes: serializedNodes,
          };

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
      setDraft(toDraft(mission));
      setEditorMode("edit");
      setFeedback(
        editorMode === "create" ? "Misión creada." : "Misión actualizada.",
      );
      await loadMissions(mission.id);
    } catch (error) {
      setErrorMessage(
        error instanceof Error
          ? error.message
          : "No se pudo guardar la Misión.",
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleDeactivate() {
    if (!selectedMissionId) {
      return;
    }

    setIsSubmitting(true);
    setFeedback(null);
    setErrorMessage(null);

    try {
      const response = await fetch(
        `${missionsUrl}/${selectedMissionId}/deactivate`,
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
      setDraft(toDraft(mission));
      setFeedback("Misión desactivada.");
      await loadMissions(mission.id);
    } catch (error) {
      setErrorMessage(
        error instanceof Error
          ? error.message
          : "No se pudo desactivar la Misión.",
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleActivate() {
    if (!selectedMissionId) {
      return;
    }

    setIsSubmitting(true);
    setFeedback(null);
    setErrorMessage(null);

    try {
      const response = await fetch(
        `${missionsUrl}/${selectedMissionId}/activate`,
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
      setDraft(toDraft(mission));
      setFeedback("Misión activada.");
      await loadMissions(mission.id);
    } catch (error) {
      setErrorMessage(
        error instanceof Error
          ? error.message
          : "No se pudo activar la Misión.",
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="workspace-section">
      <div className="workspace-section-header">
        <div className="stack-sm">
          <span className="eyebrow">Diseño de misiones</span>
          <h2>Espacio de trabajo de Misiones</h2>
          <p className="text-muted">
            Superficie de control del administrador para autores de misiones,
            con un lenguaje operativo tranquilo orientado hacia la consola
            cálida existente más bloques de edición anidados más densos.
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
        {feedback ? (
          <div className="success-banner">{feedback}</div>
        ) : null}

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
                    <th>Dificultad</th>
                    <th>Tipo</th>
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
                      <td>
                        <span
                          className={difficultyBadgeClass(mission.difficulty)}
                        >
                          {mission.difficulty}
                        </span>
                      </td>
                      <td>
                        <span
                          className={gameTypeBadgeClass(mission.gameType)}
                        >
                          {mission.gameType}
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
                  {editorMode === "create"
                    ? "Crear"
                    : "Misión seleccionada"}
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
              {/* Tree stats */}
              <div className="info-banner">
                <div className="row-wrap" style={{ gap: "1rem" }}>
                  <span>
                    <strong>Total de nodos:</strong>{" "}
                    {missionTreeStats.totalNodes}
                  </span>
                  <span>
                    <strong>Etapas hoja:</strong>{" "}
                    {missionTreeStats.leafStages}
                  </span>
                  <span>
                    <strong>Bloques:</strong>{" "}
                    {missionTreeStats.compositeBlocks}
                  </span>
                  <span>
                    <strong>Pistas:</strong> {missionTreeStats.hints}
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
                  rows={5}
                  value={draft.description}
                />
              </div>

              <div className="form-row">
                <div className="form-group">
                  <label className="form-label">Dificultad</label>
                  <select
                    className="form-select"
                    onChange={(event) =>
                      updateDraftField("difficulty", event.target.value)
                    }
                    required
                    value={draft.difficulty}
                  >
                    {difficultyOptions.map((difficulty) => (
                      <option key={difficulty} value={difficulty}>
                        {difficulty}
                      </option>
                    ))}
                  </select>
                </div>

                <div className="form-group">
                  <label className="form-label">
                    Duración máxima (minutos)
                  </label>
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

              <div className="form-group">
                <label className="form-label">
                  Tipo de juego del catálogo
                </label>
                <select
                  className="form-select"
                  onChange={(event) =>
                    updateDraftField("gameType", event.target.value)
                  }
                  value={draft.gameType}
                >
                  {gameTypeOptions.map((gameType) => (
                    <option key={gameType} value={gameType}>
                      {gameType}
                    </option>
                  ))}
                </select>
                <span className="form-hint">
                  Las etapas de la misión ahora pueden mezclar tipos de juego.
                  Mantenga esta etiqueta del catálogo alineada con cómo debe
                  aparecer la Misión en los resúmenes del backend actual.
                </span>
              </div>

              {/* ── Node tree ── */}
              <div className="card-section stack">
                <div className="card-header card-header-actions">
                  <div className="stack-sm">
                    <span className="eyebrow">Estructura</span>
                    <h4>Árbol de nodos de la misión</h4>
                  </div>
                  <div className="row-sm">
                    <button
                      className="btn btn-ghost btn-sm"
                      onClick={() => addRootNode("leaf")}
                      type="button"
                    >
                      Agregar etapa raíz
                    </button>
                    <button
                      className="btn btn-ghost btn-sm"
                      onClick={() => addRootNode("composite")}
                      type="button"
                    >
                      Agregar bloque raíz
                    </button>
                  </div>
                </div>

                {draft.nodes.length === 0 ? (
                  <div className="empty-state">
                    <strong>Aún no hay nodos.</strong>
                    <p>
                      Comience con una etapa jugable o un bloque compuesto que
                      anide un flujo más profundo.
                    </p>
                  </div>
                ) : (
                  <div className="node-tree">
                    {draft.nodes.map((node) => (
                      <MissionNodeEditor
                        depth={0}
                        key={node.clientId}
                        node={node}
                        onAddChild={addChildNode}
                        onAddHint={addHint}
                        onMakeComposite={makeComposite}
                        onMakeLeaf={makeLeaf}
                        onRemoveHint={removeHint}
                        onRemoveNode={removeNode}
                        onUpdateHint={updateHintField}
                        onUpdateNode={updateNodeField}
                      />
                    ))}
                  </div>
                )}
              </div>

              <div className="form-actions">
                <button
                  className="btn btn-primary"
                  disabled={isSubmitting}
                  type="submit"
                >
                  {editorMode === "create"
                    ? "Crear Misión"
                    : "Guardar cambios"}
                </button>

                {selectedMission ? (
                  selectedMission.isActive ? (
                    <button
                      className="btn btn-danger"
                      disabled={isSubmitting}
                      onClick={handleDeactivate}
                      type="button"
                    >
                      Desactivar
                    </button>
                  ) : (
                    <button
                      className="btn btn-success"
                      disabled={isSubmitting}
                      onClick={handleActivate}
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
                  <span className="detail-label">Etiqueta del catálogo</span>
                  <span className="detail-value">
                    <span
                      className={gameTypeBadgeClass(
                        selectedMission.gameType,
                      )}
                    >
                      {selectedMission.gameType}
                    </span>
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
