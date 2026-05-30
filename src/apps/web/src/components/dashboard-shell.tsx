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

  return (
    <main className="shell-page">
      <aside className="shell-rail">
        <div className="brand-block">
          <p className="eyebrow">UMBRAL web</p>
          <h1>{roleLabel(role)}</h1>
          <p>{summary}</p>
        </div>

        <nav className="rail-nav">
          <a className={role === "Administrator" ? "rail-link is-active" : "rail-link"} href="/administrator">
            Administrator
          </a>
          <a className={role === "Operator" ? "rail-link is-active" : "rail-link"} href="/operator">
            Operator
          </a>
        </nav>

        <div className="panel compact-panel">
          <p className="eyebrow">Session</p>
          <strong>{session.displayName}</strong>
          <p>{session.username}</p>
          <p>{session.roles.join(", ")}</p>
          <p>
            Expires <time dateTime={session.expiresAt}>{formatExpiryTimestamp(session.expiresAt)}</time>
          </p>
          <LogoutButton />
        </div>
      </aside>

      <section className="shell-main">
        <header className="hero-panel">
          <div>
            <p className="eyebrow">Operational shell</p>
            <h2>{title}</h2>
          </div>
          <p className="hero-copy">{summary}</p>
        </header>

        <section className="panel stack-gap">
          <div className="section-heading">
            <div>
              <p className="eyebrow">Scope now</p>
              <h2>What this shell proves</h2>
            </div>
            <p className="section-copy">
              No fake business screens. Only auth, protected routing, live transport, config, and operational
              framing.
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
              <p className="eyebrow">Verification</p>
              <h2>Failure modes covered</h2>
            </div>
          </div>

          <ul className="plain-list">
            <li>Participant login rejected from web shell.</li>
            <li>Expired cookie redirects back to login.</li>
            <li>SignalR reconnect calls resync callback.</li>
            <li>Protected service checks surface live 401 or 403 failures.</li>
          </ul>
        </section>

        {sideContent}
      </aside>
    </main>
  );
}
