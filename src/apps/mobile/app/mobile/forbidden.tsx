import { Redirect, useRouter } from "expo-router";
import { Pressable, StyleSheet, Text, View } from "react-native";
import { ScreenShell, shellStyles } from "../../src/components/screen-shell";
import { useSession } from "../../src/providers/session-provider";

export default function ForbiddenPage() {
  const router = useRouter();
  const { loading, session, signOut } = useSession();

  if (!loading && !session) {
    return <Redirect href="/mobile/login" />;
  }

  return (
    <ScreenShell
      eyebrow="Role rejected"
      title="This shell only allows Participant."
      description="Identity is valid, but the mobile shell does not expose Administrator or Operator routes."
    >
      <View style={shellStyles.card}>
        <Text style={shellStyles.cardTitle}>Current role set</Text>
        <Text style={shellStyles.cardText}>{session?.roles.join(", ") ?? "No active session"}</Text>
      </View>

      <Pressable
        onPress={() => {
          void signOut().then(() => router.replace("/mobile/login"));
        }}
        style={({ pressed }) => [styles.button, pressed && styles.buttonPressed]}
      >
        <Text style={styles.buttonLabel}>Sign out and return to login</Text>
      </Pressable>
    </ScreenShell>
  );
}

const styles = StyleSheet.create({
  button: {
    backgroundColor: "#EA2B2B",
    borderRadius: 18,
    alignItems: "center",
    paddingHorizontal: 16,
    paddingVertical: 16
  },
  buttonPressed: {
    opacity: 0.85
  },
  buttonLabel: {
    color: "#FFFFFF",
    fontSize: 15,
    fontWeight: "700"
  }
});
