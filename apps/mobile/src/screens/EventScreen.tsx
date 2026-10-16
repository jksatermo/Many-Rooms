import { useCallback, useEffect, useState } from "react";
import { RefreshControl, ScrollView, Text, View } from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";
import {
  Button,
  Card,
  colors,
  ErrorCard,
  Loading,
  Status,
  styles,
} from "../components/Ui";
import { Conflicts } from "../components/Conflicts";
import { ApiError, type Assignment } from "../lib/api";
import { api } from "../lib/config";
import { formatTime } from "../lib/time";
import { useFocusedResource } from "../hooks/useFocusedResource";

export default function EventScreen({ eventId }: { eventId: string }) {
  // A new route must not retain another event's cached assignments or controls.
  return <EventContent key={eventId} eventId={eventId} />;
}

function EventContent({ eventId }: { eventId: string }) {
  const load = useCallback(
    async (signal: AbortSignal) => {
      const context = await api.context(signal);
      return {
        context,
        event: await api.event(context.organizationId, eventId, signal),
      };
    },
    [eventId],
  );
  const { data, setData, loading, error, refresh } = useFocusedResource(load);
  const [saving, setSaving] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<string | null>(null);
  const [failure, setFailure] = useState<string | null>(null);
  const [stale, setStale] = useState(false);
  useEffect(() => {
    if (data && !loading && !error) setStale(false);
  }, [data, loading, error]);
  const reload = () => {
    setFailure(null);
    refresh();
  };
  async function respond(
    assignment: Assignment,
    decision: "Accepted" | "Declined",
  ) {
    if (
      !data ||
      saving ||
      loading ||
      error ||
      stale ||
      !assignment.isCurrentUser ||
      assignment.isCancelled
    )
      return;
    setSaving(assignment.id);
    setFeedback(null);
    setFailure(null);
    try {
      const updated = await api.respond(
        data.context.organizationId,
        assignment.id,
        decision,
        assignment.version,
      );
      setData(
        (current) =>
          current && {
            ...current,
            event: {
              ...current.event,
              assignments: current.event.assignments.map((item) =>
                item.id === updated.id ? updated : item,
              ),
            },
          },
      );
      setFeedback(
        `${assignment.roleName}: ${decision.toLowerCase()}. Your response is saved.`,
      );
      refresh();
    } catch (problem) {
      setFailure(
        problem instanceof Error
          ? problem.message
          : "We couldn’t save your response. Please try again.",
      );
      // A leader may have cancelled or reassigned the assignment since loading.
      // Review fresh ownership/version data before attempting another reply.
      setStale(
        problem instanceof ApiError && [403, 404, 409].includes(problem.status),
      );
    } finally {
      setSaving(null);
    }
  }
  return (
    <SafeAreaView edges={["left", "right", "bottom"]} style={styles.page}>
      <ScrollView
        contentContainerStyle={styles.content}
        refreshControl={
          <RefreshControl
            refreshing={loading && data !== null}
            onRefresh={reload}
            tintColor={colors.green}
          />
        }
      >
        <Text style={styles.eyebrow}>Shared development demo · Volunteer</Text>
        {loading && !data && <Loading label="Loading event…" />}
        {error && <ErrorCard message={error} retry={reload} />}
        {data && (
          <>
            <View style={styles.group}>
              <Text style={styles.eyebrow}>
                {data.context.organizationName}
              </Text>
              <Text accessibilityRole="header" style={styles.heading}>
                {data.event.title}
              </Text>
              <Text style={styles.body}>{data.event.description}</Text>
              <Text style={styles.body}>
                {formatTime(data.event.startsAt, data.context.timeZone)} –{" "}
                {formatTime(data.event.endsAt, data.context.timeZone)}
              </Text>
              <Text style={styles.small}>{data.event.location}</Text>
            </View>
            {feedback && (
              <Text accessibilityLiveRegion="polite" style={styles.body}>
                {feedback}
              </Text>
            )}
            {failure && (
              <Card>
                <Text accessibilityRole="alert" style={styles.body}>
                  {failure}
                </Text>
                {stale && (
                  <Button label="Refresh latest details" onPress={reload} />
                )}
              </Card>
            )}
            <Text accessibilityRole="header" style={styles.heading}>
              Showing up together
            </Text>
            {data.event.assignments.length === 0 && (
              <Card>
                <Text style={styles.body}>
                  No volunteers have been assigned yet.
                </Text>
              </Card>
            )}
            {data.event.assignments.map((assignment) => (
              <Card key={assignment.id}>
                <Text style={styles.eyebrow}>{assignment.teamName}</Text>
                <Text accessibilityRole="header" style={styles.subheading}>
                  {assignment.roleName}
                </Text>
                <Text style={styles.body}>
                  {assignment.volunteerName}
                  {assignment.isCurrentUser ? " · You" : ""}
                </Text>
                <Status
                  status={assignment.status}
                  isCancelled={assignment.isCancelled}
                />
                {assignment.isCancelled ? (
                  <Text style={styles.small}>
                    This assignment was cancelled. No reply is needed.
                  </Text>
                ) : (
                  <Conflicts
                    conflicts={assignment.conflicts}
                    timeZone={data.context.timeZone}
                  />
                )}
                {assignment.isCurrentUser && !assignment.isCancelled && (
                  <>
                    {new Date(data.event.startsAt).getTime() <= Date.now() ? (
                      <Text style={styles.small}>
                        This event has started, so replies are closed.
                      </Text>
                    ) : (
                      <>
                        <Text style={styles.small}>
                          Can you join the team? Your latest reply is shared
                          with your community.
                        </Text>
                        <Button
                          label={
                            saving === assignment.id
                              ? "Saving response…"
                              : "Accept"
                          }
                          accessibilityLabel={`Accept ${assignment.roleName}`}
                          disabled={
                            saving !== null ||
                            loading ||
                            error !== null ||
                            stale ||
                            assignment.status === "Accepted"
                          }
                          onPress={() => void respond(assignment, "Accepted")}
                        />
                        <Button
                          secondary
                          label="Decline"
                          accessibilityLabel={`Decline ${assignment.roleName}`}
                          disabled={
                            saving !== null ||
                            loading ||
                            error !== null ||
                            stale ||
                            assignment.status === "Declined"
                          }
                          onPress={() => void respond(assignment, "Declined")}
                        />
                      </>
                    )}
                  </>
                )}
              </Card>
            ))}
          </>
        )}
      </ScrollView>
    </SafeAreaView>
  );
}
