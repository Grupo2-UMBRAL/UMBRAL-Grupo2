import { DashboardShell } from "@/components/dashboard-shell";
import { MissionsAdminWorkspace } from "@/components/missions-admin-workspace";
import { OperatorUsersWorkspace } from "@/components/operator-users-workspace";
import { getRequiredSession } from "@/lib/session";

export default async function AdministratorPage() {
  const session = await getRequiredSession(["Administrator"]);

  return (
    <DashboardShell
      focus={[
        "Protect administrator route with server-side cookie guard and role-based redirects.",
        "Manage Operator Users through the identity-access facade over edge-proxy with the current bearer token.",
        "Drive Mission CRUD directly against the protected mission-design API boundary.",
        "Expose realtime connection state and smoke checks beside control-plane workflows.",
        "Leave role edits, password rotation, and Mission Stage follow-ups outside this slice."
      ]}
      mainContent={
        <>
          <OperatorUsersWorkspace accessToken={session.accessToken} />
          <MissionsAdminWorkspace accessToken={session.accessToken} />
        </>
      }
      role="Administrator"
      session={session}
      summary="Administration shell now covers Operator User provisioning and Mission catalog control while retaining identity, connectivity, and service verification."
      title="Administrator control plane"
    />
  );
}
