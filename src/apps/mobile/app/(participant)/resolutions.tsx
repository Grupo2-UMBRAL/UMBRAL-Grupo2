import { Text, View } from "react-native";
import { ScreenShell, shellStyles } from "../../src/components/screen-shell";
import { StatusChip } from "../../src/components/status-chip";

export default function ResolutionsPage() {
  return (
    <ScreenShell
      eyebrow="Hints and solutions"
      title="Keep a dedicated route for Hint Release and final revealed answers."
      description="The shell separates operational hints from live board pressure so the participant flow stays readable."
    >
      <View style={shellStyles.card}>
        <StatusChip label="Hint Release placeholder" tone="warn" />
        <Text style={shellStyles.cardText}>
          Future hints will appear here with release time and relevance to the current stage.
        </Text>
      </View>
      <View style={shellStyles.card}>
        <StatusChip label="Revealed solution placeholder" tone="neutral" />
        <Text style={shellStyles.cardText}>
          Final resolutions stay isolated from active play and only surface when the session rules allow it.
        </Text>
      </View>
    </ScreenShell>
  );
}
