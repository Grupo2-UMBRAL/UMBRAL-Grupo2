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
        <p className="eyebrow">Consola web de UMBRAL</p>
        <h1>Operaciones tranquilas. Autenticación rápida. Transporte real.</h1>
        <p>
          El Administrador y el Operador ingresan a través de Keycloak, luego la consola valida rutas protegidas,
          llamadas API respaldadas por JWT y el flujo de reconexión de SignalR sin simular que existen características comerciales.
        </p>
        <ul className="hint-list">
          <li>`admin / admin123!` dirige al espacio de trabajo del Administrador.</li>
          <li>`operator / operator123!` dirige al espacio de trabajo del Operador.</li>
          <li>`participant / participant123!` es rechazado de la consola web por rol.</li>
        </ul>
      </section>

      <section className="auth-panel">
        <div className="auth-card">
          <div>
            <p className="eyebrow">Iniciar sesión</p>
            <h2>Ingresar a la consola local de operaciones</h2>
          </div>
          <Suspense fallback={<p>Cargando formulario de autenticación...</p>}>
            <LoginForm />
          </Suspense>
        </div>
      </section>
    </main>
  );
}
