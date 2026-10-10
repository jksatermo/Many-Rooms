import { Text, View } from "react-native";
import type { ScheduleConflict } from "../lib/api";
import { formatTime } from "../lib/time";
import { styles } from "./Ui";

export function Conflicts({
  conflicts,
  timeZone,
}: {
  conflicts: ScheduleConflict[];
  timeZone: string;
}) {
  if (!conflicts.length) return null;
  return (
    <View style={styles.notice}>
      <Text style={styles.subheading}>Schedule conflict</Text>
      {conflicts.map((conflict) => (
        <View
          style={styles.group}
          key={`${conflict.type}-${conflict.relatedAssignmentId ?? conflict.availabilityId}`}
        >
          <Text style={styles.body}>{conflict.message}</Text>
          <Text style={styles.small}>
            {formatTime(conflict.startsAt, timeZone)} –{" "}
            {formatTime(conflict.endsAt, timeZone)}
          </Text>
        </View>
      ))}
      <Text style={styles.small}>
        Your assignment stays in place. You can still reply and coordinate with
        your team.
      </Text>
    </View>
  );
}
