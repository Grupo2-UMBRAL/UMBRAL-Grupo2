"use client";

import { useState } from "react";
import { getClientConfig } from "@/lib/config";
import {
  type LiveSessionStateChangedEvent,
  useSessionOperationsConnection
} from "@/hooks/use-session-operations-connection";

type LiveSessionConnectionProps = {
  accessToken: string;
  onResync: () => void;
};

export function LiveSessionConnection({
  accessToken,
  onResync
}: LiveSessionConnectionProps) {
  const config = getClientConfig();
  const [resyncCount, setResyncCount] = useState(0);
  const [lastStateChange, setLastStateChange] = useState<LiveSessionStateChangedEvent | null>(null);
  const connection = useSessionOperationsConnection({
    accessToken,
    hubUrl: config.sessionHubUrl,
    onResync: () => {
      setResyncCount((current) => current + 1);
      onResync();
    },
    onLiveSessionStateChanged: setLastStateChange
  });

  return (
    <section className="panel stack-gap">
      <div className="section-heading">
        <div>
          <p className="eyebrow">Tiempo real</p>
          <h2>Transmisión de sesión</h2>
        </div>
        <p className="section-copy">
          El hook apunta a `/session-hub/hubs/session`, utiliza autenticación por consulta JWT, se reconecta automáticamente y luego solicita la resincronización de la consola.
        </p>
      </div>

      <div className={`signal-card signal-${connection.kind}`}>
        <strong>{connection.kind}</strong>
        <p>{connection.detail}</p>
      </div>

      <dl className="definition-grid">
        <div>
          <dt>URL del Hub</dt>
          <dd>{config.sessionHubUrl}</dd>
        </div>
        <div>
          <dt>Cantidad de resincronizaciones</dt>
          <dd>{resyncCount}</dd>
        </div>
      </dl>

      {lastStateChange ? (
        <div className="signal-card signal-connected">
          <strong>Último evento de ciclo de vida</strong>
          <p>
            {lastStateChange.previousState} -&gt; {lastStateChange.state}
          </p>
          <p>
            Sesión {lastStateChange.liveSessionId} con {lastStateChange.registeredSessionTeamCount} equipo(s)
          </p>
        </div>
      ) : null}
    </section>
  );
}
