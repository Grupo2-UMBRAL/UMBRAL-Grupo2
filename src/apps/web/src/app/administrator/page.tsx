import { DashboardShell } from "@/components/dashboard-shell";
import { getRequiredSession } from "@/lib/session";

export default async function AdministratorPage() {
  const session = await getRequiredSession(["Administrator"]);

  return (
    <DashboardShell
      focus={[
        "Protect administrator route with server-side cookie guard.",
        "Validate JWT against all protected service smoke endpoints.",
        "Expose realtime connection state before operational dashboard exists.",
        "Keep room for mission management and user management slices."
      ]}
      role="Administrator"
      session={session}
      summary="Administration shell tuned for identity, access, connectivity, and future control-plane slices."
      title="Administrator base layout"
    />
  );
}
