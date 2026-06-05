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

type ConnectionKind = "connecting" | "connected" | "reconnecting" | "disconnected" | "error";

export type SessionOperationsConnectionState = {
  kind: ConnectionKind;
  detail: string;
  connection: HubConnection | null;
};

type ConnectionOptions = {
  accessToken: string;
  hubUrl: string;
  enabled?: boolean;
  onResync?: () => void;
  onLiveSessionStateChanged?: (stateChangedEvent: LiveSessionStateChangedEvent) => void;
};

function createState(
  kind: ConnectionKind,
  detail: string,
  connection: HubConnection | null
): SessionOperationsConnectionState {
  return {
    kind,
    detail,
    connection
  };
}

export function useSessionOperationsConnection({
  accessToken,
  hubUrl,
  enabled = true,
  onResync,
  onLiveSessionStateChanged
}: ConnectionOptions) {
  const [state, setState] = useState<SessionOperationsConnectionState>(
    createState("connecting", "Opening participant stream.", null)
  );
  const shouldConnect =
    enabled && accessToken.trim().length > 0 && hubUrl.trim().length > 0;
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
      setState(
        createState("disconnected", "SignalR waiting for authenticated session.", null)
      );
      return;
    }

    async function startConnection() {
      setState(createState("connecting", "Opening participant stream.", null));

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

        setState(
          createState(
            "reconnecting",
            "Connection dropped. Waiting for SignalR reconnect.",
            connection
          )
        );
      });

      connection.onreconnected(() => {
        if (!active) {
          return;
        }

        setState(
          createState("connected", "Realtime participant stream restored.", connection)
        );
        resyncRef.current?.();
      });

      connection.onclose((error) => {
        if (!active) {
          return;
        }

        setState(
          createState(
            error ? "error" : "disconnected",
            error ? `SignalR closed: ${error.message}` : "SignalR closed before session resumed.",
            null
          )
        );
      });

      try {
        await connection.start();
        if (!active) {
          await connection.stop();
          return;
        }

        setState(
          createState("connected", "Realtime participant stream connected.", connection)
        );
      } catch (error) {
        if (!active) {
          return;
        }

        setState(
          createState(
            "error",
            error instanceof Error ? error.message : "SignalR startup failed.",
            null
          )
        );
      }
    }

    void startConnection();

    return () => {
      active = false;
      if (connection) {
        void connection.stop();
      }
    };
  }, [accessToken, enabled, hubUrl, shouldConnect]);

  return shouldConnect
    ? state
    : createState("disconnected", "SignalR waiting for authenticated session.", null);
}
