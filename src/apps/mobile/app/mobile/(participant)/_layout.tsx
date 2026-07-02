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
          backgroundColor: "#F7F7F7"
        },
        headerTitleStyle: {
          color: "#4B4B4B",
          fontWeight: "700"
        },
        headerShadowVisible: false,
        headerTintColor: "#4B4B4B",
        contentStyle: {
          backgroundColor: "#F7F7F7"
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
