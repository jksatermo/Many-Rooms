// datetime-local has no zone. Interpret it in the explicitly named device zone,
// reject normalization of invalid dates / spring-forward gaps, then send UTC.
// JavaScript chooses the earlier instant for a repeated fall-back local hour.
export function localDateTimeToUtc(value: string): string {
  const parts = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})$/.exec(value);
  if (!parts) throw new Error('Choose a valid start and end date and time.');
  const [, year, month, day, hour, minute] = parts.map(Number);
  const instant = new Date(value);
  if (
    !Number.isFinite(instant.getTime()) ||
    instant.getFullYear() !== year ||
    instant.getMonth() + 1 !== month ||
    instant.getDate() !== day ||
    instant.getHours() !== hour ||
    instant.getMinutes() !== minute
  )
    throw new Error(
      'That local time does not exist. Choose another date or time, including around daylight saving changes.',
    );
  return instant.toISOString();
}

export function toLocalDateTime(value: string): string {
  const date = new Date(value);
  const pad = (part: number) => String(part).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}
