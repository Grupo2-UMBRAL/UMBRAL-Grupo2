import { Suspense } from "react";
import { redirect } from "next/navigation";
import { LoginForm } from "@/components/login-form";
import { getOptionalSession, resolveDashboardPath } from "@/lib/session";

export default async function LoginPage() {
  const session = await getOptionalSession();
  if (session) {
    redirect(resolveDashboardPath(session));
  }

  return (
    <main className="auth-page">
      <section className="auth-copy">
        <p className="eyebrow">UMBRAL web shell</p>
        <h1>Calm operations. Fast auth. Real transport.</h1>
        <p>
          Administrator and Operator enter through Keycloak, then shell proves protected routes,
          JWT-backed API calls, and SignalR reconnect flow without pretending business features exist.
        </p>
        <ul className="hint-list">
          <li>`admin / admin123!` routes to Administrator workspace.</li>
          <li>`operator / operator123!` routes to Operator workspace.</li>
          <li>`participant / participant123!` is rejected from web shell by role.</li>
        </ul>
      </section>

      <section className="auth-panel">
        <div className="auth-card">
          <div>
            <p className="eyebrow">Sign in</p>
            <h2>Enter local operator shell</h2>
          </div>
          <Suspense fallback={<p>Loading auth form...</p>}>
            <LoginForm />
          </Suspense>
        </div>
      </section>
    </main>
  );
}
