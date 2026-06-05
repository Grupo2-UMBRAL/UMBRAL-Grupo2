import { DashboardShell } from "@/components/dashboard-shell";
import { MissionsAdminWorkspace } from "@/components/missions-admin-workspace";
import { OperatorUsersWorkspace } from "@/components/operator-users-workspace";
import { getRequiredSession } from "@/lib/session";

export default async function AdministratorPage() {
  const session = await getRequiredSession(["Administrator"]);

  return (
    <DashboardShell
      focus={[
        "Proteger la ruta del administrador con protección de cookies del lado del servidor y redirecciones basadas en roles.",
        "Administrar Usuarios Operadores a través de la fachada de identity-access sobre edge-proxy con el token de portador actual.",
        "Rotar contraseñas de Usuarios Operadores a través del mismo límite de identity-access en lugar de la administración directa de Keycloak.",
        "Realizar el CRUD de Misiones directamente contra el límite de API protegido de mission-design.",
        "Exponer el estado de conexión en tiempo real y pruebas de humo junto a los flujos de trabajo del panel de control.",
        "Dejar las ediciones de roles y el seguimiento de las etapas de la misión fuera de este alcance."
      ]}
      mainContent={
        <>
          <OperatorUsersWorkspace accessToken={session.accessToken} />
          <MissionsAdminWorkspace accessToken={session.accessToken} />
        </>
      }
      role="Administrator"
      session={session}
      summary="La consola de administración ahora abarca el aprovisionamiento de Usuarios Operadores y el control del catálogo de Misiones, manteniendo la verificación de identidad, conectividad y servicios."
      title="Panel de control del Administrador"
    />
  );
}
