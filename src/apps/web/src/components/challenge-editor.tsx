import {
  type ChallengeDraft,
  type Difficulty,
  type GameType,
  type QuestionDraft,
  type SearchDraft,
  difficultyOptions,
  gameTypeOptions,
} from "./mission-authoring-types";
import {
  createEmptyQuestionDraft,
  createEmptySearchDraft,
} from "./mission-authoring-model";
import { TriviaChallengeEditor } from "./trivia-challenge-editor";
import { TreasureHuntChallengeEditor } from "./treasure-hunt-challenge-editor";

type ChallengeEditorProps = {
  challenge: ChallengeDraft;
  onChange: (challenge: ChallengeDraft) => void;
};

export function ChallengeEditor({ challenge, onChange }: ChallengeEditorProps) {
  function setGameType(nextGameType: GameType) {
    onChange({
      ...challenge,
      gameType: nextGameType,
      // Seed the relevant sub-list when switching into an empty type, so the
      // user always sees one editable row. The other branch's data is kept in
      // the draft but ignored on serialize.
      questions:
        nextGameType === "Trivia" && challenge.questions.length === 0
          ? [createEmptyQuestionDraft()]
          : challenge.questions,
      searches:
        nextGameType === "Treasure Hunt" && challenge.searches.length === 0
          ? [createEmptySearchDraft()]
          : challenge.searches,
    });
  }

  function setQuestions(questions: QuestionDraft[]) {
    onChange({ ...challenge, questions });
  }

  function setSearches(searches: SearchDraft[]) {
    onChange({ ...challenge, searches });
  }

  return (
    <div className="stack">
      <div className="form-row">
        <div className="form-group">
          <label className="form-label">Título del reto *</label>
          <input
            className="form-input"
            maxLength={120}
            onChange={(event) =>
              onChange({ ...challenge, title: event.target.value })
            }
            placeholder="Ej: Curiosidades de Halloween"
            required
            value={challenge.title}
          />
        </div>

        <div className="form-group">
          <label className="checkbox-label">
            <input
              checked={challenge.isActive}
              onChange={(event) =>
                onChange({ ...challenge, isActive: event.target.checked })
              }
              type="checkbox"
            />
            Activo
          </label>
        </div>
      </div>

      <div className="form-row">
        <div className="form-group">
          <label className="form-label">Tipo de juego</label>
          <select
            className="form-select"
            onChange={(event) => setGameType(event.target.value as GameType)}
            value={challenge.gameType}
          >
            {gameTypeOptions.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </select>
        </div>

        <div className="form-group">
          <label className="form-label">Dificultad</label>
          <select
            className="form-select"
            onChange={(event) =>
              onChange({
                ...challenge,
                difficulty: event.target.value as Difficulty,
              })
            }
            value={challenge.difficulty}
          >
            {difficultyOptions.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </select>
        </div>

        <div className="form-group">
          <label className="form-label">Límite de tiempo (min) *</label>
          <input
            className="form-input"
            min={1}
            onChange={(event) =>
              onChange({ ...challenge, timeLimitMinutes: event.target.value })
            }
            required
            type="number"
            value={challenge.timeLimitMinutes}
          />
        </div>
      </div>

      {challenge.gameType === "Trivia" ? (
        <TriviaChallengeEditor
          onChange={setQuestions}
          questions={challenge.questions}
        />
      ) : (
        <TreasureHuntChallengeEditor
          onChange={setSearches}
          searches={challenge.searches}
        />
      )}
    </div>
  );
}
