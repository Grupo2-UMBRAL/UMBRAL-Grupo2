import { Text, View } from "react-native";
import { ScreenShell, shellStyles } from "../../src/components/screen-shell";
import { StatusChip } from "../../src/components/status-chip";

const rankingPreview = [
  { team: "Shadow Foxes", score: "420", state: "You" },
  { team: "Cipher Owls", score: "415", state: "Ahead by time" },
  { team: "Signal Wolves", score: "390", state: "Chasing" }
];

export default function RankingPage() {
  return (
    <ScreenShell
      eyebrow="Ranking placeholder"
      title="Reserve a participant-friendly ranking surface."
      description="Scoreboard and tie-break logic will arrive later. The shell only keeps the route, hierarchy and tone ready."
    >
      <View style={shellStyles.section}>
        {rankingPreview.map((entry, index) => (
          <View key={entry.team} style={shellStyles.card}>
            <StatusChip label={`#${index + 1} ${entry.state}`} tone={index === 0 ? "success" : "info"} />
            <Text style={shellStyles.cardTitle}>{entry.team}</Text>
            <Text style={shellStyles.cardText}>{entry.score} pts</Text>
          </View>
        ))}
      </View>
    </ScreenShell>
  );
}
