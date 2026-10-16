import { useCallback } from "react";
import { router } from "expo-router";
import { RefreshControl, ScrollView, Text, View } from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";
import {
  Brand,
  Button,
  Card,
  colors,
  ErrorCard,
  Loading,
  Status,
  styles,
} from "../components/Ui";
import { Conflicts } from "../components/Conflicts";
import { api } from "../lib/config";
import { formatTime } from "../lib/time";
import { useFocusedResource } from "../hooks/useFocusedResource";

export default function DashboardScreen() {
  const load = useCallback(async (signal: AbortSignal) => {
    const context = await api.context(signal);
    return api.dashboard(context.organizationId, signal);
  }, []);
  const { data: dashboard, loading, error, refresh } = useFocusedResource(load);
  function openEvent(id: string) {
    router.push({ pathname: "/events/[id]", params: { id } });
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
        {loading && !dashboard && <Loading label="Finding your place…" />}
        {error && <ErrorCard message={error} retry={refresh} />}
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
            <Button
              secondary
              label="Manage my unavailable time"
              onPress={() => router.push("/availability")}
            />
            <Text accessibilityRole="header" style={styles.heading}>
              Your assignments
            </Text>
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
                  {formatTime(
                    assignment.eventStartsAt,
                    dashboard.organization.timeZone,
                  )}
                </Text>
                <Status
                  status={assignment.status}
                  isCancelled={assignment.isCancelled}
                />
                {!assignment.isCancelled && (
                  <Conflicts
                    conflicts={assignment.conflicts}
                    timeZone={dashboard.organization.timeZone}
                  />
                )}
                <Button
                  secondary
                  label="View assignment"
                  accessibilityLabel={`View ${assignment.roleName} assignment for ${assignment.eventTitle}`}
                  onPress={() => openEvent(assignment.eventId)}
                />
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
                <Text style={styles.body}>
                  {formatTime(event.startsAt, dashboard.organization.timeZone)}
                </Text>
                <Text style={styles.small}>{event.location}</Text>
                <Button
                  secondary
                  label="Open event"
                  accessibilityLabel={`Open ${event.title}`}
                  onPress={() => openEvent(event.id)}
                />
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
