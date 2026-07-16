import { useEffect } from "react";
import { useAuth } from "@/context/auth-context";
import { useNavigate } from "react-router-dom";
import { DashboardLayout } from "@/components/dashboard-layout";
import { LiveSessionsWorkspace } from "@/components/live-sessions-workspace";

export default function OperatorPage() {
  const { isAuthenticated, token, roles, loading } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    if (loading) return;

    if (!isAuthenticated) {
      navigate("/");
      return;
    }

    const isOperatorOrAdmin =
      roles.includes("Operator") || roles.includes("Administrator");
    if (!isOperatorOrAdmin) {
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
      <LiveSessionsWorkspace accessToken={token} />
    </DashboardLayout>
  );
}
