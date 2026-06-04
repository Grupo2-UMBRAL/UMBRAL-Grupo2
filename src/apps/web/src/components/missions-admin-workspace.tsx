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
    difficulty: "",
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
    difficulty: mission.difficulty,
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
      return `${pathPrefix}: node name is required.`;
    }

    const order = Number.parseInt(node.order, 10);
    if (!Number.isFinite(order) || order <= 0) {
      return `${pathPrefix}: node "${trimmedName || "Untitled"}" needs a positive order.`;
    }

    if (orders.has(order)) {
      return `${pathPrefix}: sibling order ${order} is duplicated.`;
    }

    orders.set(order, trimmedName);
  }

  for (const node of nodes) {
    const label = `${pathPrefix} / ${node.name.trim()}`;
    const isLeaf = node.children.length === 0;
    const defaultBudget = parseOptionalInteger(node.defaultTimeBudgetMinutes);
    const ownBudget = parseOptionalInteger(node.timeBudgetMinutes);

    if (defaultBudget !== null && !Number.isFinite(defaultBudget)) {
      return `${label}: default time budget must be a number.`;
    }

    if (ownBudget !== null && !Number.isFinite(ownBudget)) {
      return `${label}: time budget must be a number.`;
    }

    if (isLeaf) {
      const effectiveBudget = ownBudget ?? inheritedBudgetMinutes;

      if (!effectiveBudget || effectiveBudget <= 0) {
        return `${label}: leaf stage needs a local time budget or inherited mission budget.`;
      }

      if (!node.gameType) {
        return `${label}: leaf stage needs a game type.`;
      }

      if (!trimToNull(node.prompt)) {
        return `${label}: leaf stage needs a prompt visible to participants.`;
      }

      if (node.gameType === "Treasure Hunt" && !trimToNull(node.expectedQrHash)) {
        return `${label}: Treasure Hunt stage needs expected QR hash.`;
      }

      if (
        node.gameType === "Trivia" &&
        !trimToNull(node.triviaValidAnswer) &&
        !trimToNull(node.triviaInitialValidationCriterion)
      ) {
        return `${label}: Trivia stage needs valid answer or validation criterion.`;
      }

      for (const hint of node.hints) {
        const hintLabel = `${label} / hint`;
        if (!hint.content.trim()) {
          return `${hintLabel}: content is required.`;
        }

        const hasLatitude = hint.latitude.trim().length > 0;
        const hasLongitude = hint.longitude.trim().length > 0;
        if (hasLatitude !== hasLongitude) {
          return `${hintLabel}: coordinates need both latitude and longitude.`;
        }

        if (
          hasLatitude &&
          !Number.isFinite(parseOptionalDecimal(hint.latitude))
        ) {
          return `${hintLabel}: latitude must be numeric.`;
        }

        if (
          hasLongitude &&
          !Number.isFinite(parseOptionalDecimal(hint.longitude))
        ) {
          return `${hintLabel}: longitude must be numeric.`;
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
    <article
      className="mission-node-card"
      style={{ marginLeft: `${depth * 16}px` }}
    >
      <div className="mission-node-header">
        <div>
          <div className="node-chip-row">
            <span
              className={
                isLeaf ? "node-chip is-leaf" : "node-chip is-composite"
              }
            >
              {isLeaf ? "🎯 Ronda" : "📋 Etapa"}
            </span>
            <span
              className={
                node.isActive
                  ? "status-pill status-ok"
                  : "status-pill status-error"
              }
            >
              {node.isActive ? "activa" : "inactiva"}
            </span>
          </div>
          <h4>
            {node.name.trim() ||
              (isLeaf ? "Ronda sin título" : "Etapa sin título")}
          </h4>
          <p className="muted-copy">
            {isLeaf
              ? "Actividad jugable con validación, pistas y tiempo configurables."
              : "Etapa compuesta. Las rondas hijas heredan el tiempo de esta etapa salvo que definan el propio."}
          </p>
        </div>

        <div className="node-actions">
          {isLeaf ? (
            <button
              className="ghost-button"
              onClick={() => onMakeComposite(node.clientId)}
              type="button"
            >
              Convertir en etapa
            </button>
          ) : (
            <button
              className="ghost-button"
              onClick={() => onMakeLeaf(node.clientId)}
              type="button"
            >
              Convertir en ronda
            </button>
          )}
          <button
            className="ghost-button danger-button"
            onClick={() => onRemoveNode(node.clientId)}
            type="button"
          >
            Eliminar
          </button>
        </div>
      </div>

      <div className="node-grid">
        <label className="field">
          <span>Nombre *</span>
          <input
            className="input"
            maxLength={120}
            onChange={(event) =>
              onUpdateNode(node.clientId, "name", event.target.value)
            }
            placeholder={
              isLeaf
                ? "Ej: Curiosidades de Halloween"
                : "Ej: Recolección de información"
            }
            required
            value={node.name}
          />
        </label>

        <label className="field">
          <span>Orden</span>
          <input
            className="input"
            min={1}
            onChange={(event) =>
              onUpdateNode(node.clientId, "order", event.target.value)
            }
            required
            type="number"
            value={node.order}
          />
        </label>

        <label className="field inline-toggle">
          <span>Activa</span>
          <input
            checked={node.isActive}
            onChange={(event) =>
              onUpdateNode(node.clientId, "isActive", event.target.checked)
            }
            type="checkbox"
          />
        </label>
      </div>

      {isLeaf ? (
        <>
          <div className="node-grid">
            <label className="field">
              <span>Time budget minutes</span>
              <input
                className="input"
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
              <span className="field-hint">
                Leave empty to inherit the mission or parent block budget.
              </span>
            </label>

            <label className="field">
              <span>Game Type</span>
              <select
                className="input"
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
            </label>
          </div>

          <label className="field">
            <span>Prompt</span>
            <textarea
              className="input textarea-input"
              maxLength={1024}
              onChange={(event) => onUpdateNode(node.clientId, "prompt", event.target.value)}
              required
              rows={4}
              value={node.prompt}
            />
            <span className="field-hint">
              Shared participant-facing copy. Trivia uses this as question; Treasure Hunt uses this as instruction.
            </span>
          </label>

          <GameTypeConfig
            gameType={node.gameType}
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

          <section className="node-subsection">
            <div className="mission-list-header">
              <div>
                <p className="eyebrow">Hints</p>
                <h5>Clues and solution drops</h5>
              </div>
              <button
                className="ghost-button"
                onClick={() => onAddHint(node.clientId)}
                type="button"
              >
                Add hint
              </button>
            </div>

            {node.hints.length === 0 ? (
              <div className="tree-empty-state">
                <strong>No hints yet.</strong>
                <p>
                  Treasure Hunt and Trivia stages can carry visible clues or
                  final solution drops.
                </p>
              </div>
            ) : null}

            <div className="hint-editor-list">
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
          </section>
        </>
      ) : (
        <>
          <label className="field">
            <span>Default time budget minutes</span>
            <input
              className="input"
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
            <span className="field-hint">
              Optional override for all descendants that do not define a leaf
              budget.
            </span>
          </label>

          <section className="node-subsection">
            <div className="mission-list-header">
              <div>
                <p className="eyebrow">Children</p>
                <h5>Nested flow</h5>
              </div>
              <div className="node-actions">
                <button
                  className="ghost-button"
                  onClick={() => onAddChild(node.clientId, "leaf")}
                  type="button"
                >
                  Add stage
                </button>
                <button
                  className="ghost-button"
                  onClick={() => onAddChild(node.clientId, "composite")}
                  type="button"
                >
                  Add block
                </button>
              </div>
            </div>

            <div className="mission-node-list">
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
          </section>
        </>
      )}
    </article>
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
          error instanceof Error ? error.message : "Could not load Missions.",
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
            : "Could not load Mission detail.",
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
      draft.name.trim() || "Mission",
    );
    if (validationIssue) {
      setErrorMessage(validationIssue);
      setIsSubmitting(false);
      return;
    }

    const serializedNodes = draft.nodes.map(serializeNodeDraft);
    const body =
      editorMode === "create"
        ? {
            name: draft.name,
            description: draft.description,
            difficulty: draft.difficulty,
            maximumDurationMinutes,
            gameType: draft.gameType,
            nodes: serializedNodes,
          }
        : {
            name: draft.name,
            description: draft.description,
            difficulty: draft.difficulty,
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
        editorMode === "create" ? "Mission created." : "Mission updated.",
      );
      await loadMissions(mission.id);
    } catch (error) {
      setErrorMessage(
        error instanceof Error ? error.message : "Could not save Mission.",
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
      setFeedback("Mission deactivated.");
      await loadMissions(mission.id);
    } catch (error) {
      setErrorMessage(
        error instanceof Error
          ? error.message
          : "Could not deactivate Mission.",
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <section className="panel stack-gap">
      <div className="section-heading">
        <div>
          <p className="eyebrow">Mission design</p>
          <h2>Missions workspace</h2>
        </div>
        <p className="section-copy">
          Reading this as: administrator control surface for mission authors,
          with a calm operational language, leaning toward the existing warm
          shell plus denser nested editing blocks.
        </p>
      </div>

      <div className="mission-summary-grid">
        <article className="signal-card">
          <strong>Total Missions</strong>
          <p className="metric-value">{selectionSummary.total}</p>
        </article>
        <article className="signal-card">
          <strong>Active</strong>
          <p className="metric-value metric-success">
            {selectionSummary.active}
          </p>
        </article>
        <article className="signal-card">
          <strong>Inactive</strong>
          <p className="metric-value metric-danger">
            {selectionSummary.inactive}
          </p>
        </article>
      </div>

      {errorMessage ? <p className="banner-error">{errorMessage}</p> : null}
      {feedback ? <p className="banner-success">{feedback}</p> : null}

      <div className="mission-workspace-grid">
        <section className="mission-list-panel">
          <div className="mission-list-header">
            <div>
              <p className="eyebrow">Catalog</p>
              <h3>Missions</h3>
            </div>
            <button
              className="ghost-button"
              onClick={handleCreateMode}
              type="button"
            >
              New Mission
            </button>
          </div>

          {isLoadingList ? (
            <p className="muted-copy">Loading Missions.</p>
          ) : null}

          {!isLoadingList && missions.length === 0 ? (
            <div className="empty-state">
              <strong>No Missions yet.</strong>
              <p>
                Create the first reusable Mission for the administrator catalog.
              </p>
            </div>
          ) : null}

          <div className="mission-list">
            {missions.map((mission) => (
              <button
                className={
                  mission.id === selectedMissionId
                    ? "mission-list-item is-active"
                    : "mission-list-item"
                }
                key={mission.id}
                onClick={() => {
                  setFeedback(null);
                  setSelectedMissionId(mission.id);
                }}
                type="button"
              >
                <div className="mission-list-item-top">
                  <strong>{mission.name}</strong>
                  <span
                    className={
                      mission.isActive
                        ? "status-pill status-ok"
                        : "status-pill status-error"
                    }
                  >
                    {mission.isActive ? "active" : "inactive"}
                  </span>
                </div>
                <p>{mission.difficulty}</p>
                <dl className="mission-meta-grid">
                  <div>
                    <dt>Catalog type</dt>
                    <dd>{mission.gameType}</dd>
                  </div>
                  <div>
                    <dt>Mission budget</dt>
                    <dd>{mission.maximumDurationMinutes} min</dd>
                  </div>
                </dl>
              </button>
            ))}
          </div>
        </section>

        <section className="mission-editor-panel">
          <div className="mission-list-header">
            <div>
              <p className="eyebrow">
                {editorMode === "create" ? "Create" : "Selected Mission"}
              </p>
              <h3>
                {editorMode === "create"
                  ? "New Mission"
                  : (selectedMission?.name ?? "Mission detail")}
              </h3>
            </div>
            {selectedMission ? (
              <span
                className={
                  selectedMission.isActive
                    ? "status-pill status-ok"
                    : "status-pill status-error"
                }
              >
                {selectedMission.isActive ? "active" : "inactive"}
              </span>
            ) : null}
          </div>

          {isLoadingDetail ? (
            <p className="muted-copy">Loading Mission detail.</p>
          ) : null}

          <form className="auth-form" onSubmit={handleSubmit}>
            <div className="tree-stat-grid">
              <article className="signal-card">
                <strong>Total nodes</strong>
                <p className="metric-value">{missionTreeStats.totalNodes}</p>
              </article>
              <article className="signal-card">
                <strong>Leaf stages</strong>
                <p className="metric-value">{missionTreeStats.leafStages}</p>
              </article>
              <article className="signal-card">
                <strong>Blocks</strong>
                <p className="metric-value">
                  {missionTreeStats.compositeBlocks}
                </p>
              </article>
              <article className="signal-card">
                <strong>Hints</strong>
                <p className="metric-value">{missionTreeStats.hints}</p>
              </article>
            </div>

            <label className="field">
              <span>Name</span>
              <input
                className="input"
                maxLength={120}
                onChange={(event) =>
                  updateDraftField("name", event.target.value)
                }
                required
                value={draft.name}
              />
            </label>

            <label className="field">
              <span>Description</span>
              <textarea
                className="input textarea-input"
                maxLength={1024}
                onChange={(event) =>
                  updateDraftField("description", event.target.value)
                }
                required
                rows={5}
                value={draft.description}
              />
            </label>

            <div className="form-grid-two">
              <label className="field">
                <span>Difficulty</span>
                <input
                  className="input"
                  maxLength={60}
                  onChange={(event) =>
                    updateDraftField("difficulty", event.target.value)
                  }
                  required
                  value={draft.difficulty}
                />
              </label>

              <label className="field">
                <span>Mission budget minutes</span>
                <input
                  className="input"
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
              </label>
            </div>

            <label className="field">
              <span>Catalog Game Type</span>
              <select
                className="input"
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
              <span className="field-hint">
                Mission stages can mix game types now. Keep this catalog label
                aligned with how the Mission should be listed in current backend
                summaries.
              </span>
            </label>

            <section className="node-subsection">
              <div className="mission-list-header">
                <div>
                  <p className="eyebrow">Structure</p>
                  <h4>Mission node tree</h4>
                </div>
                <div className="node-actions">
                  <button
                    className="ghost-button"
                    onClick={() => addRootNode("leaf")}
                    type="button"
                  >
                    Add root stage
                  </button>
                  <button
                    className="ghost-button"
                    onClick={() => addRootNode("composite")}
                    type="button"
                  >
                    Add root block
                  </button>
                </div>
              </div>

              {draft.nodes.length === 0 ? (
                <div className="tree-empty-state">
                  <strong>No nodes yet.</strong>
                  <p>
                    Start with a playable stage or a composite block that nests
                    a deeper flow.
                  </p>
                </div>
              ) : (
                <div className="mission-node-list">
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
            </section>

            <div className="mission-action-row">
              <button
                className="primary-button"
                disabled={isSubmitting}
                type="submit"
              >
                {editorMode === "create" ? "Create Mission" : "Save changes"}
              </button>

              {selectedMission?.isActive ? (
                <button
                  className="ghost-button danger-button"
                  disabled={isSubmitting}
                  onClick={handleDeactivate}
                  type="button"
                >
                  Deactivate
                </button>
              ) : null}
            </div>
          </form>

          {selectedMission ? (
            <dl className="definition-grid mission-detail-grid">
              <div>
                <dt>Mission ID</dt>
                <dd>{selectedMission.id}</dd>
              </div>
              <div>
                <dt>Catalog label</dt>
                <dd>{selectedMission.gameType}</dd>
              </div>
              <div>
                <dt>Current status</dt>
                <dd>{selectedMission.isActive ? "Active" : "Inactive"}</dd>
              </div>
            </dl>
          ) : null}
        </section>
      </div>
    </section>
  );
}
