import { useCallback, useEffect, useState } from "react";
import { getClientConfig } from "@/lib/config";
import type { WebShellRole } from "@/lib/roles";

type ServiceCheck = {
  label: string;
  status: "pending" | "ok" | "error";
  detail: string;
};

type ServiceStatusBoardProps = {
  accessToken: string;
  role: WebShellRole;
};

export function ServiceStatusBoard({
  accessToken,
  role,
}: ServiceStatusBoardProps) {
  const [checks, setChecks] = useState<ServiceCheck[]>([]);

  const runChecks = useCallback(async () => {
    const config = getClientConfig();
    const base = config.edgeProxyPublicBaseUrl;
    const headers = { Authorization: `Bearer ${accessToken}` };

    const endpoints: { label: string; url: string; auth: boolean }[] = [
      {
        label: "Keycloak OIDC",
        url: `${base}/auth/realms/umbral/.well-known/openid-configuration`,
        auth: false,
      },
      {
        label: "Mission Design",
        url: `${base}/mission-design/api/mission-design/smoke/${role}`,
        auth: true,
      },
      {
        label: "Session Operations",
        url: `${base}/session-operations/api/session-operations/smoke/${role}`,
        auth: true,
      },
      {
        label: "Scoring & Audit",
        url: `${base}/scoring-audit/api/scoring-audit/smoke/${role}`,
        auth: true,
      },
    ];

    const results: ServiceCheck[] = endpoints.map((ep) => ({
      label: ep.label,
      status: "pending" as const,
      detail: "Verificando...",
    }));
    setChecks([...results]);

    for (let i = 0; i < endpoints.length; i++) {
      const ep = endpoints[i];
      try {
        const res = await fetch(ep.url, {
          headers: ep.auth ? headers : undefined,
        });
        results[i] = {
          label: ep.label,
          status: res.ok ? "ok" : "error",
          detail: res.ok ? `${res.status} OK` : `${res.status} ${res.statusText}`,
        };
      } catch (err) {
        results[i] = {
          label: ep.label,
          status: "error",
          detail: err instanceof Error ? err.message : "Error de conexión",
        };
      }
      setChecks([...results]);
    }
  }, [accessToken, role]);

  useEffect(() => {
    void runChecks();
  }, [runChecks]);

  const statusDotClass = (status: ServiceCheck["status"]) => {
    switch (status) {
      case "ok": return "status-dot status-dot-green";
      case "error": return "status-dot status-dot-red";
      default: return "status-dot status-dot-muted status-dot-pulse";
    }
  };

  return (
    <div className="card card-compact">
      <div className="row-between" style={{ marginBottom: "1rem" }}>
        <h3 style={{ fontSize: "var(--text-sm)", fontWeight: 600 }}>
          Estado de servicios
        </h3>
        <button className="btn btn-ghost btn-sm" onClick={() => void runChecks()}>
          Verificar
        </button>
      </div>
      <div className="status-grid">
        {checks.map((check) => (
          <div className="status-card" key={check.label}>
            <div className="status-card-header">
              <span className="status-card-label">{check.label}</span>
              <span className={statusDotClass(check.status)} />
            </div>
            <span className="status-card-detail">{check.detail}</span>
          </div>
        ))}
      </div>
    </div>
  );
}
