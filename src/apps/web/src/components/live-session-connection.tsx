import { useEffect, useRef, useState } from "react";
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  HttpTransportType,
  LogLevel,
} from "@microsoft/signalr";
import { getClientConfig } from "@/lib/config";

type ConnectionState =
  | "connecting"
  | "connected"
  | "reconnecting"
  | "disconnected"
  | "error";

type LiveSessionConnectionProps = {
  accessToken: string;
  onResync?: () => void;
};

export function LiveSessionConnection({
  accessToken,
  onResync,
}: LiveSessionConnectionProps) {
  const [state, setState] = useState<ConnectionState>("connecting");
  const connectionRef = useRef<HubConnection | null>(null);

  useEffect(() => {
    const config = getClientConfig();

    const connection = new HubConnectionBuilder()
      .withUrl(config.sessionHubUrl, {
        accessTokenFactory: () => accessToken,
        transport: HttpTransportType.WebSockets,
        skipNegotiation: true,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .configureLogging(LogLevel.Warning)
      .build();

    connectionRef.current = connection;

    connection.onreconnecting(() => setState("reconnecting"));
    connection.onreconnected(() => {
      setState("connected");
      onResync?.();
    });
    connection.onclose(() => setState("disconnected"));

    connection
      .start()
      .then(() => setState("connected"))
      .catch(() => setState("error"));

    return () => {
      if (connection.state !== HubConnectionState.Disconnected) {
        void connection.stop();
      }
    };
  }, [accessToken, onResync]);

  const dotClass = () => {
    switch (state) {
      case "connected": return "status-dot status-dot-green";
      case "reconnecting":
      case "connecting": return "status-dot status-dot-amber status-dot-pulse";
      case "error": return "status-dot status-dot-red";
      default: return "status-dot status-dot-muted";
    }
  };

  const label = () => {
    switch (state) {
      case "connected": return "Conectado";
      case "reconnecting": return "Reconectando...";
      case "connecting": return "Conectando...";
      case "error": return "Error de conexión";
      default: return "Desconectado";
    }
  };

  return (
    <div className="connection-indicator">
      <span className={dotClass()} />
      <span>{label()}</span>
    </div>
  );
}
