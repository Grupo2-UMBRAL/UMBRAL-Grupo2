import { useEffect, useRef, useState } from "react";
import {
  HubConnectionBuilder,
  HttpTransportType,
  LogLevel,
  type HubConnection
} from "@microsoft/signalr";

export type LiveSessionStateChangedEvent = {
  liveSessionId: string;
  previousState: string;
  state: string;
  registeredSessionTeamCount: number;
  occurredAtUtc: string;
};

type ConnectionState =
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
  onLiveSessionStateChanged?: (stateChangedEvent: LiveSessionStateChangedEvent) => void;
};

export function useSessionOperationsConnection({
  accessToken,
  hubUrl,
  onResync,
  onLiveSessionStateChanged
}: ConnectionOptions) {
  const waitingState: ConnectionState = {
    kind: "disconnected",
    detail: "SignalR waiting for authenticated session."
  };
  const [state, setState] = useState<ConnectionState>({
    kind: "connecting",
    detail: "Opening participant stream."
  });
  const shouldConnect = accessToken.trim().length > 0 && hubUrl.trim().length > 0;
  const resyncRef = useRef(onResync);
  const liveSessionStateChangedRef = useRef(onLiveSessionStateChanged);

  useEffect(() => {
    resyncRef.current = onResync;
  }, [onResync]);

  useEffect(() => {
    liveSessionStateChangedRef.current = onLiveSessionStateChanged;
  }, [onLiveSessionStateChanged]);

  useEffect(() => {
    let active = true;
    let connection: HubConnection | null = null;

    if (!shouldConnect) {
      return;
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

      connection.on("liveSessionStateChanged", (stateChangedEvent: LiveSessionStateChangedEvent) => {
        if (!active) {
          return;
        }

        liveSessionStateChangedRef.current?.(stateChangedEvent);
        resyncRef.current?.();
      });

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
  }, [accessToken, hubUrl, shouldConnect]);

  return shouldConnect ? state : waitingState;
}
