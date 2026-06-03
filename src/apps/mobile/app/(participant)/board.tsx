import { Redirect } from "expo-router";
import { useEffect, useState } from "react";
import { Text, View } from "react-native";
import { LoadingScreen } from "../../src/components/loading-screen";
import { ScreenShell, shellStyles } from "../../src/components/screen-shell";
import { StatusChip } from "../../src/components/status-chip";
import {
  loadStoredEnrollment,
  type StoredEnrollment
} from "../../src/lib/session-storage";

export default function BoardPage() {
  const [loadingEnrollment, setLoadingEnrollment] = useState(true);
  const [storedEnrollment, setStoredEnrollment] = useState<StoredEnrollment | null>(null);

  useEffect(() => {
    let active = true;

    async function hydrateEnrollment() {
      const enrollment = await loadStoredEnrollment();
      if (!active) {
        return;
      }

      setStoredEnrollment(enrollment);
      setLoadingEnrollment(false);
    }

    void hydrateEnrollment();

    return () => {
      active = false;
    };
  }, []);

  if (loadingEnrollment) {
    return <LoadingScreen message="Checking Session Team enrollment..." />;
  }

  if (!storedEnrollment) {
    return <Redirect href="/join" />;
  }

  return (
    <ScreenShell
      eyebrow="Participant Stage View"
      title="Team board context ready."
      description="Enrollment context is stored locally so the participant can return to the Session Team board without entering the Join Code again."
    >
      <View style={shellStyles.card}>
        <Text style={shellStyles.cardTitle}>Session Team</Text>
        <StatusChip label="Enrollment restored" tone="success" />
        <Text style={shellStyles.cardText}>Join Code: {storedEnrollment.joinCode}</Text>
        <Text style={shellStyles.cardText}>Team: {storedEnrollment.teamName}</Text>
        <Text style={shellStyles.mono}>TeamId: {storedEnrollment.teamId}</Text>
      </View>

      <View style={shellStyles.card}>
        <Text style={shellStyles.cardTitle}>Current stage placeholder</Text>
        <StatusChip label="Awaiting snapshot contract" tone="warn" />
        <Text style={shellStyles.cardText}>Parent block: Ancient archive</Text>
        <Text style={shellStyles.cardText}>Current playable stage: Decode the seal</Text>
        <Text style={shellStyles.cardText}>
          Primary action will live here once Evidence Submission is available.
        </Text>
      </View>
    </ScreenShell>
  );
}
