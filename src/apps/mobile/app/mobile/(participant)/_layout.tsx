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
      <Stack.Screen name="home" options={{ title: "Participant shell" }} />
      <Stack.Screen name="join" options={{ title: "Join session" }} />
      <Stack.Screen name="board" options={{ title: "Team board" }} />
      <Stack.Screen name="progress" options={{ title: "Progress path" }} />
      <Stack.Screen name="ranking" options={{ title: "Ranking" }} />
      <Stack.Screen name="resolutions" options={{ title: "Hints and solutions" }} />
    </Stack>
  );
}
