"use client";

import { useEffect, useState } from "react";
import { getClientConfig } from "@/lib/config";
import type { WebShellRole } from "@/lib/roles";

type ServiceCheck = {
  id: string;
  label: string;
  path: string;
  requiresToken: boolean;
};

type ServiceCheckState = {
  id: string;
  label: string;
  status: "loading" | "ok" | "error";
  detail: string;
};

type ServiceStatusBoardProps = {
  accessToken: string;
  role: WebShellRole;
  refreshKey: number;
};

export function ServiceStatusBoard({
  accessToken,
  role,
  refreshKey
}: ServiceStatusBoardProps) {
  const [checks, setChecks] = useState<ServiceCheckState[]>([]);
  const config = getClientConfig();

  useEffect(() => {
    const roleRouteSegment = role === "Administrator" ? "administrator" : "operator";
    const serviceChecks: ServiceCheck[] = [
      {
        id: "identity",
        label: "Descubrimiento de Keycloak",
        path: `${config.keycloakPublicBaseUrl}/realms/${config.keycloakRealm}/.well-known/openid-configuration`,
        requiresToken: false
      },
      {
        id: "mission-design",
        label: "Prueba de humo de Diseño de Misiones",
        path: `${config.edgeProxyPublicBaseUrl}/mission-design/api/mission-design/smoke/${roleRouteSegment}`,
        requiresToken: true
      },
      {
        id: "session-operations",
        label: "Prueba de humo de Operaciones de Sesión",
        path: `${config.edgeProxyPublicBaseUrl}/session-operations/api/session-operations/smoke/${roleRouteSegment}`,
        requiresToken: true
      },
      {
        id: "scoring-audit",
        label: "Prueba de humo de Scoring y Auditoría",
        path: `${config.edgeProxyPublicBaseUrl}/scoring-audit/api/scoring-audit/smoke/${roleRouteSegment}`,
        requiresToken: true
      }
    ];

    let ignore = false;
    const loadingChecks = serviceChecks.map((check) => ({
      id: check.id,
      label: check.label,
      status: "loading",
      detail: "Verificando endpoint."
    })) satisfies ServiceCheckState[];

    async function runChecks() {
      setChecks(loadingChecks);

      const results = await Promise.all(
        serviceChecks.map(async (check) => {
          try {
            const response = await fetch(check.path, {
              headers: check.requiresToken
                ? {
                    Authorization: `Bearer ${accessToken}`
                  }
                : undefined
            });

            const detail = response.ok
              ? "El endpoint aceptó la solicitud."
              : `${response.status} ${response.statusText}`;

            return {
              id: check.id,
              label: check.label,
              status: response.ok ? "ok" : "error",
              detail
            } satisfies ServiceCheckState;
          } catch (error) {
            return {
              id: check.id,
              label: check.label,
              status: "error",
              detail: error instanceof Error ? error.message : "La solicitud falló."
            } satisfies ServiceCheckState;
          }
        })
      );

      if (!ignore) {
        setChecks(results);
      }
    }

    void runChecks();

    return () => {
      ignore = true;
    };
  }, [accessToken, config.edgeProxyPublicBaseUrl, config.keycloakPublicBaseUrl, config.keycloakRealm, refreshKey, role]);

  return (
    <section className="panel stack-gap">
      <div className="section-heading">
        <div>
          <p className="eyebrow">Disponibilidad</p>
          <h2>Verificaciones operativas</h2>
        </div>
        <p className="section-copy">
          El cliente del navegador accede al servicio de identidad y a los endpoints de API protegidos con el JWT actual.
        </p>
      </div>

      <div className="status-grid">
        {checks.map((check) => (
          <article className="status-card" key={check.id}>
            <div className={`status-pill status-${check.status}`}>
              {check.status === "loading" ? "cargando" : check.status === "ok" ? "correcto" : "error"}
            </div>
            <strong>{check.label}</strong>
            <p>{check.detail}</p>
          </article>
        ))}
      </div>
    </section>
  );
}
