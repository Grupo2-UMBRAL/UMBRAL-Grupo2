// ---------------------------------------------------------------------------
// Mission authoring types — mirror of
// MissionManagement.Application/Features/Missions/MissionContracts.cs
//
// The backend replaced the recursive MissionNode tree with a linear
// Section/Challenge/Play model. These types are SEGREGATED from the operator
// (live-session) snapshot types on purpose: the operator consumes a different
// contract (EligibleMission*) that is evolving in parallel.
// ---------------------------------------------------------------------------

export const gameTypeOptions = ["Trivia", "Treasure Hunt"] as const;
export type GameType = (typeof gameTypeOptions)[number];

export const difficultyOptions = ["Easy", "Medium", "Hard"] as const;
export type Difficulty = (typeof difficultyOptions)[number];

export const defaultDifficulty: Difficulty = "Medium";
export const defaultGameType: GameType = gameTypeOptions[0];

export const missionItemKind = {
  section: "Section",
  challenge: "Challenge",
} as const;
export type MissionItemKind =
  (typeof missionItemKind)[keyof typeof missionItemKind];

// ---------------------------------------------------------------------------
// Wire shapes (request + response share the same tree; only the response
// carries server-assigned ids). `?` ids on requests = "create new".
// ---------------------------------------------------------------------------

export type ChoicePayload = {
  id?: string;
  order: number;
  text: string;
  isCorrect: boolean;
};

export type QuestionPayload = {
  id?: string;
  order: number;
  text: string;
  difficultyOverride: string | null;
  timeLimitMinutesOverride: number | null;
  choices: ChoicePayload[];
};

export type HintPayload = {
  id?: string;
  order: number;
  content: string;
  isSolution: boolean;
  latitude: number | null;
  longitude: number | null;
};

export type SearchPayload = {
  id?: string;
  order: number;
  clue: string;
  expectedQrHash: string;
  difficultyOverride: string | null;
  timeLimitMinutesOverride: number | null;
  hints: HintPayload[];
};

export type MissionItemPayload = {
  kind: MissionItemKind;
  id?: string;
  order: number;
  title: string;
  // Section
  children?: MissionItemPayload[];
  // Challenge
  gameType?: string;
  difficulty?: string;
  timeLimitMinutes?: number;
  isActive?: boolean;
  questions?: QuestionPayload[];
  searches?: SearchPayload[];
};

export type MissionWritePayload = {
  name: string;
  description: string;
  maximumDurationMinutes: number;
  items: MissionItemPayload[];
};

// Detail (GET) response. Admin DOES see `isCorrect` on choices.
export type ChoiceResponse = {
  id: string;
  order: number;
  text: string;
  isCorrect: boolean;
};

export type QuestionResponse = {
  id: string;
  order: number;
  text: string;
  difficultyOverride: string | null;
  timeLimitMinutesOverride: number | null;
  choices: ChoiceResponse[];
};

export type HintResponse = {
  id: string;
  order: number;
  content: string;
  isSolution: boolean;
  latitude: number | null;
  longitude: number | null;
};

export type SearchResponse = {
  id: string;
  order: number;
  clue: string;
  expectedQrHash: string;
  difficultyOverride: string | null;
  timeLimitMinutesOverride: number | null;
  hints: HintResponse[];
};

export type MissionItemResponse = {
  kind: string;
  id: string;
  order: number;
  title: string | null;
  children: MissionItemResponse[] | null;
  gameType: string | null;
  difficulty: string | null;
  timeLimitMinutes: number | null;
  isActive: boolean | null;
  questions: QuestionResponse[] | null;
  searches: SearchResponse[] | null;
};

export type MissionSummary = {
  id: string;
  name: string;
  maximumDurationMinutes: number;
  isActive: boolean;
};

export type MissionDetail = {
  id: string;
  name: string;
  description: string;
  maximumDurationMinutes: number;
  isActive: boolean;
  items: MissionItemResponse[];
};

// ---------------------------------------------------------------------------
// Editable drafts. Number inputs hold strings (like the old editor) so the
// user can clear / partially type a value without it snapping to 0.
// `clientId` is a stable React key + dnd-kit sortable id; it is NEVER sent.
// ---------------------------------------------------------------------------

export type ChoiceDraft = {
  clientId: string;
  id?: string;
  text: string;
  isCorrect: boolean;
};

export type QuestionDraft = {
  clientId: string;
  id?: string;
  text: string;
  difficultyOverride: string;
  timeLimitMinutesOverride: string;
  choices: ChoiceDraft[];
};

export type HintDraft = {
  clientId: string;
  id?: string;
  content: string;
  isSolution: boolean;
  latitude: string;
  longitude: string;
};

export type SearchDraft = {
  clientId: string;
  id?: string;
  clue: string;
  expectedQrHash: string;
  difficultyOverride: string;
  timeLimitMinutesOverride: string;
  hints: HintDraft[];
};

export type SectionDraft = {
  clientId: string;
  kind: "Section";
  id?: string;
  title: string;
  children: MissionItemDraft[];
};

export type ChallengeDraft = {
  clientId: string;
  kind: "Challenge";
  id?: string;
  title: string;
  gameType: GameType;
  difficulty: Difficulty;
  timeLimitMinutes: string;
  isActive: boolean;
  questions: QuestionDraft[];
  searches: SearchDraft[];
};

export type MissionItemDraft = SectionDraft | ChallengeDraft;

export type MissionDraft = {
  name: string;
  description: string;
  maximumDurationMinutes: string;
  items: MissionItemDraft[];
};

export type MissionItemStats = {
  sections: number;
  challenges: number;
  questions: number;
  searches: number;
};
