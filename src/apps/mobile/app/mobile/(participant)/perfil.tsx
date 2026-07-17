import { useRouter } from "expo-router";
import { useCallback, useEffect, useState } from "react";
import { Alert, Linking, Platform, StyleSheet, Text, TextInput, View } from "react-native";
import { GameButton } from "../../../src/components/game-button";
import { LoadingScreen } from "../../../src/components/loading-screen";
import { ScreenShell, shellStyles } from "../../../src/components/screen-shell";
import { StatusChip } from "../../../src/components/status-chip";
import {
  ApiClientError,
  createParticipantAccountClient,
  type ParticipantProfile
} from "../../../src/lib/api-client";
import { buildAccountConsoleUrl } from "../../../src/lib/keycloak-account";
import { useSession } from "../../../src/providers/session-provider";
import { colors } from "../../../src/theme/tokens";

const deactivateWarning =
  "Tu cuenta se desactivará y no podrás volver a iniciar sesión. Desde la app no hay vuelta atrás: " +
  "tendrás que contactar a un administrador para recuperarla.\n\n" +
  "Tu equipo conserva sus puntos y evidencias.";

function readUsernameError(error: unknown) {
  if (!(error instanceof ApiClientError)) {
    return error instanceof Error ? error.message : "No pudimos cambiar tu usuario.";
  }

  switch (error.code) {
    case "participant_username_taken":
      return "Ese usuario ya está tomado. Prueba con otro.";
    case "participant_username_required":
      return "Escribe un usuario.";
    case "participant_username_too_short":
      return "El usuario debe tener al menos 3 caracteres.";
    case "participant_username_too_long":
      return "El usuario debe tener menos de 40 caracteres.";
    case "participant_username_invalid":
      return "Solo letras, números, punto, guion y guion bajo.";
    default:
      return error.status === 429
        ? "Demasiados cambios seguidos. Espera unos minutos."
        : "No pudimos cambiar tu usuario.";
  }
}

export default function ProfilePage() {
  const router = useRouter();
  const { session, signOut, renewSession } = useSession();
  const accessToken = session?.accessToken ?? "";

  const [profile, setProfile] = useState<ParticipantProfile | null>(null);
  const [username, setUsername] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [deactivating, setDeactivating] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const loadProfile = useCallback(async () => {
    try {
      const result = await createParticipantAccountClient(accessToken).getMyProfile();
      setProfile(result);
      setUsername(result.username);
      setError(null);
    } catch (loadError) {
      setError(
        loadError instanceof Error ? loadError.message : "No pudimos cargar tu perfil."
      );
    } finally {
      setLoading(false);
    }
  }, [accessToken]);

  useEffect(() => {
    void loadProfile();
  }, [loadProfile]);

  async function handleChangeUsername() {
    setError(null);
    setNotice(null);
    setSaving(true);

    try {
      const updated = await createParticipantAccountClient(accessToken).changeMyUsername(username);
      setProfile(updated);
      setUsername(updated.username);

      // The token carries preferred_username, so it now names someone who no longer exists. Renew it
      // here rather than waiting for the scheduled refresh, or home keeps greeting the old handle.
      // The rename is already committed: if re-minting fails, the change still stands, so this must
      // not surface as a failed rename.
      try {
        await renewSession();
      } catch {
        // The scheduled refresh will pick the new claims up.
      }

      setNotice(`Listo. Ahora inicias sesión como "${updated.username}".`);
    } catch (changeError) {
      setError(readUsernameError(changeError));
    } finally {
      setSaving(false);
    }
  }

  async function deactivateAccount() {
    setError(null);
    setDeactivating(true);

    try {
      await createParticipantAccountClient(accessToken).deactivateMyAccount();
      await signOut();
      router.replace("/mobile/login");
    } catch (deactivateError) {
      setError(
        deactivateError instanceof Error
          ? deactivateError.message
          : "No pudimos desactivar tu cuenta."
      );
      setDeactivating(false);
    }
  }

  function confirmDeactivate() {
    // Alert.alert has no effect on react-native-web, so the browser prompt stands in. Without this
    // the web build would deactivate on the first tap, with no confirmation at all.
    if (Platform.OS === "web") {
      // eslint-disable-next-line no-alert
      if (window.confirm(`${deactivateWarning}\n\n¿Desactivar tu cuenta?`)) {
        void deactivateAccount();
      }
      return;
    }

    Alert.alert("¿Desactivar tu cuenta?", deactivateWarning, [
      { text: "Cancelar", style: "cancel" },
      {
        text: "Desactivar",
        style: "destructive",
        onPress: () => void deactivateAccount()
      }
    ]);
  }

  if (loading) {
    return <LoadingScreen message="Cargando tu perfil..." />;
  }

  const usernameChanged = profile !== null && username.trim() !== profile.username;

  return (
    <ScreenShell
      eyebrow="Mi perfil"
      title="Tu cuenta UMBRAL"
      description="Revisa tus datos, cambia tu usuario o tu contraseña, y gestiona tu cuenta."
    >
      {error ? (
        <View style={shellStyles.card}>
          <StatusChip label="Algo salió mal" tone="error" />
          <Text style={shellStyles.cardText}>{error}</Text>
        </View>
      ) : null}

      <View style={shellStyles.card}>
        <Text style={shellStyles.cardTitle}>Tus datos</Text>
        <View style={styles.field}>
          <Text style={styles.fieldLabel}>Usuario</Text>
          <Text style={styles.fieldValue}>{profile?.username}</Text>
        </View>
        <View style={styles.field}>
          <Text style={styles.fieldLabel}>Correo</Text>
          <Text style={styles.fieldValue}>{profile?.email}</Text>
        </View>
      </View>

      <View style={shellStyles.card}>
        <Text style={shellStyles.cardTitle}>Cambiar usuario</Text>
        <Text style={shellStyles.cardText}>
          Es el nombre que ven tus compañeros y con el que inicias sesión. Si lo cambias, entra con el
          nuevo la próxima vez.
        </Text>
        <TextInput
          autoCapitalize="none"
          autoCorrect={false}
          onChangeText={setUsername}
          placeholder="Usuario"
          placeholderTextColor={colors.text.mutedAlt}
          style={styles.input}
          value={username}
        />
        {notice ? <Text style={styles.notice}>{notice}</Text> : null}
        <GameButton
          label={saving ? "Guardando..." : "Guardar usuario"}
          icon="✏️"
          disabled={!usernameChanged || saving}
          loading={saving}
          onPress={() => void handleChangeUsername()}
        />
      </View>

      <View style={shellStyles.card}>
        <Text style={shellStyles.cardTitle}>Cambiar contraseña</Text>
        <Text style={shellStyles.cardText}>
          Tu contraseña se cambia en la página de tu cuenta. Se abrirá en el navegador y te pediremos
          iniciar sesión de nuevo ahí.
        </Text>
        <GameButton
          label="Abrir en el navegador"
          icon="🔑"
          variant="ghost"
          onPress={() => void Linking.openURL(buildAccountConsoleUrl())}
        />
      </View>

      <View style={shellStyles.card}>
        <Text style={shellStyles.cardTitle}>Desactivar mi cuenta</Text>
        <Text style={shellStyles.cardText}>
          Dejarás de poder iniciar sesión. Desde la app no hay vuelta atrás: para recuperarla tendrás
          que contactar a un administrador.
        </Text>
        <GameButton
          label={deactivating ? "Desactivando..." : "Desactivar mi cuenta"}
          icon="⚠️"
          variant="ghost"
          disabled={deactivating}
          loading={deactivating}
          onPress={confirmDeactivate}
        />
      </View>
    </ScreenShell>
  );
}

const styles = StyleSheet.create({
  field: {
    gap: 4
  },
  fieldLabel: {
    color: colors.text.mutedAlt,
    fontSize: 12,
    fontWeight: "700",
    letterSpacing: 0.6,
    textTransform: "uppercase"
  },
  fieldValue: {
    color: colors.text.primary,
    fontSize: 17,
    fontWeight: "700"
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
  notice: {
    color: colors.state.success.text,
    fontSize: 14,
    lineHeight: 20
  }
});
