"use client";

import { type FormEvent, useCallback, useEffect, useMemo, useState } from "react";
import { getClientConfig } from "@/lib/config";

type OperatorUserSummary = {
  id: string;
  username: string;
  displayName: string;
  firstName: string | null;
  lastName: string | null;
  email: string | null;
  isActive: boolean;
  roles: string[];
  lastUpdatedAt: string | null;
};

type OperatorUserDraft = {
  username: string;
  email: string;
};

type OperatorUsersWorkspaceProps = {
  accessToken: string;
};

function createAuthorizedHeaders(accessToken: string) {
  return {
    Authorization: `Bearer ${accessToken}`
  };
}

function createEmptyDraft(): OperatorUserDraft {
  return {
    username: "",
    email: ""
  };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

function readFirstString(record: Record<string, unknown>, keys: readonly string[]) {
  for (const key of keys) {
    const value = record[key];
    if (typeof value === "string" && value.trim()) {
      return value.trim();
    }
  }

  return null;
}

function readFirstBoolean(record: Record<string, unknown>, keys: readonly string[]) {
  for (const key of keys) {
    const value = record[key];
    if (typeof value === "boolean") {
      return value;
    }
  }

  return null;
}

function readStringArray(record: Record<string, unknown>, keys: readonly string[]) {
  for (const key of keys) {
    const value = record[key];
    if (Array.isArray(value)) {
      return value.filter((item): item is string => typeof item === "string" && item.trim().length > 0);
    }
  }

  return [];
}

function normalizeOperatorUser(payload: unknown): OperatorUserSummary | null {
  if (!isRecord(payload)) {
    return null;
  }

  const username = readFirstString(payload, ["username", "userName", "login"]);
  const firstName = readFirstString(payload, ["firstName", "givenName"]);
  const lastName = readFirstString(payload, ["lastName", "familyName"]);
  const fullNameFromParts = [firstName, lastName].filter((value): value is string => Boolean(value)).join(" ").trim();
  const displayName =
    readFirstString(payload, ["displayName", "fullName", "name"]) ??
    (fullNameFromParts.length > 0 ? fullNameFromParts : null);
  const email = readFirstString(payload, ["email"]);
  const status = readFirstString(payload, ["status"]);
  const explicitActiveFlag = readFirstBoolean(payload, ["isActive", "enabled", "active"]);
  const baseRoles = readStringArray(payload, ["roles", "roleNames"]);
  const singleRole = readFirstString(payload, ["role"]);
  const roles = singleRole && !baseRoles.includes(singleRole) ? [...baseRoles, singleRole] : baseRoles;
  const id =
    readFirstString(payload, ["id", "userId", "operatorId", "subject", "sub"]) ??
    username ??
    displayName ??
    email;

  if (!id) {
    return null;
  }

  const isActive =
    explicitActiveFlag ??
    (status ? !["disabled", "inactive"].includes(status.toLowerCase()) : true);

  return {
    id,
    username: username ?? id,
    displayName: displayName ?? username ?? id,
    firstName,
    lastName,
    email,
    isActive,
    roles: roles.length > 0 ? roles : ["Operator"],
    lastUpdatedAt: readFirstString(payload, ["updatedAt", "lastUpdatedAt", "createdAt"])
  };
}

function normalizeOperatorUsers(payload: unknown) {
  if (Array.isArray(payload)) {
    return payload.map(normalizeOperatorUser).filter((item): item is OperatorUserSummary => item !== null);
  }

  if (!isRecord(payload)) {
    return [];
  }

  const collections = ["operators", "items", "data", "results"] as const;

  for (const key of collections) {
    const nestedValue = payload[key];
    if (Array.isArray(nestedValue)) {
      return nestedValue
        .map(normalizeOperatorUser)
        .filter((item): item is OperatorUserSummary => item !== null);
    }
  }

  const singleRecord = normalizeOperatorUser(payload);
  return singleRecord ? [singleRecord] : [];
}

type ErrorTranslationRule = {
  match: (text: string) => boolean;
  message: string;
};

const ERROR_TRANSLATIONS: ErrorTranslationRule[] = [
  {
    match: (text) => text.includes("username only admits") || text.includes("username cannot contain spaces"),
    message: "El nombre de usuario solo admite letras, números, puntos, guiones bajos o guiones."
  },
  {
    match: (text) => text.includes("already exists"),
    message: "El usuario ya existe o el nombre de usuario/correo ya está en uso."
  },
  {
    match: (text) => text.includes("invalid email"),
    message: "El formato del correo electrónico no es válido."
  },
  {
    match: (text) => text.includes("password") && text.includes("at least"),
    message: "La contraseña es demasiado corta o no cumple con los requisitos mínimos."
  },
  {
    match: (text) => text.includes("password") && text.includes("uppercase"),
    message: "La contraseña debe contener al menos una letra mayúscula."
  },
  {
    match: (text) => text.includes("password") && text.includes("non-alphanumeric"),
    message: "La contraseña debe contener al menos un carácter especial."
  },
  {
    match: (text) => text === "bad request" || text === "400 bad request",
    message: "Solicitud incorrecta. Verifique que los datos ingresados sean válidos."
  },
  {
    match: (text) => text === "unauthorized" || text === "401 unauthorized",
    message: "No autorizado. Su sesión podría haber expirado."
  },
  {
    match: (text) => text === "forbidden" || text === "403 forbidden",
    message: "No tiene permisos suficientes para realizar esta acción."
  },
  {
    match: (text) => text === "not found" || text === "404 not found",
    message: "El recurso solicitado no fue encontrado."
  },
  {
    match: (text) => text === "conflict" || text === "409 conflict",
    message: "Hay un conflicto con los datos proporcionados."
  },
  {
    match: (text) => text === "internal server error" || text === "500 internal server error",
    message: "Error interno del servidor. Por favor, intente nuevamente más tarde."
  }
];

function translateError(errorText: string, status: number): string {
  const text = errorText.toLowerCase().trim();

  const matchedRule = ERROR_TRANSLATIONS.find(rule => rule.match(text));
  if (matchedRule) {
    return matchedRule.message;
  }

  // Anything the rules miss is raw backend text, and the backend speaks English.
  // Keep it in the console for debugging rather than in a Spanish-only UI.
  console.error("Error del backend sin traducción:", errorText);
  return `Ocurrió un error inesperado (Código: ${status}).`;
}

async function readFailureDetail(response: Response) {
  const contentType = response.headers.get("content-type") ?? "";
  let errorMessage = `${response.status} ${response.statusText}`;

  try {
    if (contentType.includes("application/json")) {
      const payload = await response.json();

      if (typeof payload === "string" && payload.trim()) {
        errorMessage = payload.trim();
      } else if (isRecord(payload)) {
        errorMessage =
          readFirstString(payload, ["detail", "title", "message", "error"]) ??
          errorMessage;
      }
    } else {
      const text = await response.text();
      if (text.trim()) {
        errorMessage = text.trim();
      }
    }
  } catch {
    // Ignorar errores de parseo y mantener el mensaje por defecto
  }

  return translateError(errorMessage, response.status);
}

function formatTimestamp(value: string | null) {
  if (!value) {
    return "No reportado por la fachada";
  }

  const timestamp = new Date(value);
  if (Number.isNaN(timestamp.getTime())) {
    return value;
  }

  return timestamp.toISOString().replace("T", " ").replace(/\.\d{3}Z$/, " UTC");
}

export function OperatorUsersWorkspace({ accessToken }: OperatorUsersWorkspaceProps) {
  const [operators, setOperators] = useState<OperatorUserSummary[]>([]);
  const [selectedOperatorId, setSelectedOperatorId] = useState<string | null>(null);
  const [draft, setDraft] = useState<OperatorUserDraft>(createEmptyDraft);
  const [isLoading, setIsLoading] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isDeactivating, setIsDeactivating] = useState(false);
  const [isActivating, setIsActivating] = useState(false);
  const [isResendingInvitation, setIsResendingInvitation] = useState(false);
  const [isSendingPasswordResetLink, setIsSendingPasswordResetLink] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<string | null>(null);
  const [showCreateModal, setShowCreateModal] = useState(false);

  useEffect(() => {
    if (!errorMessage && !feedback) {
      return;
    }

    const timeout = window.setTimeout(() => {
      setErrorMessage(null);
      setFeedback(null);
    }, 6000);

    return () => window.clearTimeout(timeout);
  }, [errorMessage, feedback]);

  const operatorsUrl = useMemo(
    () => `${getClientConfig().edgeProxyPublicBaseUrl}/user-management/api/operators`,
    []
  );

  const selectedOperator =
    operators.find((operator) => operator.id === selectedOperatorId) ?? operators[0] ?? null;

  const summary = useMemo(() => {
    const active = operators.filter((operator) => operator.isActive).length;

    return {
      total: operators.length,
      active,
      inactive: operators.length - active
    };
  }, [operators]);

  const fetchOperators = useCallback(async () => {
    const response = await fetch(operatorsUrl, {
      headers: createAuthorizedHeaders(accessToken)
    });

    if (!response.ok) {
      throw new Error(await readFailureDetail(response));
    }

    const payload = (await response.json()) as unknown;
    return normalizeOperatorUsers(payload);
  }, [accessToken, operatorsUrl]);

  const syncOperators = useCallback(
    async (preferredOperatorId?: string) => {
      setErrorMessage(null);

      try {
        const nextOperators = await fetchOperators();
        setOperators(nextOperators);

        const nextSelectedOperator =
          preferredOperatorId && nextOperators.some((operator) => operator.id === preferredOperatorId)
            ? preferredOperatorId
            : nextOperators[0]?.id ?? null;

        setSelectedOperatorId(nextSelectedOperator);
      } catch (error) {
        setErrorMessage(error instanceof Error ? error.message : "No se pudieron cargar los Usuarios Operadores.");
      } finally {
        setIsLoading(false);
      }
    },
    [fetchOperators]
  );

  useEffect(() => {
    let isCancelled = false;

    async function hydrateOperators() {
      try {
        const nextOperators = await fetchOperators();
        if (isCancelled) {
          return;
        }

        setOperators(nextOperators);
        setSelectedOperatorId(nextOperators[0]?.id ?? null);
      } catch (error) {
        if (isCancelled) {
          return;
        }

        setErrorMessage(error instanceof Error ? error.message : "No se pudieron cargar los Usuarios Operadores.");
      } finally {
        if (!isCancelled) {
          setIsLoading(false);
        }
      }
    }

    void hydrateOperators();

    return () => {
      isCancelled = true;
    };
  }, [fetchOperators]);

  async function handleCreateOperator(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSubmitting(true);
    setFeedback(null);
    setErrorMessage(null);

    const username = draft.username.trim();
    const email = draft.email.trim();

    if (!username || !email) {
      setErrorMessage("El nombre de usuario y el correo electrónico son obligatorios.");
      setIsSubmitting(false);
      return;
    }

    try {
      const response = await fetch(operatorsUrl, {
        method: "POST",
        headers: {
          ...createAuthorizedHeaders(accessToken),
          "Content-Type": "application/json"
        },
        body: JSON.stringify({
          username,
          email
        })
      });

      if (!response.ok) {
        throw new Error(await readFailureDetail(response));
      }

      let createdOperatorId: string | undefined;
      const contentType = response.headers.get("content-type") ?? "";

      if (contentType.includes("application/json")) {
        const payload = (await response.json()) as unknown;
        createdOperatorId = normalizeOperatorUser(payload)?.id;
      }

      setIsLoading(true);
      setDraft(createEmptyDraft());
      setShowCreateModal(false);
      setFeedback("Operador creado. Verifica o reenvía la invitación de configuración.");
      await syncOperators(createdOperatorId);
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "No se pudo crear el Usuario Operador.");
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleDeactivateSelectedOperator() {
    if (!selectedOperator || !selectedOperator.isActive) {
      return;
    }

    setIsDeactivating(true);
    setFeedback(null);
    setErrorMessage(null);

    try {
      const response = await fetch(`${operatorsUrl}/${encodeURIComponent(selectedOperator.id)}/deactivate`, {
        method: "POST",
        headers: createAuthorizedHeaders(accessToken)
      });

      if (!response.ok) {
        throw new Error(await readFailureDetail(response));
      }

      setIsLoading(true);
      setFeedback(`Usuario Operador ${selectedOperator.username} desactivado a través de la fachada user-management.`);
      await syncOperators(selectedOperator.id);
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "No se pudo desactivar el Usuario Operador.");
    } finally {
      setIsDeactivating(false);
    }
  }

  async function handleActivateSelectedOperator() {
    if (!selectedOperator || selectedOperator.isActive) {
      return;
    }

    setIsActivating(true);
    setFeedback(null);
    setErrorMessage(null);

    try {
      const response = await fetch(`${operatorsUrl}/${encodeURIComponent(selectedOperator.id)}/activate`, {
        method: "POST",
        headers: createAuthorizedHeaders(accessToken)
      });

      if (!response.ok) {
        throw new Error(await readFailureDetail(response));
      }

      setIsLoading(true);
      setFeedback(`Usuario Operador ${selectedOperator.username} reactivado.`);
      await syncOperators(selectedOperator.id);
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "No se pudo reactivar el Usuario Operador.");
    } finally {
      setIsActivating(false);
    }
  }

  async function handleResendInvitation() {
    if (!selectedOperator) {
      return;
    }

    setIsResendingInvitation(true);
    setFeedback(null);
    setErrorMessage(null);

    try {
      const response = await fetch(
        `${operatorsUrl}/${encodeURIComponent(selectedOperator.id)}/resend-onboarding-invitation`,
        {
          method: "POST",
          headers: createAuthorizedHeaders(accessToken)
        }
      );

      if (!response.ok) {
        throw new Error(await readFailureDetail(response));
      }

      setFeedback(`Se reenvió la invitación de configuración a ${selectedOperator.username}.`);
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "No se pudo reenviar la invitación.");
    } finally {
      setIsResendingInvitation(false);
    }
  }

  async function handleSendPasswordResetLink() {

    if (!selectedOperator) {
      return;
    }

    setIsSendingPasswordResetLink(true);
    setFeedback(null);
    setErrorMessage(null);

    try {
      const response = await fetch(`${operatorsUrl}/${encodeURIComponent(selectedOperator.id)}/send-password-reset-link`, {
        method: "POST",
        headers: createAuthorizedHeaders(accessToken)
      });

      if (!response.ok) {
        throw new Error(await readFailureDetail(response));
      }

      setFeedback(`Se envió un enlace de restablecimiento a ${selectedOperator.username}.`);
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "No se pudo enviar el enlace de restablecimiento.");
    } finally {
      setIsSendingPasswordResetLink(false);
    }
  }

  return (
    <div className="workspace-section">
      <div className="workspace-section-header">
        <h3>Espacio de trabajo de Operadores</h3>
        <button
          className="btn btn-primary"
          onClick={() => {
            setDraft(createEmptyDraft());
            setErrorMessage(null);
            setShowCreateModal(true);
          }}
          type="button"
        >
          Crear Usuario Operador
        </button>
      </div>

      <div className="workspace-section-body">
        <p className="text-muted text-sm">
          Las llamadas del navegador del administrador pasan a través de la fachada de edge-proxy con el token de portador actual.
          No hay tráfico directo de administración de Keycloak desde la consola.
        </p>

        <div className="row row-wrap" style={{ gap: "1rem", marginTop: "0.75rem", marginBottom: "0.75rem" }}>
          <div className="card card-compact">
            <div className="card-section">
              <span className="text-muted text-sm">Total de Operadores</span>
              <p><strong>{summary.total}</strong></p>
            </div>
          </div>
          <div className="card card-compact">
            <div className="card-section">
              <span className="text-muted text-sm">Activos</span>
              <p><strong className="badge badge-green">{summary.active}</strong></p>
            </div>
          </div>
          <div className="card card-compact">
            <div className="card-section">
              <span className="text-muted text-sm">Inactivos</span>
              <p><strong className="badge badge-red">{summary.inactive}</strong></p>
            </div>
          </div>
        </div>

        <div aria-live="polite" className="operator-users-notifications">
          {errorMessage ? <div className="error-banner">{errorMessage}</div> : null}
          {feedback ? <div className="success-banner">{feedback}</div> : null}
        </div>

        {isLoading ? (
          <div className="loading-center">Cargando Usuarios Operadores…</div>
        ) : operators.length === 0 ? (
          <div className="empty-state">
            <p><strong>Aún no se han devuelto Usuarios Operadores.</strong></p>
            <p>La fachada es accesible, pero no ha devuelto una lista para esta vista de administrador.</p>
          </div>
        ) : (
          <div className="split-layout">
            {/* Left: operator table */}
            <div>
              <div className="card card-flush">
                <div className="card-header card-header-actions">
                  <span className="eyebrow">Lista de Usuarios Operadores</span>
                  <button
                    className="btn btn-ghost btn-sm"
                    onClick={() => {
                      setIsLoading(true);
                      void syncOperators(selectedOperator?.id);
                    }}
                    type="button"
                  >
                    Actualizar
                  </button>
                </div>
                <div className="table-wrap">
                  <table>
                    <thead>
                      <tr>
                        <th>Nombre</th>
                        <th>Usuario</th>
                        <th>Email</th>
                        <th>Roles</th>
                        <th>Estado</th>
                      </tr>
                    </thead>
                    <tbody>
                      {operators.map((operator) => (
                        <tr
                          className={
                            operator.id === selectedOperator?.id
                              ? "clickable is-selected"
                              : "clickable"
                          }
                          key={operator.id}
                          onClick={() => {
                            setFeedback(null);
                            setSelectedOperatorId(operator.id);
                          }}
                        >
                          <td>{operator.displayName}</td>
                          <td><code>@{operator.username}</code></td>
                          <td>{operator.email ?? <span className="text-muted">No reportado</span>}</td>
                          <td>{operator.roles.join(", ")}</td>
                          <td>
                            <span className={operator.isActive ? "badge badge-green" : "badge badge-red"}>
                              {operator.isActive ? "Activo" : "Inactivo"}
                            </span>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            </div>

            {/* Right: detail panel */}
            <div>
              {selectedOperator ? (
                <div className="stack">
                  <div className="detail-panel">
                    <h4 style={{ marginBottom: "0.5rem" }}>{selectedOperator.displayName}</h4>
                    <div className="detail-row">
                      <span className="detail-label">ID de usuario</span>
                      <span className="detail-value mono">{selectedOperator.id}</span>
                    </div>
                    <div className="detail-row">
                      <span className="detail-label">Nombre de usuario</span>
                      <span className="detail-value">{selectedOperator.username}</span>
                    </div>
                    <div className="detail-row">
                      <span className="detail-label">Email</span>
                      <span className="detail-value">{selectedOperator.email ?? "No reportado por la fachada"}</span>
                    </div>
                    <div className="detail-row">
                      <span className="detail-label">Nombre</span>
                      <span className="detail-value">{selectedOperator.firstName ?? "No reportado por la fachada"}</span>
                    </div>
                    <div className="detail-row">
                      <span className="detail-label">Apellido</span>
                      <span className="detail-value">{selectedOperator.lastName ?? "No reportado por la fachada"}</span>
                    </div>
                    <div className="detail-row">
                      <span className="detail-label">Roles</span>
                      <span className="detail-value">{selectedOperator.roles.join(", ")}</span>
                    </div>
                    <div className="detail-row">
                      <span className="detail-label">Estado</span>
                      <span className="detail-value">
                        <span className={selectedOperator.isActive ? "badge badge-green" : "badge badge-red"}>
                          {selectedOperator.isActive ? "Activo" : "Inactivo"}
                        </span>
                      </span>
                    </div>
                    <div className="detail-row">
                      <span className="detail-label">Última actualización</span>
                      <span className="detail-value">{formatTimestamp(selectedOperator.lastUpdatedAt)}</span>
                    </div>
                  </div>

                  <div className="row" style={{ gap: "0.5rem" }}>
                    <button
                      className="btn btn-ghost btn-sm"
                      disabled={!selectedOperator || isResendingInvitation}
                      onClick={() => void handleResendInvitation()}
                      type="button"
                    >
                      {isResendingInvitation ? "Reenviando…" : "Reenviar invitación"}
                    </button>
                    <button
                      className="btn btn-ghost btn-sm"
                      disabled={!selectedOperator || isSendingPasswordResetLink}
                      onClick={() => void handleSendPasswordResetLink()}
                      type="button"
                    >
                      {isSendingPasswordResetLink ? "Enviando…" : "Enviar enlace de restablecimiento"}
                    </button>
                    {selectedOperator.isActive ? (
                      <button
                        className="btn btn-danger btn-sm"
                        disabled={isDeactivating}
                        onClick={() => void handleDeactivateSelectedOperator()}
                        type="button"
                      >
                        {isDeactivating ? "Desactivando…" : "Desactivar"}
                      </button>
                    ) : (
                      <button
                        className="btn btn-primary btn-sm"
                        disabled={isActivating}
                        onClick={() => void handleActivateSelectedOperator()}
                        type="button"
                      >
                        {isActivating ? "Reactivando…" : "Reactivar"}
                      </button>
                    )}
                  </div>

                </div>
              ) : (
                <div className="empty-state">
                  <p><strong>Ningún Usuario Operador seleccionado.</strong></p>
                  <p>Seleccione una entrada de la lista después de que la fachada devuelva datos.</p>
                </div>
              )}
            </div>
          </div>
        )}
      </div>

      {/* Create operator modal */}
      {showCreateModal ? (
        <div className="modal-overlay">
          <div className="modal">
            <div className="modal-header">
              <h3>Crear Usuario Operador</h3>
              <button
                className="modal-close"
                onClick={() => setShowCreateModal(false)}
                type="button"
              >
                ×
              </button>
            </div>
            <div className="modal-body">
              <form id="create-operator-form" onSubmit={handleCreateOperator}>
                <div className="stack">
                  <div className="form-row">
                    <div className="form-group">
                      <label className="form-label">Nombre de usuario</label>
                      <input
                        className="form-input"
                        maxLength={40}
                        onChange={(event) =>
                          setDraft((current) => ({
                            ...current,
                            username: event.target.value
                          }))
                        }
                        required
                        value={draft.username}
                      />
                    </div>
                    <div className="form-group">
                      <label className="form-label">Email</label>
                      <input
                        className="form-input"
                        maxLength={120}
                        onChange={(event) =>
                          setDraft((current) => ({
                            ...current,
                            email: event.target.value
                          }))
                        }
                        required
                        type="email"
                        value={draft.email}
                      />
                    </div>
                  </div>
                  <p className="text-muted text-sm">
                    El operador recibirá un correo para definir su contraseña, completar su perfil
                    (nombre y apellido) y verificar su email. El administrador no fija ninguna contraseña.
                  </p>
                </div>
              </form>
            </div>
            <div className="modal-footer">
              <button
                className="btn btn-ghost"
                onClick={() => setShowCreateModal(false)}
                type="button"
              >
                Cancelar
              </button>
              <button
                className="btn btn-primary"
                disabled={isSubmitting}
                form="create-operator-form"
                type="submit"
              >
                {isSubmitting ? "Creando…" : "Crear Usuario Operador"}
              </button>
            </div>
          </div>
        </div>
      ) : null}

    </div>
  );
}
