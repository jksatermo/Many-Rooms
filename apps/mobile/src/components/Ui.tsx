import type { ReactNode } from "react";
import {
  ActivityIndicator,
  Pressable,
  StyleSheet,
  Text,
  View,
} from "react-native";

export const colors = {
  background: "#f5f3ec",
  paper: "#fffdf7",
  green: "#35584a",
  ink: "#253e35",
  muted: "#56665d",
  line: "#d8ded2",
  pale: "#e6eddf",
};

export function Brand() {
  return (
    <View style={styles.brand}>
      <View style={styles.mark}>
        <Text style={styles.markText}>m</Text>
      </View>
      <Text style={styles.brandText}>Many Rooms</Text>
    </View>
  );
}

export function Card({ children }: { children: ReactNode }) {
  return <View style={styles.card}>{children}</View>;
}

export function Button({
  label,
  onPress,
  disabled = false,
  secondary = false,
  accessibilityLabel,
}: {
  label: string;
  onPress: () => void;
  disabled?: boolean;
  secondary?: boolean;
  accessibilityLabel?: string;
}) {
  return (
    <Pressable
      accessibilityRole="button"
      onPress={onPress}
      disabled={disabled}
      accessibilityLabel={accessibilityLabel ?? label}
      accessibilityState={{ disabled }}
      style={({ pressed }) => [
        styles.button,
        secondary && styles.secondaryButton,
        (pressed || disabled) && { opacity: 0.6 },
      ]}
    >
      <Text style={[styles.buttonText, secondary && { color: colors.ink }]}>
        {label}
      </Text>
    </Pressable>
  );
}

export function Loading({
  label = "Loading your schedule…",
}: {
  label?: string;
}) {
  return (
    <View accessibilityLiveRegion="polite" style={styles.group}>
      <ActivityIndicator color={colors.green} />
      <Text style={styles.body}>{label}</Text>
    </View>
  );
}
export function ErrorCard({
  message,
  retry,
}: {
  message: string;
  retry: () => void;
}) {
  return (
    <Card>
      <Text accessibilityRole="alert" style={styles.body}>
        {message}
      </Text>
      <Button label="Try again" onPress={retry} />
    </Card>
  );
}
export function Status({
  status,
  isCancelled = false,
}: {
  status: "Pending" | "Accepted" | "Declined";
  isCancelled?: boolean;
}) {
  return (
    <View style={styles.badge}>
      <Text style={styles.badgeText}>
        {isCancelled
          ? "Cancelled"
          : status === "Pending"
            ? "Awaiting reply"
            : status}
      </Text>
    </View>
  );
}

export const styles = StyleSheet.create({
  page: { flex: 1, backgroundColor: colors.background },
  content: {
    padding: 24,
    paddingBottom: 48,
    gap: 24,
    width: "100%",
    maxWidth: 700,
    alignSelf: "center",
  },
  brand: { flexDirection: "row", alignItems: "center", gap: 10 },
  mark: {
    backgroundColor: colors.green,
    borderTopLeftRadius: 16,
    borderTopRightRadius: 16,
    borderBottomLeftRadius: 4,
    borderBottomRightRadius: 4,
    width: 36,
    height: 38,
    alignItems: "center",
    justifyContent: "center",
  },
  markText: {
    fontSize: 27,
    fontWeight: "600",
    color: colors.paper,
    lineHeight: 30,
  },
  brandText: {
    fontSize: 23,
    fontWeight: "600",
    color: colors.ink,
    letterSpacing: -0.7,
  },
  eyebrow: {
    textTransform: "uppercase",
    letterSpacing: 2,
    color: colors.green,
    fontSize: 11,
    fontWeight: "700",
  },
  title: {
    fontSize: 44,
    lineHeight: 50,
    letterSpacing: -1.5,
    color: colors.ink,
    fontWeight: "600",
  },
  heading: {
    fontSize: 25,
    lineHeight: 32,
    color: colors.ink,
    fontWeight: "600",
    letterSpacing: -0.5,
  },
  subheading: {
    fontSize: 18,
    lineHeight: 25,
    color: colors.ink,
    fontWeight: "600",
  },
  body: { fontSize: 16, lineHeight: 25, color: colors.muted },
  small: { fontSize: 13, lineHeight: 20, color: colors.muted },
  card: {
    padding: 22,
    borderRadius: 22,
    borderWidth: 1,
    borderColor: colors.line,
    backgroundColor: colors.paper,
    gap: 12,
  },
  button: {
    backgroundColor: colors.green,
    borderRadius: 14,
    minHeight: 50,
    paddingHorizontal: 20,
    paddingVertical: 15,
    alignItems: "center",
    justifyContent: "center",
  },
  secondaryButton: {
    backgroundColor: colors.pale,
    borderWidth: 1,
    borderColor: colors.line,
  },
  input: {
    minHeight: 50,
    padding: 14,
    borderWidth: 1,
    borderColor: colors.muted,
    borderRadius: 12,
    fontSize: 16,
    color: colors.ink,
    backgroundColor: colors.paper,
  },
  notice: {
    gap: 8,
    padding: 14,
    borderRadius: 12,
    backgroundColor: "#f1ead9",
    borderLeftWidth: 3,
    borderLeftColor: "#7c6431",
  },
  buttonText: { color: colors.paper, fontSize: 16, fontWeight: "600" },
  group: { gap: 12 },
  badge: {
    alignSelf: "flex-start",
    backgroundColor: colors.pale,
    borderRadius: 30,
    paddingHorizontal: 12,
    paddingVertical: 6,
  },
  badgeText: { color: colors.ink, fontSize: 12, fontWeight: "600" },
  divider: { height: 1, backgroundColor: colors.line, marginVertical: 4 },
  row: { flexDirection: "row", flexWrap: "wrap", gap: 20 },
  stat: { flexGrow: 1, minWidth: 85, gap: 4 },
  number: { color: colors.green, fontSize: 32, fontWeight: "600" },
});
