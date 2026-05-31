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
  firstName: string;
  lastName: string;
  email: string;
  password: string;
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
    firstName: "",
    lastName: "",
    email: "",
    password: ""
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

async function readFailureDetail(response: Response) {
  const contentType = response.headers.get("content-type") ?? "";

  try {
    if (contentType.includes("application/json")) {
      const payload = await response.json();

      if (typeof payload === "string" && payload.trim()) {
        return payload;
      }

      if (isRecord(payload)) {
        const detail =
          readFirstString(payload, ["detail", "title", "message", "error"]) ??
          `${response.status} ${response.statusText}`;
        return detail;
      }
    }

    const text = await response.text();
    if (text.trim()) {
      return text.trim();
    }
  } catch {
    return `${response.status} ${response.statusText}`;
  }

  return `${response.status} ${response.statusText}`;
}

function formatTimestamp(value: string | null) {
  if (!value) {
    return "Not reported by facade";
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
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<string | null>(null);

  const operatorsUrl = useMemo(
    () => `${getClientConfig().edgeProxyPublicBaseUrl}/identity-access/api/identity-access/operators`,
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
        setErrorMessage(error instanceof Error ? error.message : "Could not load Operator Users.");
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

        setErrorMessage(error instanceof Error ? error.message : "Could not load Operator Users.");
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
    const firstName = draft.firstName.trim();
    const lastName = draft.lastName.trim();
    const email = draft.email.trim();
    const password = draft.password.trim();

    if (!username || !firstName || !lastName || !email || !password) {
      setErrorMessage("Username, first name, last name, email, and password are required.");
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
          firstName,
          lastName,
          email,
          password
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
      setFeedback("Operator User created through the identity-access facade.");
      await syncOperators(createdOperatorId);
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "Could not create Operator User.");
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
      setFeedback(`Operator User ${selectedOperator.username} deactivated through the identity-access facade.`);
      await syncOperators(selectedOperator.id);
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "Could not deactivate Operator User.");
    } finally {
      setIsDeactivating(false);
    }
  }

  return (
    <section className="panel stack-gap">
      <div className="section-heading">
        <div>
          <p className="eyebrow">Identity and access</p>
          <h2>Operator Users workspace</h2>
        </div>
        <p className="section-copy">
          Administrator browser calls go through the edge-proxy facade with the current bearer token.
          No direct Keycloak admin traffic from the shell.
        </p>
      </div>

      <div className="mission-summary-grid">
        <article className="signal-card">
          <strong>Total Operator Users</strong>
          <p className="metric-value">{summary.total}</p>
        </article>
        <article className="signal-card">
          <strong>Active</strong>
          <p className="metric-value metric-success">{summary.active}</p>
        </article>
        <article className="signal-card">
          <strong>Inactive</strong>
          <p className="metric-value metric-danger">{summary.inactive}</p>
        </article>
      </div>

      {errorMessage ? <p className="banner-error">{errorMessage}</p> : null}
      {feedback ? <p className="banner-success">{feedback}</p> : null}

      <div className="operator-workspace-grid">
        <section className="operator-list-panel">
          <div className="mission-list-header">
            <div>
              <p className="eyebrow">Roster</p>
              <h3>Operator Users</h3>
            </div>
            <button
              className="ghost-button"
              onClick={() => {
                setIsLoading(true);
                void syncOperators(selectedOperator?.id);
              }}
              type="button"
            >
              Refresh
            </button>
          </div>

          {isLoading ? <p className="muted-copy">Loading Operator Users.</p> : null}

          {!isLoading && operators.length === 0 ? (
            <div className="empty-state">
              <strong>No Operator Users returned yet.</strong>
              <p>The facade is reachable, but it has not returned a roster for this administrator view.</p>
            </div>
          ) : null}

          <div className="operator-list">
            {operators.map((operator) => (
              <button
                className={
                  operator.id === selectedOperator?.id ? "operator-list-item is-active" : "operator-list-item"
                }
                key={operator.id}
                onClick={() => {
                  setFeedback(null);
                  setSelectedOperatorId(operator.id);
                }}
                type="button"
              >
                <div className="mission-list-item-top">
                  <strong>{operator.displayName}</strong>
                  <span className={operator.isActive ? "status-pill status-ok" : "status-pill status-error"}>
                    {operator.isActive ? "active" : "inactive"}
                  </span>
                </div>
                <p>@{operator.username}</p>
                <dl className="mission-meta-grid">
                  <div>
                    <dt>Email</dt>
                    <dd>{operator.email ?? "Not reported"}</dd>
                  </div>
                  <div>
                    <dt>Roles</dt>
                    <dd>{operator.roles.join(", ")}</dd>
                  </div>
                </dl>
              </button>
            ))}
          </div>
        </section>

        <section className="operator-form-panel">
          <div className="mission-list-header">
            <div>
              <p className="eyebrow">Provision</p>
              <h3>Create Operator User</h3>
            </div>
          </div>

          <form className="auth-form" onSubmit={handleCreateOperator}>
            <div className="form-grid-two">
              <label className="field">
                <span>Username</span>
                <input
                  className="input"
                  maxLength={100}
                  onChange={(event) =>
                    setDraft((current) => ({
                      ...current,
                      username: event.target.value
                    }))
                  }
                  required
                  value={draft.username}
                />
              </label>

              <label className="field">
                <span>Email</span>
                <input
                  className="input"
                  maxLength={200}
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
              </label>
            </div>

            <div className="form-grid-two">
              <label className="field">
                <span>First name</span>
                <input
                  className="input"
                  maxLength={80}
                  onChange={(event) =>
                    setDraft((current) => ({
                      ...current,
                      firstName: event.target.value
                    }))
                  }
                  required
                  value={draft.firstName}
                />
              </label>

              <label className="field">
                <span>Last name</span>
                <input
                  className="input"
                  maxLength={80}
                  onChange={(event) =>
                    setDraft((current) => ({
                      ...current,
                      lastName: event.target.value
                    }))
                  }
                  required
                  value={draft.lastName}
                />
              </label>
            </div>

            <div className="form-grid-two">
              <label className="field">
                <span>Password</span>
                <input
                  className="input"
                  minLength={8}
                  onChange={(event) =>
                    setDraft((current) => ({
                      ...current,
                      password: event.target.value
                    }))
                  }
                  required
                  type="password"
                  value={draft.password}
                />
              </label>
            </div>

            <label className="field">
              <span>Route contract</span>
              <input className="input" disabled value={operatorsUrl} />
              <span className="field-hint">
                This slice uses GET, POST, and POST /{userId}/deactivate through the edge-proxy facade.
              </span>
            </label>

            <div className="mission-action-row">
              <button className="primary-button" disabled={isSubmitting} type="submit">
                Create Operator User
              </button>
            </div>
          </form>

          <section className="operator-detail-card">
            <div className="mission-list-header">
              <div>
                <p className="eyebrow">Selected User</p>
                <h3>{selectedOperator?.displayName ?? "No Operator User selected"}</h3>
              </div>
            </div>

            {selectedOperator ? (
              <dl className="definition-grid">
                <div>
                  <dt>User ID</dt>
                  <dd>{selectedOperator.id}</dd>
                </div>
                <div>
                  <dt>Username</dt>
                  <dd>{selectedOperator.username}</dd>
                </div>
                <div>
                  <dt>Email</dt>
                  <dd>{selectedOperator.email ?? "Not reported by facade"}</dd>
                </div>
                <div>
                  <dt>First name</dt>
                  <dd>{selectedOperator.firstName ?? "Not reported by facade"}</dd>
                </div>
                <div>
                  <dt>Last name</dt>
                  <dd>{selectedOperator.lastName ?? "Not reported by facade"}</dd>
                </div>
                <div>
                  <dt>Roles</dt>
                  <dd>{selectedOperator.roles.join(", ")}</dd>
                </div>
                <div>
                  <dt>Status</dt>
                  <dd>{selectedOperator.isActive ? "Active" : "Inactive"}</dd>
                </div>
                <div>
                  <dt>Last update</dt>
                  <dd>{formatTimestamp(selectedOperator.lastUpdatedAt)}</dd>
                </div>
              </dl>
            ) : (
              <div className="empty-state">
                <strong>No Operator User selected.</strong>
                <p>Select a roster entry after the facade returns data.</p>
              </div>
            )}

            <div className="mission-action-row">
              <button
                className="ghost-button danger-button"
                disabled={!selectedOperator?.isActive || isDeactivating}
                onClick={() => void handleDeactivateSelectedOperator()}
                type="button"
              >
                {isDeactivating ? "Deactivating..." : "Deactivate Operator User"}
              </button>
            </div>
          </section>
        </section>
      </div>
    </section>
  );
}
