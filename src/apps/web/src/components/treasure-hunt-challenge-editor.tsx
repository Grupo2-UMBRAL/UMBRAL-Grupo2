import {
  type HintDraft,
  type SearchDraft,
  difficultyOptions,
} from "./mission-authoring-types";
import {
  createEmptyHintDraft,
  createEmptySearchDraft,
} from "./mission-authoring-model";
import { HintEditor } from "./hint-editor";
import { printQrCode } from "../lib/qr-code-svg";
import { QrCode } from "./qr-code";

type TreasureHuntChallengeEditorProps = {
  searches: SearchDraft[];
  onChange: (searches: SearchDraft[]) => void;
};

function updateSearch(
  searches: SearchDraft[],
  clientId: string,
  updater: (search: SearchDraft) => SearchDraft,
): SearchDraft[] {
  return searches.map((search) =>
    search.clientId === clientId ? updater(search) : search,
  );
}

export function TreasureHuntChallengeEditor({
  searches,
  onChange,
}: TreasureHuntChallengeEditorProps) {
  function addSearch() {
    onChange([...searches, createEmptySearchDraft()]);
  }

  function removeSearch(clientId: string) {
    onChange(searches.filter((search) => search.clientId !== clientId));
  }

  function setSearchField(
    clientId: string,
    field: "clue" | "expectedQrHash" | "difficultyOverride" | "timeLimitMinutesOverride",
    value: string,
  ) {
    onChange(
      updateSearch(searches, clientId, (search) => ({
        ...search,
        [field]: value,
      })),
    );
  }

  function addHint(clientId: string) {
    onChange(
      updateSearch(searches, clientId, (search) => ({
        ...search,
        hints: [...search.hints, createEmptyHintDraft()],
      })),
    );
  }

  function removeHint(clientId: string, hintClientId: string) {
    onChange(
      updateSearch(searches, clientId, (search) => ({
        ...search,
        hints: search.hints.filter((hint) => hint.clientId !== hintClientId),
      })),
    );
  }

  function updateHint(
    clientId: string,
    hintClientId: string,
    field: "content" | "isSolution" | "latitude" | "longitude",
    value: string | boolean,
  ) {
    onChange(
      updateSearch(searches, clientId, (search) => ({
        ...search,
        hints: search.hints.map((hint) =>
          hint.clientId === hintClientId
            ? ({ ...hint, [field]: value } as HintDraft)
            : hint,
        ),
      })),
    );
  }

  function updateHintLocation(
    clientId: string,
    hintClientId: string,
    latitude: string,
    longitude: string,
  ) {
    onChange(
      updateSearch(searches, clientId, (search) => ({
        ...search,
        hints: search.hints.map((hint) =>
          hint.clientId === hintClientId
            ? { ...hint, latitude, longitude }
            : hint,
        ),
      })),
    );
  }

  return (
    <div className="stack">
      <div className="card-header card-header-actions">
        <div className="stack-sm">
          <span className="eyebrow">Treasure Hunt</span>
          <strong>Búsquedas y pistas</strong>
        </div>
        <button className="btn btn-ghost btn-sm" onClick={addSearch} type="button">
          Agregar búsqueda
        </button>
      </div>

      {searches.length === 0 ? (
        <div className="empty-state">
          <strong>Sin búsquedas todavía.</strong>
          <p>Cada búsqueda lleva una pista, el hash QR esperado y pistas opcionales.</p>
        </div>
      ) : null}

      <div className="stack">
        {searches.map((search, index) => (
          <article className="node-item" key={search.clientId}>
            <div className="row-between">
              <span className="text-sm font-semibold text-secondary">
                Búsqueda {index + 1}
              </span>
              <button
                className="btn btn-danger btn-sm"
                onClick={() => removeSearch(search.clientId)}
                type="button"
              >
                Quitar
              </button>
            </div>

            <div className="form-group">
              <label className="form-label">Pista visible (clue) *</label>
              <textarea
                className="form-textarea"
                maxLength={1024}
                onChange={(event) =>
                  setSearchField(search.clientId, "clue", event.target.value)
                }
                placeholder="Ej: Busca el código QR escondido en el salón principal..."
                required
                rows={2}
                value={search.clue}
              />
            </div>

            <div className="form-group">
              <label className="form-label">QR Hash esperado *</label>
              <input
                className="form-input"
                maxLength={256}
                onChange={(event) =>
                  setSearchField(
                    search.clientId,
                    "expectedQrHash",
                    event.target.value,
                  )
                }
                placeholder="Hash del código QR que deben escanear"
                required
                value={search.expectedQrHash}
              />
            </div>

            {search.expectedQrHash.trim() ? (
              <div className="qr-code-block">
                <QrCode value={search.expectedQrHash.trim()} size={140} />
                <div className="stack-sm">
                  <span className="text-xs text-muted">
                    Vista previa del QR que escaneará el jugador. Imprímelo y colócalo en el punto físico.
                  </span>
                  <button
                    className="btn btn-ghost btn-sm"
                    onClick={() =>
                      printQrCode(search.expectedQrHash.trim(), {
                        title: `Búsqueda ${index + 1}`,
                        clue: search.clue.trim(),
                      })
                    }
                    type="button"
                  >
                    Imprimir QR
                  </button>
                </div>
              </div>
            ) : null}

            <div className="form-row">
              <div className="form-group">
                <label className="form-label">Dificultad (override)</label>
                <select
                  className="form-select"
                  onChange={(event) =>
                    setSearchField(
                      search.clientId,
                      "difficultyOverride",
                      event.target.value,
                    )
                  }
                  value={search.difficultyOverride}
                >
                  <option value="">Heredar del reto</option>
                  {difficultyOptions.map((option) => (
                    <option key={option} value={option}>
                      {option}
                    </option>
                  ))}
                </select>
              </div>

              <div className="form-group">
                <label className="form-label">Tiempo override (min)</label>
                <input
                  className="form-input"
                  min={1}
                  onChange={(event) =>
                    setSearchField(
                      search.clientId,
                      "timeLimitMinutesOverride",
                      event.target.value,
                    )
                  }
                  placeholder="Heredar del reto"
                  type="number"
                  value={search.timeLimitMinutesOverride}
                />
              </div>
            </div>

            <div className="card-section stack-sm">
              <div className="card-header card-header-actions">
                <div className="stack-sm">
                  <span className="eyebrow">Pistas</span>
                  <strong>Claves y solución</strong>
                </div>
                <button
                  className="btn btn-ghost btn-sm"
                  onClick={() => addHint(search.clientId)}
                  type="button"
                >
                  Agregar pista
                </button>
              </div>

              {search.hints.length === 0 ? (
                <div className="empty-state">
                  <strong>Aún no hay pistas.</strong>
                  <p>Las pistas pueden ser claves intermedias o la solución final.</p>
                </div>
              ) : null}

              <div className="stack-sm">
                {search.hints.map((hint, hintIndex) => (
                  <HintEditor
                    hint={hint}
                    index={hintIndex}
                    key={hint.clientId}
                    onRemove={() => removeHint(search.clientId, hint.clientId)}
                    onUpdate={(field, value) =>
                      updateHint(search.clientId, hint.clientId, field, value)
                    }
                    onLocationUpdate={(latitude, longitude) =>
                      updateHintLocation(search.clientId, hint.clientId, latitude, longitude)
                    }
                  />
                ))}
              </div>
            </div>
          </article>
        ))}
      </div>
    </div>
  );
}
