import { useEffect } from "react";
import { useAuth } from "@/context/auth-context";
import { useNavigate } from "react-router-dom";
import { DashboardLayout } from "@/components/dashboard-layout";
import { MissionsAdminWorkspace } from "@/components/missions-admin-workspace";
import { OperatorUsersWorkspace } from "@/components/operator-users-workspace";
import { ServiceStatusBoard } from "@/components/service-status-board";

export default function AdministratorPage() {
  const { isAuthenticated, token, roles, loading } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    if (!loading && (!isAuthenticated || !roles.includes("Administrator"))) {
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
    <DashboardLayout role="Administrator" title="Panel de Administrador">
      <ServiceStatusBoard accessToken={token} role="Administrator" />
      <OperatorUsersWorkspace accessToken={token} />
      <MissionsAdminWorkspace accessToken={token} />
    </DashboardLayout>
  );
}
