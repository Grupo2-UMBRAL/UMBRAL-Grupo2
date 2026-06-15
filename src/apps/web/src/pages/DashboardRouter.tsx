import { useEffect } from "react";
import { useAuth } from "@/context/auth-context";
import { useNavigate } from "react-router-dom";

export default function DashboardRouter() {
  const { isAuthenticated, roles, loading } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    if (loading) return;

    if (!isAuthenticated) {
      navigate("/");
      return;
    }

    if (roles.includes("Administrator")) {
      navigate("/administrator");
    } else if (roles.includes("Operator")) {
      navigate("/operator");
    } else {
      navigate("/forbidden");
    }
  }, [isAuthenticated, roles, loading, navigate]);

  return (
    <div className="loading-page">
      <p>Redirigiendo al espacio de trabajo...</p>
    </div>
  );
}
