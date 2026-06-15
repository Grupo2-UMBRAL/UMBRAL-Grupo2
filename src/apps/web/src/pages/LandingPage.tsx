import { useEffect } from "react";
import { useAuth } from "@/context/auth-context";
import { useNavigate } from "react-router-dom";
import heroImage from "@/assets/hero.png";

export default function LandingPage() {
  const { isAuthenticated, login, loading } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    if (isAuthenticated) {
      navigate("/dashboard");
    }
  }, [isAuthenticated, navigate]);

  return (
    <div className="landing">
      {/* Navigation */}
      <nav className="landing-nav">
        <div className="landing-logo">UMBRAL</div>
        <div className="landing-nav-links">
          <a href="#capacidades" className="landing-nav-link">
            Capacidades
          </a>
          <button
            className="btn btn-primary btn-sm"
            onClick={login}
            disabled={loading}
          >
            Ingresar
          </button>
        </div>
      </nav>

      {/* Hero */}
      <section className="hero">
        <div className="hero-content">
          <h1>Control de misiones en tiempo real</h1>
          <p>
            Plataforma de operaciones para experiencias inmersivas. Gestiona
            Treasure Hunt y Trivia, monitorea equipos y puntajes en vivo.
          </p>
          <div className="hero-actions">
            {loading ? (
              <button className="btn btn-primary btn-lg" disabled>
                Conectando...
              </button>
            ) : (
              <button className="btn btn-primary btn-lg" onClick={login}>
                Ingresar a Consola
              </button>
            )}
            <a href="#capacidades">
              <button className="btn btn-ghost btn-lg">Explorar</button>
            </a>
          </div>
        </div>
        <div className="hero-image">
          <img
            src={heroImage}
            alt="Vista de operaciones UMBRAL mostrando rutas y puntos de misión"
          />
        </div>
      </section>

      {/* Features */}
      <section id="capacidades" className="features">
        <div className="features-header">
          <h2>Capacidades de la plataforma</h2>
        </div>
        <div className="features-grid">
          <article className="feature-card">
            <span className="feature-number">01</span>
            <h3>Diseño de Misiones</h3>
            <p>
              Crea misiones con nodos jerárquicos, etapas configurables, pistas
              geolocalizadas y niveles de dificultad ajustables por nodo.
            </p>
          </article>
          <article className="feature-card">
            <span className="feature-number">02</span>
            <h3>Operaciones en Vivo</h3>
            <p>
              Controla sesiones en tiempo real. Inicia, pausa o finaliza etapas.
              Libera pistas y aplica penalizaciones al instante.
            </p>
          </article>
          <article className="feature-card">
            <span className="feature-number">03</span>
            <h3>Auditoría y Puntajes</h3>
            <p>
              Ranking actualizado en tiempo real con desglose de puntos por
              dificultad, penalizaciones y registro completo de eventos.
            </p>
          </article>
        </div>
      </section>

      {/* Footer */}
      <footer className="landing-footer">
        <p>&copy; {new Date().getFullYear()} UMBRAL. Todos los derechos reservados.</p>
      </footer>
    </div>
  );
}
