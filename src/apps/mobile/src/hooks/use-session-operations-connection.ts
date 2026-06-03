import { useEffect, useRef, useState } from "react";
import {
  HubConnectionBuilder,
  HttpTransportType,
  LogLevel,
  type HubConnection
} from "@microsoft/signalr";

export type ConnectionState =
  | { kind: "connecting"; detail: string }
  | { kind: "connected"; detail: string }
  | { kind: "reconnecting"; detail: string }
  | { kind: "disconnected"; detail: string }
  | { kind: "error"; detail: string };

type ConnectionOptions = {
  accessToken: string;
  hubUrl: string;
  enabled?: boolean;
  onResync?: () => void;
};

export function useSessionOperationsConnection({
  accessToken,
  hubUrl,
  enabled = true,
  onResync
}: ConnectionOptions) {
  const [state, setState] = useState<ConnectionState>({
    kind: "connecting",
    detail: "Opening participant stream."
  });
  const [connectionInstance, setConnectionInstance] = useState<HubConnection | null>(null);
  const resyncRef = useRef(onResync);

  useEffect(() => {
    resyncRef.current = onResync;
  }, [onResync]);

  useEffect(() => {
    let active = true;
    let connection: HubConnection | null = null;

    if (!enabled || !accessToken) {
      setConnectionInstance(null);
      setState({
        kind: "disconnected",
        detail: "Participant stream waiting for authenticated session."
      });
      return () => {
        active = false;
      };
    }

    async function startConnection() {
      setState({
        kind: "connecting",
        detail: "Opening participant stream."
      });

      connection = new HubConnectionBuilder()
        .withUrl(hubUrl, {
          accessTokenFactory: () => accessToken,
          skipNegotiation: false,
          transport: HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000])
        .configureLogging(LogLevel.Warning)
        .build();

      setConnectionInstance(connection);

      connection.onreconnecting(() => {
        if (!active) {
          return;
        }

        setState({
          kind: "reconnecting",
          detail: "Connection dropped. Waiting for SignalR reconnect."
        });
      });

      connection.onreconnected(() => {
        if (!active) {
          return;
        }

        setState({
          kind: "connected",
          detail: "Realtime participant stream restored."
        });
        resyncRef.current?.();
      });

      connection.onclose((error) => {
        if (!active) {
          return;
        }

        setConnectionInstance(null);
        setState({
          kind: error ? "error" : "disconnected",
          detail: error
            ? `SignalR closed: ${error.message}`
            : "SignalR closed before session resumed."
        });
      });

      try {
        await connection.start();
        if (!active) {
          await connection.stop();
          return;
        }

        setState({
          kind: "connected",
          detail: "Realtime participant stream connected."
        });
      } catch (error) {
        if (!active) {
          return;
        }

        setConnectionInstance(null);
        setState({
          kind: "error",
          detail: error instanceof Error ? error.message : "SignalR startup failed."
        });
      }
    }

    void startConnection();

    return () => {
      active = false;
      setConnectionInstance(null);
      if (connection) {
        void connection.stop();
      }
    };
  }, [accessToken, enabled, hubUrl]);

  return {
    ...state,
    connection: connectionInstance
  };
}
