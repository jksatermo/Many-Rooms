export const date = (value: string, timeZone: string) =>
  new Intl.DateTimeFormat('en-US', {
    month: 'long',
    day: 'numeric',
    weekday: 'long',
    timeZone,
  }).format(new Date(value));
export const shortMonth = (value: string, timeZone: string) =>
  new Intl.DateTimeFormat('en-US', { month: 'short', timeZone }).format(
    new Date(value),
  );
export const day = (value: string, timeZone: string) =>
  new Intl.DateTimeFormat('en-US', { day: 'numeric', timeZone }).format(
    new Date(value),
  );
export const time = (value: string, timeZone: string) =>
  new Intl.DateTimeFormat('en-US', {
    hour: 'numeric',
    minute: '2-digit',
    timeZoneName: 'short',
    timeZone,
  }).format(new Date(value));
