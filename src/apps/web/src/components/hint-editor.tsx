"use client";

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
      // Clear location when hiding
      onUpdate("latitude", "");
      onUpdate("longitude", "");
    }
    setShowLocation(!showLocation);
  }

  return (
    <article className="hint-card">
      <div className="mission-list-header">
        <div>
          <p className="eyebrow">Pista {index + 1}</p>
        </div>
        <button className="ghost-button danger-button" onClick={onRemove} type="button">
          Quitar
        </button>
      </div>

      <label className="field">
        <span>Contenido</span>
        <textarea
          className="input textarea-input"
          maxLength={1024}
          onChange={(event) => onUpdate("content", event.target.value)}
          placeholder="Descripción de la pista que verán los participantes..."
          required
          rows={3}
          value={hint.content}
        />
      </label>

      <label className="field inline-toggle">
        <span>Revelar como solución final</span>
        <input
          checked={hint.isSolution}
          onChange={(event) => onUpdate("isSolution", event.target.checked)}
          type="checkbox"
        />
      </label>

      <div className="hint-location-section">
        {!showLocation ? (
          <button className="text-link" onClick={handleToggleLocation} type="button">
            + Agregar ubicación (opcional)
          </button>
        ) : (
          <div className="location-fields">
            <div className="location-header">
              <span className="field-label">📍 Ubicación de la pista</span>
              <button className="text-link danger-text" onClick={handleToggleLocation} type="button">
                Quitar ubicación
              </button>
            </div>
            <div className="node-grid">
              <label className="field">
                <span>Latitud</span>
                <input
                  className="input"
                  onChange={(event) => onUpdate("latitude", event.target.value)}
                  placeholder="Ej: 40.7128"
                  step="any"
                  type="number"
                  value={hint.latitude}
                />
              </label>

              <label className="field">
                <span>Longitud</span>
                <input
                  className="input"
                  onChange={(event) => onUpdate("longitude", event.target.value)}
                  placeholder="Ej: -74.0060"
                  step="any"
                  type="number"
                  value={hint.longitude}
                />
              </label>
            </div>
            <p className="field-hint">Coordenadas donde se encuentra esta pista en el mapa.</p>
          </div>
        )}
      </div>
    </article>
  );
}
