import { type QuestionDraft, difficultyOptions } from "./mission-authoring-types";
import {
  createEmptyChoiceDraft,
  createEmptyQuestionDraft,
} from "./mission-authoring-model";

type TriviaChallengeEditorProps = {
  questions: QuestionDraft[];
  onChange: (questions: QuestionDraft[]) => void;
};

const MIN_CHOICES = 2;
const MAX_CHOICES = 4;

function updateQuestion(
  questions: QuestionDraft[],
  clientId: string,
  updater: (question: QuestionDraft) => QuestionDraft,
): QuestionDraft[] {
  return questions.map((question) =>
    question.clientId === clientId ? updater(question) : question,
  );
}

export function TriviaChallengeEditor({
  questions,
  onChange,
}: TriviaChallengeEditorProps) {
  function addQuestion() {
    onChange([...questions, createEmptyQuestionDraft()]);
  }

  function removeQuestion(clientId: string) {
    onChange(questions.filter((question) => question.clientId !== clientId));
  }

  function setQuestionField(
    clientId: string,
    field: "text" | "difficultyOverride" | "timeLimitMinutesOverride",
    value: string,
  ) {
    onChange(
      updateQuestion(questions, clientId, (question) => ({
        ...question,
        [field]: value,
      })),
    );
  }

  function addChoice(clientId: string) {
    onChange(
      updateQuestion(questions, clientId, (question) =>
        question.choices.length >= MAX_CHOICES
          ? question
          : { ...question, choices: [...question.choices, createEmptyChoiceDraft()] },
      ),
    );
  }

  function removeChoice(clientId: string, choiceClientId: string) {
    onChange(
      updateQuestion(questions, clientId, (question) => {
        if (question.choices.length <= MIN_CHOICES) {
          return question;
        }

        const removed = question.choices.find(
          (choice) => choice.clientId === choiceClientId,
        );
        const remaining = question.choices.filter(
          (choice) => choice.clientId !== choiceClientId,
        );

        // If we removed the correct choice, promote the first remaining one so
        // the "exactly one correct" invariant always holds.
        if (removed?.isCorrect && remaining.length > 0 && !remaining.some((c) => c.isCorrect)) {
          remaining[0] = { ...remaining[0], isCorrect: true };
        }

        return { ...question, choices: remaining };
      }),
    );
  }

  function setChoiceText(clientId: string, choiceClientId: string, text: string) {
    onChange(
      updateQuestion(questions, clientId, (question) => ({
        ...question,
        choices: question.choices.map((choice) =>
          choice.clientId === choiceClientId ? { ...choice, text } : choice,
        ),
      })),
    );
  }

  function setCorrectChoice(clientId: string, choiceClientId: string) {
    onChange(
      updateQuestion(questions, clientId, (question) => ({
        ...question,
        choices: question.choices.map((choice) => ({
          ...choice,
          isCorrect: choice.clientId === choiceClientId,
        })),
      })),
    );
  }

  return (
    <div className="stack">
      <div className="card-header card-header-actions">
        <div className="stack-sm">
          <span className="eyebrow">Trivia</span>
          <strong>Preguntas y opciones</strong>
        </div>
        <button
          className="btn btn-ghost btn-sm"
          onClick={addQuestion}
          type="button"
        >
          Agregar pregunta
        </button>
      </div>

      {questions.length === 0 ? (
        <div className="empty-state">
          <strong>Sin preguntas todavía.</strong>
          <p>Agrega al menos una pregunta con 2 a 4 opciones.</p>
        </div>
      ) : null}

      <div className="stack">
        {questions.map((question, index) => {
          const correctClientId = question.choices.find(
            (choice) => choice.isCorrect,
          )?.clientId;
          const radioGroup = `correct-${question.clientId}`;

          return (
            <article className="node-item" key={question.clientId}>
              <div className="row-between">
                <span className="text-sm font-semibold text-secondary">
                  Pregunta {index + 1}
                </span>
                <button
                  className="btn btn-danger btn-sm"
                  onClick={() => removeQuestion(question.clientId)}
                  type="button"
                >
                  Quitar
                </button>
              </div>

              <div className="form-group">
                <label className="form-label">Enunciado *</label>
                <textarea
                  className="form-textarea"
                  maxLength={1024}
                  onChange={(event) =>
                    setQuestionField(question.clientId, "text", event.target.value)
                  }
                  placeholder="Ej: ¿Qué significa 'Jack O'Lantern'?"
                  required
                  rows={2}
                  value={question.text}
                />
              </div>

              <div className="card-section stack-sm">
                <div className="card-header card-header-actions">
                  <div className="stack-sm">
                    <span className="eyebrow">Opciones ({question.choices.length}/{MAX_CHOICES})</span>
                    <span className="form-hint">
                      Marca exactamente una opción como correcta.
                    </span>
                  </div>
                  <button
                    className="btn btn-ghost btn-sm"
                    disabled={question.choices.length >= MAX_CHOICES}
                    onClick={() => addChoice(question.clientId)}
                    type="button"
                  >
                    Agregar opción
                  </button>
                </div>

                <div className="stack-sm">
                  {question.choices.map((choice, choiceIndex) => (
                    <div className="row-sm choice-row" key={choice.clientId}>
                      <label className="choice-correct" title="Marcar como correcta">
                        <input
                          checked={choice.clientId === correctClientId}
                          name={radioGroup}
                          onChange={() =>
                            setCorrectChoice(question.clientId, choice.clientId)
                          }
                          type="radio"
                        />
                      </label>
                      <input
                        className="form-input"
                        maxLength={512}
                        onChange={(event) =>
                          setChoiceText(
                            question.clientId,
                            choice.clientId,
                            event.target.value,
                          )
                        }
                        placeholder={`Opción ${choiceIndex + 1}`}
                        value={choice.text}
                      />
                      <button
                        className="btn btn-danger btn-sm"
                        disabled={question.choices.length <= MIN_CHOICES}
                        onClick={() =>
                          removeChoice(question.clientId, choice.clientId)
                        }
                        type="button"
                      >
                        Quitar
                      </button>
                    </div>
                  ))}
                </div>
              </div>

              <div className="form-row">
                <div className="form-group">
                  <label className="form-label">Dificultad (override)</label>
                  <select
                    className="form-select"
                    onChange={(event) =>
                      setQuestionField(
                        question.clientId,
                        "difficultyOverride",
                        event.target.value,
                      )
                    }
                    value={question.difficultyOverride}
                  >
                    <option value="">Heredar del reto</option>
                    {difficultyOptions.map((option) => (
                      <option key={option} value={option}>
                        {option}
                      </option>
                    ))}
                  </select>
                  <span className="form-hint">Vacío = usa la dificultad del reto.</span>
                </div>

                <div className="form-group">
                  <label className="form-label">Tiempo override (min)</label>
                  <input
                    className="form-input"
                    min={1}
                    onChange={(event) =>
                      setQuestionField(
                        question.clientId,
                        "timeLimitMinutesOverride",
                        event.target.value,
                      )
                    }
                    placeholder="Heredar del reto"
                    type="number"
                    value={question.timeLimitMinutesOverride}
                  />
                </div>
              </div>
            </article>
          );
        })}
      </div>
    </div>
  );
}
