export default function ForbiddenPage() {
  return (
    <main className="forbidden-page">
      <section className="panel forbidden-card stack-gap">
        <p className="eyebrow">Acceso Denegado</p>
        <h1>Su rol no tiene acceso a esta sección.</h1>
        <p>
          La consola web solo admite rutas permitidas para `Administrator` (Administrador) u `Operator` (Operador). 
          Regrese al inicio de sesión o cambie a una ruta correspondiente a su rol.
        </p>
        <a className="primary-button" href="/login">
          Volver al inicio de sesión
        </a>
      </section>
    </main>
  );
}
