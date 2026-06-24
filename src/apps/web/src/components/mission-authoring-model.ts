// ---------------------------------------------------------------------------
// Mission authoring model helpers: id generation, draft factories,
// response -> draft mapping, draft -> payload serialization, validation,
// stats, and badge classes. Pure functions only (no React).
// ---------------------------------------------------------------------------

import {
  type ChallengeDraft,
  type ChoiceDraft,
  type ChoicePayload,
  type ChoiceResponse,
  type Difficulty,
  type GameType,
  type HintDraft,
  type HintResponse,
  type MissionDetail,
  type MissionDraft,
  type MissionItemDraft,
  type MissionItemPayload,
  type MissionItemResponse,
  type MissionItemStats,
  type MissionWritePayload,
  type QuestionDraft,
  type QuestionResponse,
  type SearchDraft,
  type SearchResponse,
  type SectionDraft,
  defaultDifficulty,
  defaultGameType,
  difficultyOptions,
  gameTypeOptions,
  missionItemKind,
} from "./mission-authoring-types";

// --- ids -------------------------------------------------------------------

export function createClientId() {
  if (
    typeof crypto !== "undefined" &&
    typeof crypto.randomUUID === "function"
  ) {
    return crypto.randomUUID();
  }

  return `draft-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

// --- normalisation ---------------------------------------------------------

export function normalizeDifficulty(value: string | null | undefined): Difficulty {
  const normalized = value?.trim().toLowerCase();

  return (
    difficultyOptions.find((option) => option.toLowerCase() === normalized) ??
    defaultDifficulty
  );
}

export function normalizeGameType(value: string | null | undefined): GameType {
  const normalized = value?.trim().toLowerCase();

  return (
    gameTypeOptions.find((option) => option.toLowerCase() === normalized) ??
    defaultGameType
  );
}

// --- draft factories -------------------------------------------------------

export function createEmptyChoiceDraft(): ChoiceDraft {
  return {
    clientId: createClientId(),
    text: "",
    isCorrect: false,
  };
}

export function createEmptyQuestionDraft(): QuestionDraft {
  return {
    clientId: createClientId(),
    text: "",
    difficultyOverride: "",
    timeLimitMinutesOverride: "",
    // 2 choices minimum; first is correct by default so the "exactly one
    // correct" rule starts satisfied.
    choices: [
      { ...createEmptyChoiceDraft(), isCorrect: true },
      createEmptyChoiceDraft(),
    ],
  };
}

export function createEmptyHintDraft(): HintDraft {
  return {
    clientId: createClientId(),
    content: "",
    isSolution: false,
    latitude: "",
    longitude: "",
  };
}

export function createEmptySearchDraft(): SearchDraft {
  return {
    clientId: createClientId(),
    clue: "",
    expectedQrHash: "",
    difficultyOverride: "",
    timeLimitMinutesOverride: "",
    hints: [],
  };
}

export function createEmptySectionDraft(): SectionDraft {
  return {
    clientId: createClientId(),
    kind: "Section",
    title: "",
    children: [],
  };
}

export function createEmptyChallengeDraft(): ChallengeDraft {
  return {
    clientId: createClientId(),
    kind: "Challenge",
    title: "",
    gameType: defaultGameType,
    difficulty: defaultDifficulty,
    timeLimitMinutes: "10",
    isActive: true,
    questions: [createEmptyQuestionDraft()],
    searches: [],
  };
}

export function createEmptyMissionDraft(): MissionDraft {
  return {
    name: "",
    description: "",
    maximumDurationMinutes: "60",
    items: [],
  };
}

// --- response -> draft -----------------------------------------------------

function toChoiceDraft(choice: ChoiceResponse): ChoiceDraft {
  return {
    clientId: choice.id,
    id: choice.id,
    text: choice.text,
    isCorrect: choice.isCorrect,
  };
}

function toQuestionDraft(question: QuestionResponse): QuestionDraft {
  const choices = [...question.choices]
    .sort((a, b) => a.order - b.order)
    .map(toChoiceDraft);

  return {
    clientId: question.id,
    id: question.id,
    text: question.text,
    difficultyOverride: question.difficultyOverride ?? "",
    timeLimitMinutesOverride: question.timeLimitMinutesOverride?.toString() ?? "",
    choices:
      choices.length >= 2
        ? choices
        : [...choices, createEmptyChoiceDraft()].slice(0, Math.max(2, choices.length)),
  };
}

function toHintDraft(hint: HintResponse): HintDraft {
  return {
    clientId: hint.id,
    id: hint.id,
    content: hint.content,
    isSolution: hint.isSolution,
    latitude: hint.latitude?.toString() ?? "",
    longitude: hint.longitude?.toString() ?? "",
  };
}

function toSearchDraft(search: SearchResponse): SearchDraft {
  return {
    clientId: search.id,
    id: search.id,
    clue: search.clue,
    expectedQrHash: search.expectedQrHash,
    difficultyOverride: search.difficultyOverride ?? "",
    timeLimitMinutesOverride: search.timeLimitMinutesOverride?.toString() ?? "",
    hints: [...search.hints].sort((a, b) => a.order - b.order).map(toHintDraft),
  };
}

function toItemDraft(item: MissionItemResponse): MissionItemDraft {
  if (item.kind === missionItemKind.challenge) {
    const questions = (item.questions ?? [])
      .slice()
      .sort((a, b) => a.order - b.order)
      .map(toQuestionDraft);
    const searches = (item.searches ?? [])
      .slice()
      .sort((a, b) => a.order - b.order)
      .map(toSearchDraft);

    return {
      clientId: item.id,
      kind: "Challenge",
      id: item.id,
      title: item.title ?? "",
      gameType: normalizeGameType(item.gameType),
      difficulty: normalizeDifficulty(item.difficulty),
      timeLimitMinutes: item.timeLimitMinutes?.toString() ?? "",
      isActive: item.isActive ?? true,
      questions,
      searches,
    };
  }

  // Section (default / fallback)
  const children = (item.children ?? [])
    .slice()
    .sort((a, b) => a.order - b.order)
    .map(toItemDraft);

  return {
    clientId: item.id,
    kind: "Section",
    id: item.id,
    title: item.title ?? "",
    children,
  };
}

export function toMissionDraft(mission: MissionDetail): MissionDraft {
  return {
    name: mission.name,
    description: mission.description,
    maximumDurationMinutes: String(mission.maximumDurationMinutes),
    items: [...mission.items].sort((a, b) => a.order - b.order).map(toItemDraft),
  };
}

// --- parsing ---------------------------------------------------------------

export function trimToNull(value: string): string | null {
  const normalized = value.trim();
  return normalized ? normalized : null;
}

export function parseOptionalInteger(value: string): number | null {
  const normalized = value.trim();
  if (!normalized) {
    return null;
  }

  const parsed = Number.parseInt(normalized, 10);
  return Number.isFinite(parsed) ? parsed : Number.NaN;
}

export function parseOptionalDecimal(value: string): number | null {
  const normalized = value.trim();
  if (!normalized) {
    return null;
  }

  const parsed = Number.parseFloat(normalized);
  return Number.isFinite(parsed) ? parsed : Number.NaN;
}

// --- draft -> payload ------------------------------------------------------
// `order` is assigned as a sequential int per sibling scope at serialize time,
// so the user never manages order numbers by hand — drag position IS the order.

function serializeChoiceDraft(choice: ChoiceDraft, order: number): ChoicePayload {
  return {
    id: choice.id,
    order,
    text: choice.text.trim(),
    isCorrect: choice.isCorrect,
  };
}

function serializeQuestionDraft(question: QuestionDraft, order: number) {
  return {
    id: question.id,
    order,
    text: question.text.trim(),
    difficultyOverride: trimToNull(question.difficultyOverride),
    timeLimitMinutesOverride: parseOptionalInteger(question.timeLimitMinutesOverride),
    choices: question.choices.map((choice, index) =>
      serializeChoiceDraft(choice, index + 1),
    ),
  };
}

function serializeHintDraft(hint: HintDraft, order: number) {
  return {
    id: hint.id,
    order,
    content: hint.content.trim(),
    isSolution: hint.isSolution,
    latitude: parseOptionalDecimal(hint.latitude),
    longitude: parseOptionalDecimal(hint.longitude),
  };
}

function serializeSearchDraft(search: SearchDraft, order: number) {
  return {
    id: search.id,
    order,
    clue: search.clue.trim(),
    expectedQrHash: search.expectedQrHash.trim(),
    difficultyOverride: trimToNull(search.difficultyOverride),
    timeLimitMinutesOverride: parseOptionalInteger(search.timeLimitMinutesOverride),
    hints: search.hints.map((hint, index) => serializeHintDraft(hint, index + 1)),
  };
}

function serializeItemDraft(item: MissionItemDraft, order: number): MissionItemPayload {
  if (item.kind === "Challenge") {
    return {
      kind: missionItemKind.challenge,
      id: item.id,
      order,
      title: item.title.trim(),
      gameType: item.gameType,
      difficulty: item.difficulty,
      timeLimitMinutes: parseOptionalInteger(item.timeLimitMinutes) ?? 0,
      isActive: item.isActive,
      questions:
        item.gameType === "Trivia"
          ? item.questions.map((question, index) =>
              serializeQuestionDraft(question, index + 1),
            )
          : [],
      searches:
        item.gameType === "Treasure Hunt"
          ? item.searches.map((search, index) =>
              serializeSearchDraft(search, index + 1),
            )
          : [],
    };
  }

  return {
    kind: missionItemKind.section,
    id: item.id,
    order,
    title: item.title.trim(),
    children: item.children.map((child, index) =>
      serializeItemDraft(child, index + 1),
    ),
  };
}

export function serializeMissionDraft(draft: MissionDraft): MissionWritePayload {
  return {
    name: draft.name.trim(),
    description: draft.description.trim(),
    maximumDurationMinutes: Number.parseInt(draft.maximumDurationMinutes, 10),
    items: draft.items.map((item, index) => serializeItemDraft(item, index + 1)),
  };
}

// --- validation ------------------------------------------------------------
// Returns the first human-readable issue, or null when the draft is valid.

function validateChallenge(challenge: ChallengeDraft, label: string): string | null {
  if (!challenge.title.trim()) {
    return `${label}: el reto necesita un título.`;
  }

  const timeLimit = parseOptionalInteger(challenge.timeLimitMinutes);
  if (timeLimit === null || !Number.isFinite(timeLimit) || timeLimit <= 0) {
    return `${label}: el reto necesita un límite de tiempo positivo (min).`;
  }

  if (challenge.gameType === "Trivia") {
    if (challenge.questions.length === 0) {
      return `${label}: el reto de Trivia necesita al menos una pregunta.`;
    }

    for (let index = 0; index < challenge.questions.length; index += 1) {
      const question = challenge.questions[index];
      const questionLabel = `${label} / pregunta ${index + 1}`;

      if (!question.text.trim()) {
        return `${questionLabel}: el enunciado es obligatorio.`;
      }

      if (question.choices.length < 2 || question.choices.length > 4) {
        return `${questionLabel}: necesita entre 2 y 4 opciones.`;
      }

      let correctCount = 0;
      for (const choice of question.choices) {
        if (!choice.text.trim()) {
          return `${questionLabel}: todas las opciones necesitan texto.`;
        }
        if (choice.isCorrect) {
          correctCount += 1;
        }
      }

      if (correctCount !== 1) {
        return `${questionLabel}: marca exactamente una opción correcta.`;
      }

      const overrideTime = parseOptionalInteger(question.timeLimitMinutesOverride);
      if (overrideTime !== null && (!Number.isFinite(overrideTime) || overrideTime <= 0)) {
        return `${questionLabel}: el tiempo override debe ser un número positivo.`;
      }
    }

    return null;
  }

  // Treasure Hunt
  if (challenge.searches.length === 0) {
    return `${label}: el reto Treasure Hunt necesita al menos una búsqueda.`;
  }

  for (let index = 0; index < challenge.searches.length; index += 1) {
    const search = challenge.searches[index];
    const searchLabel = `${label} / búsqueda ${index + 1}`;

    if (!search.clue.trim()) {
      return `${searchLabel}: la pista (clue) es obligatoria.`;
    }

    if (!search.expectedQrHash.trim()) {
      return `${searchLabel}: el hash QR esperado es obligatorio.`;
    }

    const overrideTime = parseOptionalInteger(search.timeLimitMinutesOverride);
    if (overrideTime !== null && (!Number.isFinite(overrideTime) || overrideTime <= 0)) {
      return `${searchLabel}: el tiempo override debe ser un número positivo.`;
    }

    for (let hintIndex = 0; hintIndex < search.hints.length; hintIndex += 1) {
      const hint = search.hints[hintIndex];
      const hintLabel = `${searchLabel} / pista ${hintIndex + 1}`;

      if (!hint.content.trim()) {
        return `${hintLabel}: el contenido es obligatorio.`;
      }

      const hasLatitude = hint.latitude.trim().length > 0;
      const hasLongitude = hint.longitude.trim().length > 0;
      if (hasLatitude !== hasLongitude) {
        return `${hintLabel}: las coordenadas necesitan tanto latitud como longitud.`;
      }

      if (hasLatitude && !Number.isFinite(parseOptionalDecimal(hint.latitude))) {
        return `${hintLabel}: la latitud debe ser numérica.`;
      }

      if (hasLongitude && !Number.isFinite(parseOptionalDecimal(hint.longitude))) {
        return `${hintLabel}: la longitud debe ser numérica.`;
      }
    }
  }

  return null;
}

function validateItems(items: MissionItemDraft[], pathPrefix: string): string | null {
  for (let index = 0; index < items.length; index += 1) {
    const item = items[index];

    if (item.kind === "Section") {
      const label = `${pathPrefix} / ${item.title.trim() || `sección ${index + 1}`}`;
      if (!item.title.trim()) {
        return `${label}: la sección necesita un título.`;
      }

      const childIssue = validateItems(item.children, label);
      if (childIssue) {
        return childIssue;
      }

      continue;
    }

    const label = `${pathPrefix} / ${item.title.trim() || `reto ${index + 1}`}`;
    const challengeIssue = validateChallenge(item, label);
    if (challengeIssue) {
      return challengeIssue;
    }
  }

  return null;
}

export function findMissionValidationIssue(draft: MissionDraft): string | null {
  if (!draft.name.trim()) {
    return "La misión necesita un nombre.";
  }

  if (!draft.description.trim()) {
    return "La misión necesita una descripción.";
  }

  const maximumDuration = Number.parseInt(draft.maximumDurationMinutes, 10);
  if (!Number.isFinite(maximumDuration) || maximumDuration <= 0) {
    return "La duración máxima debe ser un número positivo de minutos.";
  }

  return validateItems(draft.items, draft.name.trim() || "Misión");
}

// --- stats -----------------------------------------------------------------

export function summarizeItems(items: MissionItemDraft[]): MissionItemStats {
  return items.reduce<MissionItemStats>(
    (stats, item) => {
      if (item.kind === "Section") {
        const childStats = summarizeItems(item.children);
        return {
          sections: stats.sections + 1 + childStats.sections,
          challenges: stats.challenges + childStats.challenges,
          questions: stats.questions + childStats.questions,
          searches: stats.searches + childStats.searches,
        };
      }

      return {
        sections: stats.sections,
        challenges: stats.challenges + 1,
        questions:
          stats.questions + (item.gameType === "Trivia" ? item.questions.length : 0),
        searches:
          stats.searches +
          (item.gameType === "Treasure Hunt" ? item.searches.length : 0),
      };
    },
    { sections: 0, challenges: 0, questions: 0, searches: 0 },
  );
}

// --- badges ----------------------------------------------------------------

export function difficultyBadgeClass(difficulty: string) {
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

export function gameTypeBadgeClass(gameType: string) {
  switch (gameType) {
    case "Treasure Hunt":
      return "badge badge-blue";
    case "Trivia":
      return "badge badge-accent";
    default:
      return "badge badge-muted";
  }
}
