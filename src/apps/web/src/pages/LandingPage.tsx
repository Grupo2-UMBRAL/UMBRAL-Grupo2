import { useEffect } from "react";
import { useAuth } from "@/context/auth-context";
import { useNavigate } from "react-router-dom";
import { MascotSvg } from "@/components/mascot-svg";
import "./landing.css";

// TODO: reemplazar por las URLs reales cuando la app esté publicada en las tiendas.
const APP_STORE_URL = "#";
const PLAY_STORE_URL = "#";

function Blobs() {
  return (
    <div className="lp-blobs" aria-hidden>
      <div className="lp-blob g" />
      <div className="lp-blob b" />
      <div className="lp-blob a" />
    </div>
  );
}

/** Apple + Google Play store badges. Links live in the constants above. */
function StoreBadges() {
  return (
    <div className="lp-stores">
      <a className="lp-store" href={APP_STORE_URL} aria-label="Descárgala en el App Store">
        <svg width="22" height="22" viewBox="0 0 384 512" fill="#fff" aria-hidden>
          <path d="M318.7 268.7c-.2-36.7 16.4-64.4 50-84.8-18.8-26.9-47.2-41.7-84.7-44.6-35.5-2.8-74.3 20.7-88.5 20.7-15 0-49.4-19.7-76.4-19.7C63.3 141.2 4 184.8 4 273.5q0 39.3 14.4 81.2c12.8 36.7 59 126.7 107.2 125.2 25.2-.6 43-17.9 75.8-17.9 31.8 0 48.3 17.9 76.4 17.9 48.6-.7 90.4-82.5 102.6-119.3-65.2-30.7-61.7-90-61.7-91.9zM262.1 104.5c27.3-32.4 24.8-61.9 24-72.5-24.1 1.4-52 16.4-67.9 34.9-17.5 19.8-27.8 44.3-25.6 71.9 26.1 2 49.9-11.4 69.5-34.3z" />
        </svg>
        <span className="lp-store-text">
          <small>Descárgala en el</small>
          <strong>App Store</strong>
        </span>
      </a>
      <a className="lp-store" href={PLAY_STORE_URL} aria-label="Disponible en Google Play">
        <svg width="20" height="22" viewBox="0 0 512 512" aria-hidden>
          <path fill="#EA4335" d="M325.3 234.3 105.9 13l280.7 161.2-61.3 60.1z" />
          <path fill="#FBBC04" d="M104.6 13c-4.4 2.3-7.6 6.5-7.6 12.1v461.8c0 5.6 3.2 9.8 7.6 12.1l256.6-243z" />
          <path fill="#4285F4" d="M447.7 255.5c15.8 9.3 15.8 24.7 0 34l-61.1 35-61.3-60.1 61.3-60.1z" />
          <path fill="#34A853" d="M104.6 499c4.5 2.4 10.2 2 16.4-1.7L386.5 328l-61.3-60.1z" />
        </svg>
        <span className="lp-store-text">
          <small>Disponible en</small>
          <strong>Google Play</strong>
        </span>
      </a>
    </div>
  );
}

export default function LandingPage() {
  const { isAuthenticated, login, loading } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    if (isAuthenticated) {
      navigate("/dashboard");
    }
  }, [isAuthenticated, navigate]);

  return (
    <div className="lp">
      <Blobs />

      <nav className="lp-nav centered">
        <div className="lp-nav-side left">
          <a className="lp-nav-link" href="#capacidades">
            Cómo funciona
          </a>
        </div>
        <div className="lp-brand lp-brand-lg">
          <MascotSvg size={52} />
          <span className="lp-brand-word">UMBRAL</span>
        </div>
        <div className="lp-nav-side right">
          <button className="lp-btn lp-btn-ghost lp-btn-sm" onClick={login} disabled={loading}>
            Soy operador
          </button>
        </div>
      </nav>

      <header className="lpb-hero">
        <div className="lpb-copy">
          <span className="lp-pill lp-pill-green">Aventura en tiempo real</span>
          <h1>
            Convierte cualquier lugar en una <span className="u">aventura</span>!
          </h1>
          <p>
            Únete a una sesión con tu equipo, escanea pistas y responde trivia mientras
            compites en vivo. Treasure Hunt y Trivia, directo desde tu teléfono.
          </p>
          <StoreBadges />
          <div className="lpb-actions">
            <button className="lp-btn lp-btn-green lp-btn-lg" onClick={login} disabled={loading}>
              {loading ? "Conectando…" : "Únete a una sesión"}
            </button>
            <a href="#capacidades">
              <button className="lp-btn lp-btn-ghost lp-btn-lg">Cómo funciona</button>
            </a>
          </div>
        </div>

        {/* live console preview */}
        <div className="lpb-preview">
          <div className="lpb-preview-mascot">
            <MascotSvg size={78} mood="celebrate" />
          </div>
          <div className="lpb-pv-head">
            <span className="lpb-pv-title">Sesión · Centro Histórico</span>
            <span className="lpb-live">
              <span className="lpb-live-dot" /> En vivo
            </span>
          </div>

          <div className="lpb-team lead">
            <span className="lpb-medal g">1</span>
            <span className="lpb-team-name">Los Exploradores</span>
            <span className="lpb-team-score">2 450</span>
          </div>
          <div className="lpb-team">
            <span className="lpb-medal">2</span>
            <span className="lpb-team-name">Brújula Norte</span>
            <span className="lpb-team-score">2 180</span>
          </div>
          <div className="lpb-team">
            <span className="lpb-medal">3</span>
            <span className="lpb-team-name">Zorros Urbanos</span>
            <span className="lpb-team-score">1 990</span>
          </div>

          <div className="lpb-progress-label">Etapa 3 de 5 · Los Exploradores</div>
          <div className="lpb-track">
            <span className="lpb-seg done" />
            <span className="lpb-seg done" />
            <span className="lpb-seg cur" />
            <span className="lpb-seg" />
            <span className="lpb-seg" />
          </div>
        </div>
      </header>

      <section id="capacidades" className="lpb-feats">
        <article className="lpb-card">
          <div className="lpb-card-ico green">📲</div>
          <h3>Únete con tu equipo</h3>
          <p>Descarga la app, ingresa el código de la sesión y entra a la aventura en segundos.</p>
        </article>
        <article className="lpb-card">
          <div className="lpb-card-ico blue">🧭</div>
          <h3>Resuelve pistas y trivia</h3>
          <p>Escanea puntos con QR, responde preguntas y recibe feedback al instante en cada etapa.</p>
        </article>
        <article className="lpb-card">
          <div className="lpb-card-ico amber">🏆</div>
          <h3>Sube en el ranking</h3>
          <p>Mira tu puntaje moverse en vivo y compite por el primer lugar hasta la última etapa.</p>
        </article>
      </section>

      <footer className="lp-footer">
        © {new Date().getFullYear()} UMBRAL · Control de misiones en tiempo real
      </footer>
    </div>
  );
}
