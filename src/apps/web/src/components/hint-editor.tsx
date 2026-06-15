import { useState } from "react";

type HintEditorProps = {
  hint: {
    clientId: string;
    content: string;
    isSolution: boolean;
    latitude: string;
    longitude: string;
  };
  index: number;
  onUpdate: (field: "content" | "isSolution" | "latitude" | "longitude", value: string | boolean) => void;
  onRemove: () => void;
};

export function HintEditor({ hint, index, onUpdate, onRemove }: HintEditorProps) {
  const hasLocation = hint.latitude.trim() !== "" && hint.longitude.trim() !== "";
  const [showLocation, setShowLocation] = useState(hasLocation);

  function handleToggleLocation() {
    if (showLocation) {
      onUpdate("latitude", "");
      onUpdate("longitude", "");
    }
    setShowLocation(!showLocation);
  }

  return (
    <article className="node-item">
      <div className="row-between">
        <span className="text-sm font-semibold text-secondary">Pista {index + 1}</span>
        <button className="btn btn-danger btn-sm" onClick={onRemove} type="button">
          Quitar
        </button>
      </div>

      <div className="form-group">
        <label className="form-label">Contenido</label>
        <textarea
          className="form-textarea"
          maxLength={1024}
          onChange={(e) => onUpdate("content", e.target.value)}
          placeholder="Descripción de la pista que verán los participantes..."
          required
          rows={3}
          value={hint.content}
        />
      </div>

      <label className="checkbox-label">
        <input
          checked={hint.isSolution}
          onChange={(e) => onUpdate("isSolution", e.target.checked)}
          type="checkbox"
        />
        Revelar como solución final
      </label>

      <div className="stack-sm">
        {!showLocation ? (
          <button className="btn btn-ghost btn-sm" onClick={handleToggleLocation} type="button">
            + Agregar ubicación
          </button>
        ) : (
          <div className="stack-sm">
            <div className="row-between">
              <span className="form-label">Ubicación de la pista</span>
              <button className="btn btn-danger btn-sm" onClick={handleToggleLocation} type="button">
                Quitar ubicación
              </button>
            </div>
            <div className="form-row">
              <div className="form-group">
                <label className="form-label">Latitud</label>
                <input
                  className="form-input"
                  onChange={(e) => onUpdate("latitude", e.target.value)}
                  placeholder="Ej: 40.7128"
                  step="any"
                  type="number"
                  value={hint.latitude}
                />
              </div>
              <div className="form-group">
                <label className="form-label">Longitud</label>
                <input
                  className="form-input"
                  onChange={(e) => onUpdate("longitude", e.target.value)}
                  placeholder="Ej: -74.0060"
                  step="any"
                  type="number"
                  value={hint.longitude}
                />
              </div>
            </div>
            <p className="form-hint">Coordenadas donde se encuentra esta pista en el mapa.</p>
          </div>
        )}
      </div>
    </article>
  );
}
