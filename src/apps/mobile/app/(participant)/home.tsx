import { useRouter } from "expo-router";
import { useState } from "react";
import { Pressable, StyleSheet, Text, View } from "react-native";
import { RouteCard } from "../../src/components/route-card";
import { ScreenShell, shellStyles } from "../../src/components/screen-shell";
import { StatusChip } from "../../src/components/status-chip";
import { createAuthorizedApiClient, type ApiProbeResult } from "../../src/lib/api-client";
import { getClientConfig } from "../../src/lib/config";
import { useSessionOperationsConnection } from "../../src/hooks/use-session-operations-connection";
import { useSession } from "../../src/providers/session-provider";

function resolveConnectionTone(kind: string) {
  switch (kind) {
    case "connected":
      return "success";
    case "reconnecting":
      return "warn";
    case "error":
      return "error";
    default:
      return "info";
  }
}

export default function HomePage() {
  const router = useRouter();
  const { session, signOut } = useSession();
  const [resyncCount, setResyncCount] = useState(0);
  const [probePending, setProbePending] = useState(false);
  const [probeResult, setProbeResult] = useState<ApiProbeResult | null>(null);
  const [probeError, setProbeError] = useState<string | null>(null);

  if (!session) {
    return null;
  }

  const config = getClientConfig();
  const apiClient = createAuthorizedApiClient(session.accessToken);
  const connectionState = useSessionOperationsConnection({
    accessToken: session.accessToken,
    hubUrl: config.sessionHubUrl,
    onResync: () => setResyncCount((current) => current + 1)
  });

  async function handleProbe() {
    setProbePending(true);
    setProbeError(null);

    try {
      const result = await apiClient.getHealth();
      setProbeResult(result);
    } catch (error) {
      setProbeResult(null);
      setProbeError(error instanceof Error ? error.message : "Edge proxy probe failed.");
    } finally {
      setProbePending(false);
    }
  }

  return (
    <ScreenShell
      eyebrow={`Signed in as ${session.displayName}`}
      title="Participant shell ready for the next game slices."
      description="The shell already proves route protection, JWT-backed transport and realtime reconnect state before Session Enrollment or the live board exist."
    >
      <View style={shellStyles.card}>
        <Text style={shellStyles.cardTitle}>Connection state</Text>
        <View style={shellStyles.row}>
          <StatusChip
            label={connectionState.kind}
            tone={resolveConnectionTone(connectionState.kind)}
          />
          <StatusChip label={`Resync ${resyncCount}`} tone="info" />
        </View>
        <Text style={shellStyles.cardText}>{connectionState.detail}</Text>
      </View>

      <View style={shellStyles.card}>
        <Text style={shellStyles.cardTitle}>API probe</Text>
        <Text style={shellStyles.cardText}>
          Calls `GET /health` through the edge proxy with the current participant `JWT`.
        </Text>
        <Pressable
          onPress={() => {
            void handleProbe();
          }}
          style={({ pressed }) => [
            styles.primaryButton,
            probePending && styles.buttonDisabled,
            pressed && styles.buttonPressed
          ]}
        >
          <Text style={styles.primaryButtonLabel}>
            {probePending ? "Probing edge proxy..." : "Probe edge proxy"}
          </Text>
        </Pressable>
        <Text style={shellStyles.mono}>Authorization: {apiClient.authorizationHeaderPreview}</Text>
        {probeResult ? (
          <Text style={shellStyles.cardText}>
            {probeResult.status} from {probeResult.url}
            {"\n"}
            {probeResult.body}
          </Text>
        ) : null}
        {probeError ? <Text style={styles.error}>{probeError}</Text> : null}
      </View>

      <View style={shellStyles.section}>
        <Text style={shellStyles.cardTitle}>Guided navigation</Text>
        <RouteCard
          title="Join session"
          description="Prepare Session Enrollment request shape for join code and Session Team choice."
          onPress={() => router.push("/join")}
        />
        <RouteCard
          title="Team board"
          description="Reserve the participant surface for current stage, team state and evidence CTA."
          onPress={() => router.push("/board")}
        />
        <RouteCard
          title="Progress path"
          description="Keep room for a Duolingo-like guided progression without exposing the whole mission tree."
          onPress={() => router.push("/progress")}
        />
        <RouteCard
          title="Ranking"
          description="Reserve a participant ranking view once Scoreboard is available."
          onPress={() => router.push("/ranking")}
        />
        <RouteCard
          title="Hints and solutions"
          description="Leave a dedicated surface for Hint Release and final revealed resolutions."
          onPress={() => router.push("/resolutions")}
        />
      </View>

      <View style={shellStyles.card}>
        <Text style={shellStyles.cardTitle}>Active configuration</Text>
        <Text style={shellStyles.cardText}>Edge proxy: {config.edgeProxyPublicBaseUrl}</Text>
        <Text style={shellStyles.cardText}>Keycloak: {config.keycloakPublicBaseUrl}</Text>
        <Text style={shellStyles.cardText}>Session hub: {config.sessionHubUrl}</Text>
      </View>

      <Pressable
        onPress={() => {
          void signOut().then(() => router.replace("/login"));
        }}
        style={({ pressed }) => [styles.secondaryButton, pressed && styles.buttonPressed]}
      >
        <Text style={styles.secondaryButtonLabel}>Sign out</Text>
      </Pressable>
    </ScreenShell>
  );
}

const styles = StyleSheet.create({
  primaryButton: {
    backgroundColor: "#1e6f8c",
    borderRadius: 18,
    alignItems: "center",
    paddingHorizontal: 16,
    paddingVertical: 14
  },
  secondaryButton: {
    backgroundColor: "#17313b",
    borderRadius: 18,
    alignItems: "center",
    paddingHorizontal: 16,
    paddingVertical: 14
  },
  primaryButtonLabel: {
    color: "#f7fbfc",
    fontSize: 15,
    fontWeight: "700"
  },
  secondaryButtonLabel: {
    color: "#f7fbfc",
    fontSize: 15,
    fontWeight: "700"
  },
  buttonDisabled: {
    opacity: 0.7
  },
  buttonPressed: {
    opacity: 0.85
  },
  error: {
    color: "#9e2f2f",
    fontSize: 14,
    lineHeight: 20
  }
});
