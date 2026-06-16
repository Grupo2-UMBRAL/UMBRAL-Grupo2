import { Stack } from "expo-router";
import { StatusBar } from "expo-status-bar";
import { Buffer } from "buffer";
import { SessionProvider } from "../../src/providers/session-provider";

if (!globalThis.Buffer) {
  globalThis.Buffer = Buffer;
}

export default function RootLayout() {
  return (
    <SessionProvider>
      <StatusBar style="dark" />
      <Stack
        screenOptions={{
          headerShown: false,
          contentStyle: {
            backgroundColor: "#f6efe6"
          }
        }}
      >
        <Stack.Screen name="index" />
        <Stack.Screen name="login" />
        <Stack.Screen name="forbidden" />
        <Stack.Screen name="(participant)" />
      </Stack>
    </SessionProvider>
  );
}
