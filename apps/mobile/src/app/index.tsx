import { router } from "expo-router";
import { ScrollView, Text, View } from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";
import { Brand, Button, Card, styles } from "../components/Ui";

export default function WelcomeScreen() {
  return (
    <SafeAreaView style={styles.page}>
      <ScrollView contentContainerStyle={styles.content}>
        <Brand />
        <View style={{ paddingVertical: 30, gap: 20 }}>
          <Text style={styles.eyebrow}>A little less coordinating</Text>
          <Text accessibilityRole="header" style={styles.title}>
            Room for{"\n"}everyone.
          </Text>
          <Text style={styles.body}>
            More time for people. Less time finding out who’s bringing the
            coffee. A simpler home for your community’s shared plans.
          </Text>
        </View>
        <Card>
          <Text style={styles.eyebrow}>Development demo</Text>
          <Text accessibilityRole="header" style={styles.heading}>
            Meet Cedar Grove.
          </Text>
          <Text style={styles.body}>
            Explore a fictional community as Alex Morgan, a volunteer on the
            welcome team.
          </Text>
          <Text style={styles.small}>
            This shared demo is not a real sign-in. The mobile app currently
            shows upcoming gatherings and your assignments. Respond to
            assignments in the web demo.
          </Text>
          <Button
            label="Explore the demo"
            onPress={() => router.push("/demo")}
          />
        </Card>
        <Text style={styles.small}>
          Actively in development · Built around showing up for one another.
        </Text>
      </ScrollView>
    </SafeAreaView>
  );
}
