import { Redirect } from "expo-router";
import { useEffect, useMemo, useState } from "react";
import { Text, View } from "react-native";
import { LoadingScreen } from "../../../src/components/loading-screen";
import { ScreenShell, shellStyles } from "../../../src/components/screen-shell";
import { StatusChip } from "../../../src/components/status-chip";
import { createAuthorizedApiClient, type SessionTeamSnapshot } from "../../../src/lib/api-client";
import {
  loadStoredEnrollment,
  type StoredEnrollment
} from "../../../src/lib/session-storage";
import { useSession } from "../../../src/providers/session-provider";

function countHintsWithCoordinates(snapshot: SessionTeamSnapshot | null) {
  if (!snapshot) {
    return 0;
  }

  return snapshot.visibleHints.filter(
    (hint) => typeof hint.latitude === "number" && typeof hint.longitude === "number"
  ).length;
}

export default function ProgressPage() {
  const { session } = useSession();
  const apiClient = useMemo(
    () => (session ? createAuthorizedApiClient(session.accessToken) : null),
    [session]
  );
  const [loadingEnrollment, setLoadingEnrollment] = useState(true);
  const [storedEnrollment, setStoredEnrollment] = useState<StoredEnrollment | null>(null);
  const [snapshot, setSnapshot] = useState<SessionTeamSnapshot | null>(null);

  useEffect(() => {
    let active = true;

    async function hydrate() {
      const enrollment = await loadStoredEnrollment();
      if (!active) {
        return;
      }

      setStoredEnrollment(enrollment);
      setLoadingEnrollment(false);

      if (apiClient && enrollment) {
        const nextSnapshot = await apiClient.getSessionTeamSnapshot(enrollment.teamId);
        if (active) {
          setSnapshot(nextSnapshot);
        }
      }
    }

    void hydrate();

    return () => {
      active = false;
    };
  }, [apiClient]);

  if (!session || loadingEnrollment) {
    return <LoadingScreen message="Loading participant progression..." />;
  }

  if (!storedEnrollment) {
    return <Redirect href="/mobile/join" />;
  }

  const completedStages = snapshot?.currentStage
    ? Math.max(0, snapshot.currentStage.sessionStageOrder - 1)
    : 0;

  return (
    <ScreenShell
      eyebrow="Guided progression"
      title="Keep participant movement linear and legible."
      description="Progression stays focused on completed stages, current playable stage and what Hint Release already unlocked."
    >
      <View style={shellStyles.section}>
        <View style={shellStyles.card}>
          <StatusChip label={`${completedStages} completed`} tone="success" />
          <Text style={shellStyles.cardText}>
            Completed stages are derived from the current Session Stage order instead of exposing the full Mission tree.
          </Text>
        </View>
        <View style={shellStyles.card}>
          <StatusChip
            label={snapshot?.currentStage ? snapshot.currentStage.name : "Waiting for current stage"}
            tone="info"
          />
          <Text style={shellStyles.cardText}>
            {snapshot?.currentStage
              ? `Current stage ${snapshot.currentStage.sessionStageOrder} uses ${snapshot.currentStage.gameType} and keeps the Prompt in the live board.`
              : "No active stage is available for this Session Team yet."}
          </Text>
        </View>
        <View style={shellStyles.card}>
          <StatusChip label={`${countHintsWithCoordinates(snapshot)} map-ready hints`} tone="warn" />
          <Text style={shellStyles.cardText}>
            Hints with coordinates render a static map. Hints without coordinates stay text-only to avoid broken map UI.
          </Text>
        </View>
      </View>
    </ScreenShell>
  );
}
