import { useNavigate } from "react-router-dom";

export default function ForbiddenPage() {
  const navigate = useNavigate();

  return (
    <main className="forbidden-page page-enter">
      <div className="forbidden-card">
        <span className="eyebrow">Acceso Denegado</span>
        <h1>Sin permisos para esta sección</h1>
        <p>
          La consola web solo admite acceso para usuarios con rol de
          Administrador u Operador. Contacta al administrador si crees que esto
          es un error.
        </p>
        <button className="btn btn-primary btn-block" onClick={() => navigate("/")}>
          Volver al inicio
        </button>
      </div>
    </main>
  );
}
