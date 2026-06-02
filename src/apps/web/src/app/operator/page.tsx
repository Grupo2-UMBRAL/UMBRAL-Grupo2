import { DashboardShell } from "@/components/dashboard-shell";
import { LiveSessionsWorkspace } from "@/components/live-sessions-workspace";
import { getRequiredSession } from "@/lib/session";

export default async function OperatorPage() {
  const session = await getRequiredSession(["Operator", "Administrator"]);

  return (
    <DashboardShell
      focus={[
        "Protect operator route with role-aware middleware and server checks.",
        "Create Scheduled LiveSessions from active Mission snapshots exposed by Mission Design.",
        "Let Operator trim and reorder Session Stage Flow without mutating reusable Mission data.",
        "Keep live transport visible with reconnect and resync signals."
      ]}
      mainContent={<LiveSessionsWorkspace accessToken={session.accessToken} />}
      role="Operator"
      session={session}
      summary="Operator shell now schedules LiveSessions from active Missions and preserves explicit failure states."
      title="Operator scheduling workspace"
    />
  );
}
