import { Text, View } from "react-native";
import { ScreenShell, shellStyles } from "../../src/components/screen-shell";
import { StatusChip } from "../../src/components/status-chip";

export default function BoardPage() {
  return (
    <ScreenShell
      eyebrow="Participant Stage View"
      title="Reserve the live board without exposing the full mission design tree."
      description="The future board will show the current playable stage plus parent block context, not the complete Mission Nodes hierarchy."
    >
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
