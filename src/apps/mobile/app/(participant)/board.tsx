import { useState } from "react";
import { Text, View } from "react-native";
import { ScreenShell, shellStyles } from "../../src/components/screen-shell";
import { StatusChip } from "../../src/components/status-chip";
import { getClientConfig } from "../../src/lib/config";
import {
  type LiveSessionStateChangedEvent,
  useSessionOperationsConnection
} from "../../src/hooks/use-session-operations-connection";
import { useSession } from "../../src/providers/session-provider";

function resolveTone(state: string | null) {
  switch (state) {
    case "Active":
      return "success";
    case "Paused":
      return "warn";
    case "Canceled":
    case "Finalized":
      return "error";
    default:
      return "info";
  }
}

export default function BoardPage() {
  const { session } = useSession();
  const config = getClientConfig();
  const [lastStateChange, setLastStateChange] = useState<LiveSessionStateChangedEvent | null>(null);

  useSessionOperationsConnection({
    accessToken: session?.accessToken ?? "",
    hubUrl: config.sessionHubUrl,
    onLiveSessionStateChanged: setLastStateChange
  });

  const currentState = lastStateChange?.state ?? null;
  const actionBlocked = currentState !== "Active";

  return (
    <ScreenShell
      eyebrow="Participant Stage View"
      title={snapshot ? `${snapshot.teamName} board` : "Session Team board"}
      description="Live board consumes Session Operations snapshot and SignalR events without redefining backend contracts."
    >
      <View style={shellStyles.card}>
        <Text style={shellStyles.cardTitle}>Current stage placeholder</Text>
        <StatusChip
          label={currentState ? `Session ${currentState}` : "Awaiting lifecycle event"}
          tone={resolveTone(currentState)}
        />
        <Text style={shellStyles.cardText}>Parent block: Ancient archive</Text>
        <Text style={shellStyles.cardText}>Current playable stage: Decode the seal</Text>
        <Text style={shellStyles.cardText}>
          Primary action stays blocked unless Session State is Active.
        </Text>
        <Text style={shellStyles.cardText}>
          {actionBlocked
            ? "Evidence CTA disabled by lifecycle guard."
            : "Evidence CTA can unlock when Session Progression arrives."}
        </Text>
      </View>
    </ScreenShell>
  );
}

const styles = StyleSheet.create({
  primaryButton: {
    backgroundColor: "#2d6a4f",
    borderRadius: 18,
    alignItems: "center",
    paddingHorizontal: 16,
    paddingVertical: 14
  },
  primaryButtonLabel: {
    color: "#f7fbfc",
    fontSize: 15,
    fontWeight: "800"
  },
  disabledButton: {
    backgroundColor: "#9aa6a1"
  },
  inlineStatus: {
    alignItems: "center",
    flexDirection: "row",
    gap: 10
  },
  inlineStatusText: {
    color: "#17313b",
    fontSize: 14,
    fontWeight: "700"
  },
  feedbackSuccess: {
    backgroundColor: "#d8f3dc",
    borderColor: "#74c69d",
    borderRadius: 16,
    borderWidth: 1,
    padding: 12
  },
  feedbackError: {
    backgroundColor: "#ffe5e5",
    borderColor: "#ef9a9a",
    borderRadius: 16,
    borderWidth: 1,
    padding: 12
  },
  feedbackText: {
    color: "#17313b",
    fontSize: 14,
    fontWeight: "700",
    lineHeight: 20
  },
  warning: {
    color: "#9e6f00",
    fontSize: 14,
    lineHeight: 20
  },
  triviaInput: {
    backgroundColor: "#f7fbfc",
    borderColor: "#b8c8cc",
    borderRadius: 16,
    borderWidth: 1,
    color: "#17313b",
    fontSize: 16,
    minHeight: 48,
    paddingHorizontal: 14,
    paddingVertical: 12
  },
  scannerModal: {
    backgroundColor: "#000",
    flex: 1
  },
  cameraPreview: {
    flex: 1
  },
  scannerOverlay: {
    ...StyleSheet.absoluteFillObject,
    justifyContent: "space-between",
    padding: 24
  },
  scannerFrame: {
    alignSelf: "center",
    borderColor: "#f7fbfc",
    borderRadius: 24,
    borderWidth: 3,
    height: 260,
    marginTop: 110,
    width: 260
  },
  scannerInstructions: {
    backgroundColor: "rgba(23, 49, 59, 0.9)",
    borderRadius: 24,
    gap: 10,
    padding: 18
  },
  scannerTitle: {
    color: "#f7fbfc",
    fontSize: 22,
    fontWeight: "800"
  },
  scannerText: {
    color: "#dce8ea",
    fontSize: 14,
    lineHeight: 20
  },
  scannerCancelButton: {
    alignItems: "center",
    backgroundColor: "#f7fbfc",
    borderRadius: 16,
    paddingVertical: 12
  },
  scannerCancelLabel: {
    color: "#17313b",
    fontSize: 15,
    fontWeight: "800"
  },
  secondaryButton: {
    backgroundColor: "#17313b",
    borderRadius: 18,
    alignItems: "center",
    paddingHorizontal: 16,
    paddingVertical: 14
  },
  secondaryButtonLabel: {
    color: "#f7fbfc",
    fontSize: 15,
    fontWeight: "700"
  },
  buttonPressed: {
    opacity: 0.85
  },
  error: {
    color: "#9e2f2f",
    fontSize: 14,
    lineHeight: 20
  },
  clock: {
    color: "#17313b",
    fontSize: 48,
    fontWeight: "800",
    letterSpacing: 1.5
  },
  stageTitle: {
    color: "#17313b",
    fontSize: 24,
    fontWeight: "800",
    lineHeight: 30
  },
  hintItem: {
    backgroundColor: "#f6efe6",
    borderColor: "#eadcc8",
    borderRadius: 18,
    borderWidth: 1,
    gap: 10,
    padding: 14
  }
});
