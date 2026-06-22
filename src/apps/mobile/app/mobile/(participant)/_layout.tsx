import { Redirect, Stack } from "expo-router";
import { LoadingScreen } from "../../../src/components/loading-screen";
import { useSession } from "../../../src/providers/session-provider";

export default function ParticipantLayout() {
  const { loading, session } = useSession();

  if (loading) {
    return <LoadingScreen message="Checking participant route guard..." />;
  }

  if (!session) {
    return <Redirect href="/mobile/login" />;
  }

  if (!session.roles.includes("Participant")) {
    return <Redirect href="/mobile/forbidden" />;
  }

  return (
    <Stack
      screenOptions={{
        headerStyle: {
          backgroundColor: "#f6efe6"
        },
        headerTitleStyle: {
          color: "#17313b",
          fontWeight: "700"
        },
        headerShadowVisible: false,
        headerTintColor: "#17313b",
        contentStyle: {
          backgroundColor: "#f6efe6"
        }
      }}
    >
      <Stack.Screen name="home" options={{ title: "Inicio" }} />
      <Stack.Screen name="join" options={{ title: "Unirse" }} />
      <Stack.Screen name="board" options={{ title: "Tablero" }} />
      <Stack.Screen name="progress" options={{ title: "Progreso" }} />
      <Stack.Screen name="ranking" options={{ title: "Ranking" }} />
      <Stack.Screen name="resolutions" options={{ title: "Pistas" }} />
    </Stack>
  );
}
