"use client";

import { useEffect, useEffectEvent, useState } from "react";
import {
  HubConnectionBuilder,
  HttpTransportType,
  LogLevel,
  type HubConnection
} from "@microsoft/signalr";

type ConnectionState =
  | { kind: "connecting"; detail: string }
  | { kind: "connected"; detail: string }
  | { kind: "reconnecting"; detail: string }
  | { kind: "disconnected"; detail: string }
  | { kind: "error"; detail: string };

type ConnectionOptions = {
  accessToken: string;
  hubUrl: string;
  onResync?: () => void;
};

export function useSessionOperationsConnection({
  accessToken,
  hubUrl,
  onResync
}: ConnectionOptions) {
  const [state, setState] = useState<ConnectionState>({
    kind: "connecting",
    detail: "Opening session stream."
  });
  const runResync = useEffectEvent(() => onResync?.());

  useEffect(() => {
    let active = true;
    let connection: HubConnection | null = null;

    async function startConnection() {
      setState({
        kind: "connecting",
        detail: "Opening session stream."
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
          detail: "Realtime session stream restored."
        });
        runResync();
      });

      connection.onclose((error) => {
        if (!active) {
          return;
        }

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
          detail: "Realtime session stream connected."
        });
      } catch (error) {
        if (!active) {
          return;
        }

        setState({
          kind: "error",
          detail: error instanceof Error ? error.message : "SignalR startup failed."
        });
      }
    }

    void startConnection();

    return () => {
      active = false;
      if (connection) {
        void connection.stop();
      }
    };
  }, [accessToken, hubUrl]);

  return state;
}
