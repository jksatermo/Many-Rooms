/** Event times use the organization's IANA zone, never the device's implicit zone. */
export function formatTime(value: string, timeZone: string): string {
  return new Intl.DateTimeFormat("en-US", {
    weekday: "short",
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
    timeZoneName: "short",
    timeZone,
  }).format(new Date(value));
}
export function deviceTimeZone(): string {
  return Intl.DateTimeFormat().resolvedOptions().timeZone;
}
export function localInput(value: string): string {
  const date = new Date(value);
  const pad = (part: number) => String(part).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`;
}
/** Explicit device-local entry; invalid dates and DST gaps are rejected.
 * Repeated autumn hours choose the earlier occurrence, as JavaScript Date does.
 */
export function localToUtc(input: string): string {
  const match = /^(\d{4})-(\d{2})-(\d{2})[ T](\d{2}):(\d{2})$/.exec(
    input.trim(),
  );
  if (!match)
    throw new Error(
      "Use YYYY-MM-DD HH:mm for both dates, for example 2027-04-18 09:30.",
    );
  const [year, month, day, hour, minute] = match.slice(1).map(Number);
  const date = new Date(year, month - 1, day, hour, minute, 0, 0);
  if (
    year < 2000 ||
    year > 9999 ||
    date.getFullYear() !== year ||
    date.getMonth() !== month - 1 ||
    date.getDate() !== day ||
    date.getHours() !== hour ||
    date.getMinutes() !== minute
  ) {
    throw new Error(
      "Enter a valid local date and time. Times skipped by daylight saving changes are not available.",
    );
  }
  return date.toISOString();
}
