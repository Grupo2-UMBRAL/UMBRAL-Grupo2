import { useEffect } from "react";
import { useAuth } from "@/context/auth-context";
import { useNavigate } from "react-router-dom";
import { DashboardLayout } from "@/components/dashboard-layout";
import { LiveSessionsWorkspace } from "@/components/live-sessions-workspace";
import { ServiceStatusBoard } from "@/components/service-status-board";

export default function OperatorPage() {
  const { isAuthenticated, token, roles, loading } = useAuth();
  const navigate = useNavigate();

  console.log("OperatorPage render:", { isAuthenticated, loading, roles, hasToken: !!token });

  useEffect(() => {
    const isOperatorOrAdmin =
      roles.includes("Operator") || roles.includes("Administrator");
    if (!loading && (!isAuthenticated || !isOperatorOrAdmin)) {
      navigate("/forbidden");
    }
  }, [isAuthenticated, roles, loading, navigate]);

  if (loading || !isAuthenticated || !token) {
    return (
      <div className="loading-page">
        <p>Cargando espacio de trabajo...</p>
      </div>
    );
  }

  return (
    <DashboardLayout role="Operator" title="Espacio del Operador">
      <ServiceStatusBoard accessToken={token} role="Operator" />
      <LiveSessionsWorkspace accessToken={token} />
    </DashboardLayout>
  );
}
