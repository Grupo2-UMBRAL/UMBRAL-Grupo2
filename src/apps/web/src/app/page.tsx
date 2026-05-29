import { redirect } from "next/navigation";
import { getOptionalSession, resolveDashboardPath } from "@/lib/session";

export default async function HomePage() {
  const session = await getOptionalSession();

  if (!session) {
    redirect("/login");
  }

  redirect(resolveDashboardPath(session));
}
