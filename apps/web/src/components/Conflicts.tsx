import type { ScheduleConflict } from '../types';
import { date, time } from '../format';

export function Conflicts({
  conflicts,
  timeZone,
}: {
  conflicts: ScheduleConflict[];
  timeZone: string;
}) {
  if (!conflicts.length) return null;
  return (
    <div className="conflict-note" data-testid="schedule-conflict">
      <strong className="conflict-label">
        <span aria-hidden="true">◷</span> Schedule conflict
      </strong>
      <ul>
        {conflicts.map((conflict, index) => (
          <li
            key={`${conflict.type}-${conflict.relatedAssignmentId ?? conflict.availabilityId}-${index}`}
          >
            <p>{conflict.message}</p>
            <small>
              {date(conflict.startsAt, timeZone)} ·{' '}
              {time(conflict.startsAt, timeZone)} –{' '}
              {date(conflict.startsAt, timeZone) !==
                date(conflict.endsAt, timeZone) &&
                `${date(conflict.endsAt, timeZone)} · `}
              {time(conflict.endsAt, timeZone)}
            </small>
          </li>
        ))}
      </ul>
      <p className="conflict-help">
        A heads-up for planning. Your assignment hasn’t been changed.
      </p>
    </div>
  );
}
