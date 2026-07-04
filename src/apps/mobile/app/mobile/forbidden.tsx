import { Redirect, useRouter } from "expo-router";
import { StyleSheet, Text, View } from "react-native";
import { GameButton } from "../../src/components/game-button";
import { Mascot } from "../../src/components/mascot";
import { ScreenShell, shellStyles } from "../../src/components/screen-shell";
import { colors } from "../../src/theme/tokens";
import { useSession } from "../../src/providers/session-provider";

export default function ForbiddenPage() {
  const router = useRouter();
  const { loading, session, signOut } = useSession();

  if (!loading && !session) {
    return <Redirect href="/mobile/login" />;
  }

  return (
    <ScreenShell
      eyebrow="Cuenta no compatible"
      title="Esta app es solo para jugadores"
      description="Tu cuenta es válida, pero el juego móvil es solo para participantes. Las cuentas de admin u operador se usan en la consola web."
    >
      <View style={styles.mascotRow}>
        <Mascot mood="sad" size={104} />
      </View>

      <GameButton
        label="Cerrar sesión"
        variant="secondary"
        icon="↩"
        onPress={() => {
          void signOut().then(() => router.replace("/mobile/login"));
        }}
      />
    </ScreenShell>
  );
}

const styles = StyleSheet.create({
  mascotRow: {
    alignItems: "center",
    gap: 8
  }
});
