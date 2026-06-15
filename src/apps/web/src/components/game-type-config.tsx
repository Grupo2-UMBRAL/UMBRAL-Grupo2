type GameTypeConfigProps = {
  gameType: string;
  prompt: string;
  expectedQrHash: string;
  triviaValidAnswer: string;
  triviaInitialValidationCriterion: string;
  onUpdate: (field: string, value: string) => void;
};

export function GameTypeConfig({
  gameType,
  prompt,
  expectedQrHash,
  triviaValidAnswer,
  triviaInitialValidationCriterion,
  onUpdate,
}: GameTypeConfigProps) {
  if (gameType === "Treasure Hunt") {
    return (
      <div className="stack">
        <h4>Configuración Treasure Hunt</h4>

        <div className="form-group">
          <label className="form-label">Instrucción para participantes *</label>
          <textarea
            className="form-textarea"
            maxLength={1024}
            onChange={(e) => onUpdate("prompt", e.target.value)}
            placeholder="Ej: Encuentra el código QR escondido en el salón..."
            required
            rows={3}
            value={prompt}
          />
          <span className="form-hint">Texto que verán los participantes explicando qué deben buscar.</span>
        </div>

        <div className="form-group">
          <label className="form-label">QR Hash esperado *</label>
          <input
            className="form-input"
            maxLength={256}
            onChange={(e) => onUpdate("expectedQrHash", e.target.value)}
            placeholder="Hash del código QR que deben encontrar"
            required
            value={expectedQrHash}
          />
          <span className="form-hint">Identificador único del QR físico que los participantes deben escanear.</span>
        </div>
      </div>
    );
  }

  if (gameType === "Trivia") {
    return (
      <div className="stack">
        <h4>Configuración Trivia</h4>

        <div className="form-group">
          <label className="form-label">Pregunta para participantes *</label>
          <textarea
            className="form-textarea"
            maxLength={1024}
            onChange={(e) => onUpdate("prompt", e.target.value)}
            placeholder="Ej: ¿Qué significa 'Jack O'Lantern'?"
            required
            rows={3}
            value={prompt}
          />
          <span className="form-hint">Pregunta que verán los participantes y deberán responder.</span>
        </div>

        <p className="form-hint" style={{ fontWeight: 500, color: "var(--text-secondary)" }}>
          Define al menos UNA de estas opciones para validar respuestas:
        </p>

        <div className="form-group">
          <label className="form-label">Respuesta válida</label>
          <input
            className="form-input"
            maxLength={512}
            onChange={(e) => onUpdate("triviaValidAnswer", e.target.value)}
            placeholder="Ej: Jack O'Lantern"
            value={triviaValidAnswer}
          />
          <span className="form-hint">Respuesta exacta esperada. Validación automática contra esta cadena.</span>
        </div>

        <div style={{ textAlign: "center", color: "var(--text-muted)", fontSize: "var(--text-sm)" }}>O</div>

        <div className="form-group">
          <label className="form-label">Criterio de validación</label>
          <input
            className="form-input"
            maxLength={512}
            onChange={(e) => onUpdate("triviaInitialValidationCriterion", e.target.value)}
            placeholder="Ej: debe mencionar 'calabaza' o 'linterna'"
            value={triviaInitialValidationCriterion}
          />
          <span className="form-hint">Regla flexible para validación inicial. Operador puede corregir después.</span>
        </div>

        {!triviaValidAnswer.trim() && !triviaInitialValidationCriterion.trim() && (
          <div className="error-banner">
            Debes definir al menos una respuesta válida o un criterio de validación para esta etapa de trivia.
          </div>
        )}
      </div>
    );
  }

  return null;
}
