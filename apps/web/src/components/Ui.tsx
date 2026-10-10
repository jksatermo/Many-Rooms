import { Arrow } from './Icons';
import { Link } from 'react-router-dom';
import { ApiError } from '../api';
import type { AssignmentStatus, EventSummary } from '../types';
import { day, shortMonth, time } from '../format';

export function Status({ value }: { value: AssignmentStatus }) {
  return (
    <span className={`status status-${value.toLowerCase()}`}>
      <span aria-hidden="true">
        {value === 'Accepted' ? '✓' : value === 'Declined' ? '−' : '○'}
      </span>
      {value === 'Pending' ? 'Awaiting response' : value}
    </span>
  );
}
export function Loading({
  label = 'Loading your community…',
}: {
  label?: string;
}) {
  return (
    <div className="state-box" role="status">
      <span className="loading-mark" aria-hidden="true" />
      <p>{label}</p>
    </div>
  );
}
export function ErrorState({
  error,
  onRetry,
}: {
  error: Error;
  onRetry?: () => void;
}) {
  return (
    <div className="state-box error-state" role="alert">
      <h2>We couldn’t load this just yet.</h2>
      <p>{error.message}</p>
      {error instanceof ApiError && error.status === 401 ? (
        <Link className="button" to="/demo">
          Return to demo entry
        </Link>
      ) : onRetry ? (
        <button className="button button-outline" onClick={onRetry}>
          Try again
        </button>
      ) : null}
    </div>
  );
}
export function EventCard({
  event,
  timeZone,
  leader = false,
}: {
  event: EventSummary;
  timeZone: string;
  leader?: boolean;
}) {
  return (
    <Link
      className="event-card"
      to={`/app/${leader ? 'leader/' : ''}events/${event.id}`}
    >
      <div className="date-tile" aria-hidden="true">
        <span>{shortMonth(event.startsAt, timeZone)}</span>
        <strong>{day(event.startsAt, timeZone)}</strong>
      </div>
      <div className="event-card-content">
        <h3>{event.title}</h3>
        <p>
          <time dateTime={event.startsAt}>
            {new Intl.DateTimeFormat('en-US', {
              month: 'long',
              day: 'numeric',
              timeZone,
            }).format(new Date(event.startsAt))}{' '}
            · {time(event.startsAt, timeZone)}
          </time>
        </p>
        <p className="muted">{event.location}</p>
      </div>
      <span className="event-count">
        {event.assignmentCount} volunteer{' '}
        {event.assignmentCount === 1 ? 'assignment' : 'assignments'}
      </span>
      <span className="card-arrow" aria-hidden="true">
        <Arrow />
      </span>
    </Link>
  );
}
