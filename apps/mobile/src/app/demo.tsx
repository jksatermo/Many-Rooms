import { useCallback, useEffect, useState } from "react";
import {
  ActivityIndicator,
  RefreshControl,
  ScrollView,
  Text,
  View,
} from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";
import { Brand, Button, Card, colors, styles } from "../components/Ui";
import type { Dashboard } from "../lib/api";
import { api } from "../lib/config";

export default function DemoScreen() {
  const [dashboard, setDashboard] = useState<Dashboard | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [attempt, setAttempt] = useState(0);
  const refresh = useCallback(() => setAttempt((value) => value + 1), []);
  useEffect(() => {
    const controller = new AbortController();
    async function load() {
      setLoading(true);
      setError(null);
      try {
        const context = await api.context(controller.signal);
        const result = await api.dashboard(
          context.organizationId,
          controller.signal,
        );
        if (!controller.signal.aborted) setDashboard(result);
      } catch (failure) {
        if (!controller.signal.aborted)
          setError(
            failure instanceof Error
              ? failure.message
              : "We couldn’t connect. Please try again.",
          );
      } finally {
        if (!controller.signal.aborted) setLoading(false);
      }
    }
    void load();
    return () => controller.abort();
  }, [attempt]);

  function date(value: string) {
    return new Intl.DateTimeFormat("en-US", {
      weekday: "short",
      month: "short",
      day: "numeric",
      hour: "numeric",
      minute: "2-digit",
      timeZoneName: "short",
      timeZone: dashboard?.organization.timeZone,
    }).format(new Date(value));
  }

  return (
    <SafeAreaView edges={["left", "right", "bottom"]} style={styles.page}>
      <ScrollView
        contentContainerStyle={styles.content}
        refreshControl={
          <RefreshControl
            refreshing={loading && dashboard !== null}
            onRefresh={refresh}
            tintColor={colors.green}
          />
        }
      >
        <Brand />
        <View style={styles.badge}>
          <Text style={styles.badgeText}>Shared development demo</Text>
        </View>
        {loading && !dashboard && (
          <View accessibilityLiveRegion="polite" style={styles.group}>
            <ActivityIndicator color={colors.green} />
            <Text style={styles.body}>Finding your place…</Text>
          </View>
        )}
        {error && (
          <Card>
            <Text accessibilityRole="alert" style={styles.body}>
              {error}
            </Text>
            <Button label="Try again" onPress={refresh} />
          </Card>
        )}
        {dashboard && (
          <>
            <View style={styles.group}>
              <Text style={styles.eyebrow}>{dashboard.organization.name}</Text>
              <Text accessibilityRole="header" style={styles.heading}>
                Good to see you,{" "}
                {dashboard.currentUser.displayName.split(" ")[0]}.
              </Text>
              <Text style={styles.body}>
                Here’s where you’re making room for others.
              </Text>
            </View>
            <Card>
              <View style={styles.row}>
                <Stat
                  value={dashboard.summary.upcomingEvents}
                  label="Gatherings"
                />
                <Stat
                  value={dashboard.summary.pendingResponses}
                  label="Awaiting reply"
                />
                <Stat
                  value={dashboard.summary.acceptedAssignments}
                  label="Accepted"
                />
              </View>
            </Card>
            <View style={styles.group}>
              <Text accessibilityRole="header" style={styles.heading}>
                Your assignments
              </Text>
              <Text style={styles.small}>
                Mobile responses are coming next. Use the web app to Accept or
                Decline, then pull to refresh here.
              </Text>
            </View>
            {dashboard.assignments.length === 0 && (
              <Card>
                <Text style={styles.body}>
                  You’re all clear. New assignments will appear here.
                </Text>
              </Card>
            )}
            {dashboard.assignments.map((assignment) => (
              <Card key={assignment.id}>
                <Text style={styles.eyebrow}>{assignment.teamName}</Text>
                <Text accessibilityRole="header" style={styles.subheading}>
                  {assignment.roleName}
                </Text>
                <Text style={styles.body}>{assignment.eventTitle}</Text>
                <Text style={styles.small}>
                  {date(assignment.eventStartsAt)}
                </Text>
                <View style={styles.badge}>
                  <Text style={styles.badgeText}>
                    {assignment.status === "Pending"
                      ? "Awaiting your reply"
                      : assignment.status}
                  </Text>
                </View>
              </Card>
            ))}
            <Text accessibilityRole="header" style={styles.heading}>
              Coming together
            </Text>
            {dashboard.events.length === 0 && (
              <Text style={styles.body}>No upcoming gatherings just yet.</Text>
            )}
            {dashboard.events.map((event) => (
              <Card key={event.id}>
                <Text accessibilityRole="header" style={styles.subheading}>
                  {event.title}
                </Text>
                <Text style={styles.body}>{date(event.startsAt)}</Text>
                <Text style={styles.small}>{event.location}</Text>
              </Card>
            ))}
          </>
        )}
      </ScrollView>
    </SafeAreaView>
  );
}

function Stat({ value, label }: { value: number; label: string }) {
  return (
    <View style={styles.stat}>
      <Text style={styles.number}>{value}</Text>
      <Text style={styles.small}>{label}</Text>
    </View>
  );
}
