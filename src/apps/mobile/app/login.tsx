import { Redirect, useRouter } from "expo-router";
import { useState } from "react";
import { Pressable, StyleSheet, Text, TextInput, View } from "react-native";
import { ScreenShell, shellStyles } from "../src/components/screen-shell";
import { useSession } from "../src/providers/session-provider";

export default function LoginPage() {
  const router = useRouter();
  const { loading, session, signIn } = useSession();
  const [username, setUsername] = useState("participant");
  const [password, setPassword] = useState("participant123!");
  const [submitting, setSubmitting] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  if (!loading && session?.roles.includes("Participant")) {
    return <Redirect href="/home" />;
  }

  async function handleLogin() {
    setSubmitting(true);
    setMessage(null);

    try {
      await signIn(username, password);
      router.replace("/home");
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Login failed.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <ScreenShell
      eyebrow="UMBRAL mobile shell"
      title="Friendly auth. Guided routes. Realtime transport."
      description="Participant enters through Keycloak, then the shell proves protected mobile routes, JWT-backed requests, and SignalR reconnect behavior before any gameplay feature exists."
    >
      <View style={shellStyles.card}>
        <Text style={shellStyles.cardTitle}>Participant sign in</Text>
        <Text style={shellStyles.cardText}>
          Use the local seed account or try a non-participant role to verify rejection.
        </Text>
        <TextInput
          autoCapitalize="none"
          onChangeText={setUsername}
          placeholder="Username"
          style={styles.input}
          value={username}
        />
        <TextInput
          autoCapitalize="none"
          onChangeText={setPassword}
          placeholder="Password"
          secureTextEntry
          style={styles.input}
          value={password}
        />
        {message ? <Text style={styles.error}>{message}</Text> : null}
        <Pressable
          disabled={submitting}
          onPress={() => {
            void handleLogin();
          }}
          style={({ pressed }) => [
            styles.primaryButton,
            submitting && styles.buttonDisabled,
            pressed && styles.buttonPressed
          ]}
        >
          <Text style={styles.primaryButtonLabel}>
            {submitting ? "Signing in..." : "Enter participant shell"}
          </Text>
        </Pressable>
      </View>

      <View style={shellStyles.card}>
        <Text style={shellStyles.cardTitle}>Verification hints</Text>
        <View style={shellStyles.list}>
          <Text style={shellStyles.cardText}>participant / participant123! enters the shell.</Text>
          <Text style={shellStyles.cardText}>admin / admin123! is rejected by role.</Text>
          <Text style={shellStyles.cardText}>operator / operator123! is rejected by role.</Text>
        </View>
      </View>
    </ScreenShell>
  );
}

const styles = StyleSheet.create({
  input: {
    backgroundColor: "#ffffff",
    borderRadius: 16,
    borderWidth: 1,
    borderColor: "#eadcc8",
    color: "#17313b",
    fontSize: 16,
    paddingHorizontal: 16,
    paddingVertical: 14
  },
  primaryButton: {
    backgroundColor: "#1e6f8c",
    borderRadius: 18,
    paddingHorizontal: 16,
    paddingVertical: 16,
    alignItems: "center"
  },
  primaryButtonLabel: {
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
