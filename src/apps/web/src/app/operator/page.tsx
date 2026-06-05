import { DashboardShell } from "@/components/dashboard-shell";
import { LiveSessionsWorkspace } from "@/components/live-sessions-workspace";
import { getRequiredSession } from "@/lib/session";

export default async function OperatorPage() {
  const session = await getRequiredSession(["Operator", "Administrator"]);

  return (
    <DashboardShell
      focus={[
        "Proteger la ruta del operador con middleware basado en roles y validaciones del servidor.",
        "Crear LiveSessions programadas a partir de capturas de Misiones activas expuestas por el Diseño de Misión.",
        "Permitir al Operador recortar y reordenar el Flujo de Etapas de Sesión sin modificar los datos reutilizables de la Misión.",
        "Mantener visible el transporte en vivo con señales de reconexión y resincronización."
      ]}
      mainContent={<LiveSessionsWorkspace accessToken={session.accessToken} />}
      role="Operator"
      session={session}
      summary="La consola del operador ahora programa LiveSessions a partir de Misiones activas y conserva los estados de falla explícitos."
      title="Espacio de trabajo de programación del Operador"
    />
  );
}
