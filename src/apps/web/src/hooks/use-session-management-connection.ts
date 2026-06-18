"use client";

import { useEffect, useEffectEvent, useState } from "react";
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
  onResync?: () => void;
  onLiveSessionStateChanged?: (stateChangedEvent: LiveSessionStateChangedEvent) => void;
};

export function useSessionManagementConnection({
  accessToken,
  hubUrl,
  onResync,
  onLiveSessionStateChanged
}: ConnectionOptions) {
  const waitingState: ConnectionState = {
    kind: "disconnected",
    detail: "SignalR esperando la sesión autenticada."
  };
  const [state, setState] = useState<ConnectionState>({
    kind: "connecting",
    detail: "Abriendo la transmisión de sesión."
  });
  const shouldConnect = accessToken.trim().length > 0 && hubUrl.trim().length > 0;
  const runResync = useEffectEvent(() => onResync?.());
  const notifyLiveSessionStateChanged = useEffectEvent((stateChangedEvent: LiveSessionStateChangedEvent) => {
    onLiveSessionStateChanged?.(stateChangedEvent);
  });

  useEffect(() => {
    let active = true;
    let connection: HubConnection | null = null;

    if (!shouldConnect) {
      return;
    }

    async function startConnection() {
      setState({
        kind: "connecting",
        detail: "Abriendo la transmisión de sesión."
      });

      connection = new HubConnectionBuilder()
        .withUrl(hubUrl, {
          accessTokenFactory: () => accessToken,
          skipNegotiation: true,
          transport: HttpTransportType.WebSockets
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000])
        .configureLogging(LogLevel.Warning)
        .build();

      connection.on("liveSessionStateChanged", (stateChangedEvent: LiveSessionStateChangedEvent) => {
        if (!active) {
          return;
        }

        notifyLiveSessionStateChanged(stateChangedEvent);
        runResync();
      });

      connection.onreconnecting(() => {
        if (!active) {
          return;
        }

        setState({
          kind: "reconnecting",
          detail: "Conexión perdida. Esperando reconexión de SignalR."
        });
      });

      connection.onreconnected(() => {
        if (!active) {
          return;
        }

        setState({
          kind: "connected",
          detail: "Transmisión de sesión en tiempo real restaurada."
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
            ? `SignalR cerrado: ${error.message}`
            : "SignalR cerrado antes de que se reanudara la sesión."
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
          detail: "Transmisión de sesión en tiempo real conectada."
        });
      } catch (error) {
        if (!active) {
          return;
        }

        setState({
          kind: "error",
          detail: error instanceof Error ? error.message : "Error al iniciar la conexión SignalR."
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
  }, [accessToken, hubUrl, shouldConnect]);

  return shouldConnect ? state : waitingState;
}
