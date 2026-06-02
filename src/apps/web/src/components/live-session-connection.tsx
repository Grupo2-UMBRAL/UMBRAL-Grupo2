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
          <p className="eyebrow">Realtime</p>
          <h2>Session stream</h2>
        </div>
        <p className="section-copy">
          Hook points to `/session-hub/hubs/session`, uses JWT query auth, reconnects automatically,
          then asks shell to resync.
        </p>
      </div>

      <div className={`signal-card signal-${connection.kind}`}>
        <strong>{connection.kind}</strong>
        <p>{connection.detail}</p>
      </div>

      <dl className="definition-grid">
        <div>
          <dt>Hub URL</dt>
          <dd>{config.sessionHubUrl}</dd>
        </div>
        <div>
          <dt>Resync count</dt>
          <dd>{resyncCount}</dd>
        </div>
      </dl>

      {lastStateChange ? (
        <div className="signal-card signal-connected">
          <strong>Last lifecycle event</strong>
          <p>
            {lastStateChange.previousState} -&gt; {lastStateChange.state}
          </p>
          <p>
            Session {lastStateChange.liveSessionId} with {lastStateChange.registeredSessionTeamCount} team(s)
          </p>
        </div>
      ) : null}
    </section>
  );
}
