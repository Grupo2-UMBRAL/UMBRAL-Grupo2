"use client";

import type { ReactNode } from "react";
import { useState } from "react";
import { LogoutButton } from "@/components/logout-button";
import { LiveSessionConnection } from "@/components/live-session-connection";
import { ServiceStatusBoard } from "@/components/service-status-board";
import { roleLabel, type WebShellRole } from "@/lib/roles";
import type { UmbralWebSession } from "@/lib/session-cookie";

type DashboardShellProps = {
  role: WebShellRole;
  session: UmbralWebSession;
  title: string;
  summary: string;
  focus: string[];
  mainContent?: ReactNode;
  sideContent?: ReactNode;
};

function formatExpiryTimestamp(expiresAt: string) {
  const expiryDate = new Date(expiresAt);
  if (Number.isNaN(expiryDate.getTime())) {
    return expiresAt;
  }

  return expiryDate.toISOString().replace("T", " ").replace(/\.\d{3}Z$/, " UTC");
}

export function DashboardShell({
  role,
  session,
  title,
  summary,
  focus,
  mainContent,
  sideContent
}: DashboardShellProps) {
  const [refreshKey, setRefreshKey] = useState(0);
  const [isRailCollapsed, setIsRailCollapsed] = useState(false);

  return (
    <main className={`shell-page ${isRailCollapsed ? "is-rail-collapsed" : ""}`}>
      {isRailCollapsed && (
        <button
          className="rail-toggle-tab"
          onClick={() => setIsRailCollapsed(false)}
          type="button"
          aria-label="Expandir menú"
          title="Expandir menú"
        >
          ➡️
        </button>
      )}

      <aside className="shell-rail">
        <div className="rail-collapse-header">
          <button
            className="ghost-button compact-button collapse-btn"
            onClick={() => setIsRailCollapsed(true)}
            type="button"
            title="Colapsar menú"
          >
            ⬅️ Ocultar menú
          </button>
        </div>

        <div className="brand-block">
          <p className="eyebrow">UMBRAL web</p>
          <h1>{roleLabel(role)}</h1>
          <p>{summary}</p>
        </div>

        <nav className="rail-nav">
          <a className={role === "Administrator" ? "rail-link is-active" : "rail-link"} href="/administrator">
            Administrador
          </a>
          <a className={role === "Operator" ? "rail-link is-active" : "rail-link"} href="/operator">
            Operador
          </a>
        </nav>

        <div className="panel compact-panel">
          <p className="eyebrow">Sesión</p>
          <strong>{session.displayName}</strong>
          <p>{session.username}</p>
          <p>{session.roles.join(", ")}</p>
          <p>
            Expira <time dateTime={session.expiresAt}>{formatExpiryTimestamp(session.expiresAt)}</time>
          </p>
          <LogoutButton />
        </div>
      </aside>

      <section className="shell-main">
        <header className="hero-panel">
          <div>
            <p className="eyebrow">Consola operativa</p>
            <h2>{title}</h2>
          </div>
          <p className="hero-copy">{summary}</p>
        </header>

        <section className="panel stack-gap">
          <div className="section-heading">
            <div>
              <p className="eyebrow">Alcance actual</p>
              <h2>Lo que esta consola demuestra</h2>
            </div>
            <p className="section-copy">
              Sin pantallas de negocio falsas. Solo autenticación, enrutamiento protegido, transporte en vivo, configuración y estructura operativa.
            </p>
          </div>

          <div className="focus-list">
            {focus.map((item) => (
              <article className="focus-item" key={item}>
                <span className="focus-dot" />
                <p>{item}</p>
              </article>
            ))}
          </div>
        </section>

        <ServiceStatusBoard accessToken={session.accessToken} refreshKey={refreshKey} role={role} />

        {mainContent}
      </section>

      <aside className="shell-side">
        <LiveSessionConnection
          accessToken={session.accessToken}
          onResync={() => {
            setRefreshKey((current) => current + 1);
          }}
        />

        <section className="panel stack-gap">
          <div className="section-heading">
            <div>
              <p className="eyebrow">Verificación</p>
              <h2>Modos de falla cubiertos</h2>
            </div>
          </div>

          <ul className="plain-list">
            <li>El inicio de sesión del participante es rechazado de la consola web.</li>
            <li>La cookie expirada redirige de vuelta al inicio de sesión.</li>
            <li>La reconexión de SignalR invoca el callback de resincronización.</li>
            <li>Las validaciones de servicios protegidos muestran fallas de error 401 o 403 en vivo.</li>
          </ul>
        </section>

        {sideContent}
      </aside>
    </main>
  );
}
