import { useCallback, useState } from "react";
import {
  KeyboardAvoidingView,
  Platform,
  RefreshControl,
  ScrollView,
  Text,
  TextInput,
  View,
} from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";
import {
  Button,
  Card,
  colors,
  ErrorCard,
  Loading,
  styles,
} from "../components/Ui";
import { ApiError, type Unavailability } from "../lib/api";
import { api } from "../lib/config";
import {
  deviceTimeZone,
  formatTime,
  localInput,
  localToUtc,
} from "../lib/time";
import { useFocusedResource } from "../hooks/useFocusedResource";

export default function AvailabilityScreen() {
  const load = useCallback(async (signal: AbortSignal) => {
    const context = await api.context(signal);
    return {
      context,
      entries: await api.availability(context.organizationId, signal),
    };
  }, []);
  const { data, loading, error, refresh } = useFocusedResource(load);
  const [editing, setEditing] = useState<Unavailability | "new" | null>(null);
  const [startsAt, setStartsAt] = useState("");
  const [endsAt, setEndsAt] = useState("");
  const [note, setNote] = useState("");
  const [removing, setRemoving] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [feedback, setFeedback] = useState<string | null>(null);
  const [failure, setFailure] = useState<string | null>(null);
  const [stale, setStale] = useState(false);
  const zone = deviceTimeZone();
  function begin(entry: Unavailability | "new") {
    setEditing(entry);
    setStartsAt(entry === "new" ? "" : localInput(entry.startsAt));
    setEndsAt(entry === "new" ? "" : localInput(entry.endsAt));
    setNote(entry === "new" ? "" : (entry.note ?? ""));
    setFailure(null);
    setFeedback(null);
    setStale(false);
    setRemoving(null);
  }
  function reload() {
    setEditing(null);
    setRemoving(null);
    setStale(false);
    setFailure(null);
    refresh();
  }
  function report(problem: unknown) {
    setFailure(
      problem instanceof Error
        ? problem.message
        : "We couldn’t update your unavailable time.",
    );
    setStale(
      problem instanceof ApiError &&
        (problem.status === 409 || problem.status === 404),
    );
  }
  async function save() {
    if (!data || !editing || saving) return;
    setFailure(null);
    setFeedback(null);
    try {
      // Preserve the exact saved instants on note-only edits, including seconds
      // and the later occurrence of a repeated daylight-saving hour.
      const start =
        editing !== "new" && startsAt === localInput(editing.startsAt)
          ? editing.startsAt
          : localToUtc(startsAt);
      const end =
        editing !== "new" && endsAt === localInput(editing.endsAt)
          ? editing.endsAt
          : localToUtc(endsAt);
      if (new Date(end).getTime() <= new Date(start).getTime())
        throw new Error("End must be later than start.");
      const input = { startsAt: start, endsAt: end, note: note.trim() || null };
      setSaving(true);
      if (editing === "new")
        await api.createAvailability(data.context.organizationId, input);
      else
        await api.updateAvailability(
          data.context.organizationId,
          editing.id,
          input,
          editing.version,
        );
      setEditing(null);
      setFeedback(
        "Your unavailable time is saved. Schedule conflicts will reflect this change.",
      );
      refresh();
    } catch (problem) {
      report(problem);
    } finally {
      setSaving(false);
    }
  }
  async function remove(entry: Unavailability) {
    if (!data || saving) return;
    setSaving(true);
    setFailure(null);
    setFeedback(null);
    try {
      await api.deleteAvailability(
        data.context.organizationId,
        entry.id,
        entry.version,
      );
      setRemoving(null);
      setFeedback("Unavailable time removed. Your assignments are unchanged.");
      refresh();
    } catch (problem) {
      report(problem);
    } finally {
      setSaving(false);
    }
  }
  return (
    <SafeAreaView edges={["left", "right", "bottom"]} style={styles.page}>
      <KeyboardAvoidingView
        style={{ flex: 1 }}
        behavior={Platform.OS === "ios" ? "padding" : undefined}
      >
        <ScrollView
          keyboardShouldPersistTaps="handled"
          contentContainerStyle={styles.content}
          refreshControl={
            <RefreshControl
              refreshing={loading && data !== null}
              onRefresh={reload}
              tintColor={colors.green}
            />
          }
        >
          <View style={styles.group}>
            <Text style={styles.eyebrow}>
              Shared development demo · Your schedule
            </Text>
            <Text accessibilityRole="header" style={styles.heading}>
              Make room for life.
            </Text>
            <Text style={styles.body}>
              Let your team know when you can’t be there. Unavailable time
              highlights possible conflicts; it never cancels an assignment.
            </Text>
          </View>
          {loading && !data && <Loading label="Loading unavailable time…" />}
          {error && <ErrorCard message={error} retry={reload} />}
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
          {data && (
            <>
              {editing === null && (
                <Button
                  label="Add unavailable time"
                  disabled={loading || saving}
                  onPress={() => begin("new")}
                />
              )}
              {editing !== null && (
                <Card>
                  <Text accessibilityRole="header" style={styles.subheading}>
                    {editing === "new"
                      ? "Add unavailable time"
                      : "Edit unavailable time"}
                  </Text>
                  <Text style={styles.small}>
                    Enter dates in your device time zone: {zone}. Use YYYY-MM-DD
                    HH:mm and a 24-hour clock. During a repeated daylight saving
                    hour, the earlier occurrence is used.
                  </Text>
                  <Text style={styles.body}>Start</Text>
                  <TextInput
                    accessibilityLabel="Unavailable start"
                    placeholder="YYYY-MM-DD HH:mm"
                    placeholderTextColor={colors.muted}
                    value={startsAt}
                    onChangeText={setStartsAt}
                    editable={!saving && !stale}
                    autoCapitalize="none"
                    autoCorrect={false}
                    style={styles.input}
                  />
                  <Text style={styles.body}>End</Text>
                  <TextInput
                    accessibilityLabel="Unavailable end"
                    placeholder="YYYY-MM-DD HH:mm"
                    placeholderTextColor={colors.muted}
                    value={endsAt}
                    onChangeText={setEndsAt}
                    editable={!saving && !stale}
                    autoCapitalize="none"
                    autoCorrect={false}
                    style={styles.input}
                  />
                  <Text style={styles.body}>Private note (optional)</Text>
                  <TextInput
                    accessibilityLabel="Private note"
                    value={note}
                    onChangeText={setNote}
                    editable={!saving && !stale}
                    maxLength={500}
                    multiline
                    textAlignVertical="top"
                    style={[styles.input, { minHeight: 90 }]}
                  />
                  <Text style={styles.small}>
                    Notes are only shown to you within this shared demo persona.
                    Leaders see the conflicting time, without your note.
                  </Text>
                  <Button
                    label={
                      saving
                        ? "Saving unavailable time…"
                        : "Save unavailable time"
                    }
                    disabled={saving || loading || stale}
                    onPress={() => void save()}
                  />
                  <Button
                    secondary
                    label="Cancel editing"
                    disabled={saving}
                    onPress={() => {
                      setEditing(null);
                      setFailure(null);
                    }}
                  />
                </Card>
              )}
              <Text accessibilityRole="header" style={styles.heading}>
                Your unavailable time
              </Text>
              <Text style={styles.small}>
                Saved ranges below are shown in {data.context.timeZone}, your
                community’s time zone.
              </Text>
              {data.entries.length === 0 && (
                <Card>
                  <Text style={styles.body}>
                    No unavailable time saved. Add a range whenever you have
                    other plans.
                  </Text>
                </Card>
              )}
              {data.entries.map((entry) => (
                <Card key={entry.id}>
                  <Text style={styles.body}>
                    {formatTime(entry.startsAt, data.context.timeZone)}
                  </Text>
                  <Text style={styles.small}>
                    Until {formatTime(entry.endsAt, data.context.timeZone)}
                  </Text>
                  {entry.note && (
                    <Text style={styles.small}>Private note: {entry.note}</Text>
                  )}
                  {removing === entry.id ? (
                    <>
                      <Text style={styles.body}>
                        Remove this unavailable time? Your assignments will stay
                        in place.
                      </Text>
                      <Button
                        label={saving ? "Removing…" : "Confirm removal"}
                        disabled={saving || loading || stale}
                        onPress={() => void remove(entry)}
                      />
                      <Button
                        secondary
                        label="Keep this time"
                        disabled={saving}
                        onPress={() => setRemoving(null)}
                      />
                    </>
                  ) : (
                    <>
                      <Button
                        secondary
                        label="Edit time"
                        accessibilityLabel={`Edit unavailable time starting ${formatTime(entry.startsAt, data.context.timeZone)}`}
                        disabled={saving || loading}
                        onPress={() => begin(entry)}
                      />
                      <Button
                        secondary
                        label="Remove time"
                        accessibilityLabel={`Remove unavailable time starting ${formatTime(entry.startsAt, data.context.timeZone)}`}
                        disabled={saving || loading}
                        onPress={() => {
                          setRemoving(entry.id);
                          setEditing(null);
                          setFailure(null);
                          setStale(false);
                        }}
                      />
                    </>
                  )}
                </Card>
              ))}
            </>
          )}
        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}
