import { Redirect, useRouter } from "expo-router";
import { useState } from "react";
import { Pressable, StyleSheet, Text, TextInput, View } from "react-native";
import { GameButton } from "../../src/components/game-button";
import { Mascot } from "../../src/components/mascot";
import { ScreenShell, shellStyles } from "../../src/components/screen-shell";
import { colors } from "../../src/theme/tokens";
import { ApiClientError, registerParticipant } from "../../src/lib/api-client";
import { useSession } from "../../src/providers/session-provider";

function isValidEmail(value: string) {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value.trim());
}

export default function SignupPage() {
  const router = useRouter();
  const { loading, session, signIn } = useSession();
  const [username, setUsername] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  if (!loading && session?.roles.includes("Participant")) {
    return <Redirect href="/mobile/home" />;
  }

  const canSubmit =
    username.trim().length >= 3 && isValidEmail(email) && password.length >= 8 && !submitting;

  async function handleSignup() {
    setMessage(null);

    if (username.trim().length < 3) {
      setMessage("El usuario debe tener al menos 3 caracteres.");
      return;
    }
    if (!isValidEmail(email)) {
      setMessage("Escribe un correo válido.");
      return;
    }
    if (password.length < 8) {
      setMessage("La contraseña debe tener al menos 8 caracteres.");
      return;
    }

    setSubmitting(true);

    try {
      await registerParticipant({ username, email, password });
      await signIn(username, password);
      router.replace("/mobile/home");
    } catch (error) {
      if (error instanceof ApiClientError && error.status === 409) {
        setMessage("Ese usuario o correo ya existe. Prueba con otro.");
      } else {
        setMessage(error instanceof Error ? error.message : "No pudimos crear tu cuenta.");
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <ScreenShell
      eyebrow="Crear cuenta"
      title="Únete a UMBRAL"
      description="Crea tu cuenta de jugador y entra directo a la acción con tu equipo."
    >
      <View style={styles.mascotRow}>
        <Mascot mood="happy" size={104} />
      </View>

      <View style={shellStyles.card}>
        <Text style={shellStyles.cardTitle}>Tus datos</Text>
        <TextInput
          autoCapitalize="none"
          autoCorrect={false}
          onChangeText={setUsername}
          placeholder="Usuario"
          placeholderTextColor={colors.text.mutedAlt}
          style={styles.input}
          value={username}
        />
        <TextInput
          autoCapitalize="none"
          autoCorrect={false}
          keyboardType="email-address"
          onChangeText={setEmail}
          placeholder="Correo"
          placeholderTextColor={colors.text.mutedAlt}
          style={styles.input}
          value={email}
        />
        <TextInput
          autoCapitalize="none"
          onChangeText={setPassword}
          placeholder="Contraseña (mín. 8 caracteres)"
          placeholderTextColor={colors.text.mutedAlt}
          secureTextEntry
          style={styles.input}
          value={password}
        />
        {message ? <Text style={styles.error}>{message}</Text> : null}
        <GameButton
          label={submitting ? "Creando cuenta..." : "Crear cuenta y jugar"}
          icon="✨"
          disabled={!canSubmit}
          loading={submitting}
          onPress={() => void handleSignup()}
        />

        <Pressable
          onPress={() => router.replace("/mobile/login")}
          style={({ pressed }) => [styles.linkRow, pressed && styles.pressed]}
        >
          <Text style={styles.linkText}>
            ¿Ya tienes cuenta? <Text style={styles.linkStrong}>Inicia sesión</Text>
          </Text>
        </Pressable>
      </View>
    </ScreenShell>
  );
}

const styles = StyleSheet.create({
  mascotRow: {
    alignItems: "center"
  },
  input: {
    backgroundColor: colors.surface.card,
    borderRadius: 16,
    borderWidth: 1,
    borderColor: colors.surface.cardBorder,
    color: colors.text.primary,
    fontSize: 16,
    paddingHorizontal: 16,
    paddingVertical: 14
  },
  error: {
    color: colors.state.error.text,
    fontSize: 14,
    lineHeight: 20
  },
  linkRow: {
    alignItems: "center",
    paddingVertical: 6
  },
  linkText: {
    color: colors.text.secondary,
    fontSize: 15
  },
  linkStrong: {
    color: colors.brand.secondary,
    fontWeight: "800"
  },
  pressed: {
    opacity: 0.85
  }
});
