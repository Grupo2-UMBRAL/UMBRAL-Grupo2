import { DashboardShell } from "@/components/dashboard-shell";
import { getRequiredSession } from "@/lib/session";

export default async function OperatorPage() {
  const session = await getRequiredSession(["Operator", "Administrator"]);

  return (
    <DashboardShell
      focus={[
        "Protect operator route with role-aware middleware and server checks.",
        "Keep live transport visible with reconnect and resync signals.",
        "Show real backend readiness instead of fake rankings or fake missions.",
        "Leave center pane ready for session dashboard follow-up ticket."
      ]}
      role="Operator"
      session={session}
      summary="Operator shell favors situational awareness, quick scan, and explicit failure states."
      title="Operator base layout"
    />
  );
}
