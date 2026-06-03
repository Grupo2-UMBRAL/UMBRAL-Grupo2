"use client";

type GameTypeConfigProps = {
  gameType: string;
  expectedQrHash: string;
  triviaValidAnswer: string;
  triviaInitialValidationCriterion: string;
  onUpdate: (field: string, value: string) => void;
};

export function GameTypeConfig({
  gameType,
  expectedQrHash,
  triviaValidAnswer,
  triviaInitialValidationCriterion,
  onUpdate
}: GameTypeConfigProps) {
  if (gameType === "Treasure Hunt") {
    return (
      <div className="game-config-section">
        <h5 className="config-heading">🗺️ Configuración Treasure Hunt</h5>
        <label className="field">
          <span>QR Hash esperado *</span>
          <input
            className="input"
            maxLength={256}
            onChange={(event) => onUpdate("expectedQrHash", event.target.value)}
            placeholder="Hash del código QR que deben encontrar"
            required
            value={expectedQrHash}
          />
          <span className="field-hint">
            Identificador único del QR físico que los participantes deben escanear.
          </span>
        </label>
        {/* TODO: Add QR photo upload + location picker for treasure hunt setup */}
      </div>
    );
  }

  if (gameType === "Trivia") {
    return (
      <div className="game-config-section">
        <h5 className="config-heading">🎯 Configuración Trivia</h5>
        <p className="field-hint config-hint">
          Define al menos UNA de estas opciones para validar respuestas:
        </p>

        <label className="field">
          <span>Respuesta válida</span>
          <input
            className="input"
            maxLength={512}
            onChange={(event) => onUpdate("triviaValidAnswer", event.target.value)}
            placeholder="Ej: Jack O'Lantern"
            value={triviaValidAnswer}
          />
          <span className="field-hint">
            Respuesta exacta esperada. Validación automática contra esta cadena.
          </span>
        </label>

        <div className="field-divider">
          <span>O</span>
        </div>

        <label className="field">
          <span>Criterio de validación</span>
          <input
            className="input"
            maxLength={512}
            onChange={(event) => onUpdate("triviaInitialValidationCriterion", event.target.value)}
            placeholder="Ej: debe mencionar 'calabaza' o 'linterna'"
            value={triviaInitialValidationCriterion}
          />
          <span className="field-hint">
            Regla flexible para validación inicial. Operador puede corregir después.
          </span>
        </label>

        {!triviaValidAnswer.trim() && !triviaInitialValidationCriterion.trim() && (
          <p className="validation-warning">
            ⚠️ Debes definir al menos una respuesta válida o un criterio de validación para esta ronda de trivia.
          </p>
        )}
      </div>
    );
  }

  return null;
}
