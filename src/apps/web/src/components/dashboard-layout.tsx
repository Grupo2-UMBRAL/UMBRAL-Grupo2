import { useState, type ReactNode } from "react";
import { useAuth } from "@/context/auth-context";
import type { WebShellRole } from "@/lib/roles";
import { Link } from "react-router-dom";

type DashboardLayoutProps = {
  role: WebShellRole;
  title: string;
  children: ReactNode;
  headerActions?: ReactNode;
};

export function DashboardLayout({
  role,
  title,
  children,
  headerActions,
}: DashboardLayoutProps) {
  const { user, roles, logout } = useAuth();
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);

  const initials = user
    ? (user.displayName || user.username)
        .split(" ")
        .map((w) => w[0])
        .join("")
        .slice(0, 2)
        .toUpperCase()
    : "U";

  return (
    <div className={`dashboard ${sidebarCollapsed ? "sidebar-collapsed" : ""}`}>
      {/* Sidebar */}
      <aside className="sidebar">
        <div className="sidebar-brand">
          <div className="sidebar-brand-name">UMBRAL</div>
          <div className="sidebar-brand-sub">Consola operativa</div>
        </div>

        <nav className="sidebar-nav">
          <div className="sidebar-section-label">Espacios de trabajo</div>
          {roles.includes("Administrator") && (
            <Link
              to="/administrator"
              className={`nav-link ${role === "Administrator" ? "is-active" : ""}`}
            >
              <svg className="nav-link-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                <rect x="3" y="3" width="7" height="7" rx="1" />
                <rect x="14" y="3" width="7" height="7" rx="1" />
                <rect x="3" y="14" width="7" height="7" rx="1" />
                <rect x="14" y="14" width="7" height="7" rx="1" />
              </svg>
              Administrador
            </Link>
          )}
          <Link
            to="/operator"
            className={`nav-link ${role === "Operator" ? "is-active" : ""}`}
          >
            <svg className="nav-link-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
              <circle cx="12" cy="12" r="3" />
              <path d="M12 1v4M12 19v4M4.22 4.22l2.83 2.83M16.95 16.95l2.83 2.83M1 12h4M19 12h4M4.22 19.78l2.83-2.83M16.95 7.05l2.83-2.83" />
            </svg>
            Operador
          </Link>
        </nav>

        <div className="sidebar-footer">
          <div className="sidebar-user">
            <div className="sidebar-avatar">{initials}</div>
            <div className="sidebar-user-info">
              <div className="sidebar-user-name">
                {user?.displayName ?? "Usuario"}
              </div>
              <div className="sidebar-user-role">
                {roles.join(", ")}
              </div>
            </div>
          </div>
          <button
            className="btn btn-ghost btn-sm btn-block"
            style={{ marginTop: "0.75rem" }}
            onClick={() => void logout()}
          >
            Cerrar sesión
          </button>
        </div>
      </aside>

      {/* Main content */}
      <div className="main-area">
        <header className="content-header">
          <div className="content-header-left">
            <button
              className="btn btn-ghost btn-sm sidebar-toggle"
              onClick={() => setSidebarCollapsed((collapsed) => !collapsed)}
              type="button"
              aria-label={sidebarCollapsed ? "Mostrar menú" : "Ocultar menú"}
              title={sidebarCollapsed ? "Mostrar menú" : "Ocultar menú"}
            >
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                <line x1="3" y1="6" x2="21" y2="6" />
                <line x1="3" y1="12" x2="21" y2="12" />
                <line x1="3" y1="18" x2="21" y2="18" />
              </svg>
            </button>
            <h2>{title}</h2>
          </div>
          {headerActions && (
            <div className="content-header-actions">{headerActions}</div>
          )}
        </header>
        <div className="content-body page-enter">{children}</div>
      </div>
    </div>
  );
}
