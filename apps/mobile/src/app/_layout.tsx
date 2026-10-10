import { Stack } from "expo-router";
import { StatusBar } from "expo-status-bar";
import { colors } from "../components/Ui";

export default function RootLayout() {
  return (
    <>
      <StatusBar style="dark" />
      <Stack
        screenOptions={{
          headerStyle: { backgroundColor: colors.background },
          headerTintColor: colors.ink,
          contentStyle: { backgroundColor: colors.background },
          headerShadowVisible: false,
        }}
      >
        <Stack.Screen name="index" options={{ headerShown: false }} />
        <Stack.Screen
          name="demo"
          options={{ title: "Your community", headerBackTitle: "Home" }}
        />
        <Stack.Screen
          name="events/[id]"
          options={{ title: "Event details", headerBackTitle: "Community" }}
        />
        <Stack.Screen
          name="availability"
          options={{
            title: "Your unavailable time",
            headerBackTitle: "Community",
          }}
        />
      </Stack>
    </>
  );
}
