import { Text, View } from "react-native";
import { ScreenShell, shellStyles } from "../../src/components/screen-shell";
import { StatusChip } from "../../src/components/status-chip";

const progressPreview = [
  { title: "Join session", tone: "success" as const, detail: "Authenticated and ready to enroll." },
  { title: "Current stage", tone: "info" as const, detail: "The active challenge gets the primary focus." },
  { title: "Next stage", tone: "neutral" as const, detail: "Upcoming challenge stays visible but not noisy." },
  { title: "Revealed solution", tone: "warn" as const, detail: "Shown only once the session ends or the stage resolves." }
];

export default function ProgressPage() {
  return (
    <ScreenShell
      eyebrow="Guided progression"
      title="Keep participant movement linear and legible."
      description="The shell reserves a warm, path-first progression view inspired by learning flows, not by dense menus."
    >
      <View style={shellStyles.section}>
        {progressPreview.map((item) => (
          <View key={item.title} style={shellStyles.card}>
            <StatusChip label={item.title} tone={item.tone} />
            <Text style={shellStyles.cardText}>{item.detail}</Text>
          </View>
        ))}
      </View>
    </ScreenShell>
  );
}
