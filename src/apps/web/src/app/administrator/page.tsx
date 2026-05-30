import { DashboardShell } from "@/components/dashboard-shell";
import { MissionsAdminWorkspace } from "@/components/missions-admin-workspace";
import { getRequiredSession } from "@/lib/session";

export default async function AdministratorPage() {
  const session = await getRequiredSession(["Administrator"]);

  return (
    <DashboardShell
      focus={[
        "Protect administrator route with server-side cookie guard and role-based redirects.",
        "Drive Mission CRUD directly against the protected mission-design API boundary.",
        "Expose realtime connection state and smoke checks beside control-plane workflows.",
        "Leave user management and Mission Stage follow-ups outside this slice."
      ]}
      mainContent={<MissionsAdminWorkspace accessToken={session.accessToken} />}
      role="Administrator"
      session={session}
      summary="Administration shell now covers Mission catalog control while retaining identity, connectivity, and service verification."
      title="Administrator Mission control"
    />
  );
}
