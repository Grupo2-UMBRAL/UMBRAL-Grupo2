import { useState } from "react";
import { Pressable, StyleSheet, Text, TextInput, View } from "react-native";
import { ScreenShell, shellStyles } from "../../src/components/screen-shell";
import { createAuthorizedApiClient } from "../../src/lib/api-client";
import { useSession } from "../../src/providers/session-provider";

export default function JoinPage() {
  const { session } = useSession();
  const [joinCode, setJoinCode] = useState("LIVE-2026");
  const [teamName, setTeamName] = useState("Shadow Foxes");
  const [payloadPreview, setPayloadPreview] = useState<string | null>(null);

  if (!session) {
    return null;
  }

  const apiClient = createAuthorizedApiClient(session.accessToken);

  return (
    <ScreenShell
      eyebrow="Session Enrollment placeholder"
      title="Prepare a participant join flow without binding it to backend rules yet."
      description="This screen reserves the linear, single-primary-action shape for join code entry and Session Team selection."
    >
      <View style={shellStyles.card}>
        <Text style={shellStyles.cardTitle}>Join request draft</Text>
        <TextInput
          autoCapitalize="characters"
          onChangeText={setJoinCode}
          placeholder="Session Join Code"
          style={styles.input}
          value={joinCode}
        />
        <TextInput
          onChangeText={setTeamName}
          placeholder="Requested Session Team"
          style={styles.input}
          value={teamName}
        />
        <Pressable
          onPress={() => {
            setPayloadPreview(
              JSON.stringify(
                {
                  joinCode,
                  requestedSessionTeamName: teamName,
                  requestHeaders: {
                    authorization: apiClient.authorizationHeaderPreview
                  }
                },
                null,
                2
              )
            );
          }}
          style={({ pressed }) => [styles.button, pressed && styles.buttonPressed]}
        >
          <Text style={styles.buttonLabel}>Preview enrollment payload</Text>
        </Pressable>
        {payloadPreview ? <Text style={shellStyles.mono}>{payloadPreview}</Text> : null}
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
  button: {
    backgroundColor: "#1e6f8c",
    borderRadius: 18,
    alignItems: "center",
    paddingHorizontal: 16,
    paddingVertical: 14
  },
  buttonPressed: {
    opacity: 0.85
  },
  buttonLabel: {
    color: "#f7fbfc",
    fontSize: 15,
    fontWeight: "700"
  }
});
