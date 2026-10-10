import { Arrow, Spark } from '../components/Icons';
import { useCallback, useEffect, useState } from 'react';
import { Link, useOutletContext, useParams } from 'react-router-dom';
import { api, ApiError } from '../api';
import { useResource } from '../useResource';
import type { Assignment, Decision, DemoContext } from '../types';
import { ErrorState, Loading, Status } from '../components/Ui';
import { date, time } from '../format';
import { Conflicts } from '../components/Conflicts';

export function EventDetails() {
  const context = useOutletContext<DemoContext>();
  const { eventId = '' } = useParams();
  const [saving, setSaving] = useState<string | null>(null);
  const result = useResource(
    useCallback(
      (signal: AbortSignal) =>
        api.event(context.organizationId, eventId, signal),
      [context.organizationId, eventId],
    ),
    { refreshOnFocus: saving === null },
  );
  const [now, setNow] = useState(Date.now);
  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 30_000);
    return () => window.clearInterval(timer);
  }, []);
  const [notice, setNotice] = useState('');
  const [error, setError] = useState<Error | null>(null);

  async function respond(assignment: Assignment, decision: Decision) {
    if (saving || !result.data) return;
    setSaving(assignment.id);
    setNotice('');
    setError(null);
    try {
      const updated = await api.respond(
        context.organizationId,
        assignment,
        decision,
      );
      result.setData({
        ...result.data,
        assignments: result.data.assignments.map((item) =>
          item.id === updated.id ? updated : item,
        ),
      });
      setNotice(
        decision === 'Accepted'
          ? 'You’re in! Your acceptance has been saved.'
          : 'Your decline has been saved. Thanks for letting your team know.',
      );
    } catch (cause) {
      if (cause instanceof ApiError && cause.status === 409) {
        setNotice(
          'This assignment changed or the event has already started. We’ve refreshed the details. Review them before choosing a response again.',
        );
        result.reload();
      } else
        setError(
          cause instanceof Error
            ? cause
            : new Error('Your response could not be saved. Please try again.'),
        );
    } finally {
      setSaving(null);
    }
  }

  if (result.loading)
    return (
      <>
        <Link className="back-link" to="/app/events">
          ← All events
        </Link>
        {notice && (
          <p className="notice" role="status">
            {notice}
          </p>
        )}
        <Loading label="Loading the event and its teams…" />
      </>
    );
  if (result.error)
    return (
      <>
        <Link className="back-link" to="/app/events">
          ← All events
        </Link>
        <ErrorState error={result.error} onRetry={result.reload} />
      </>
    );
  const event = result.data!;
  const teams = Array.from(
    new Set(event.assignments.map((assignment) => assignment.teamName)),
  );
  const started = new Date(event.startsAt).getTime() <= now;
  return (
    <>
      <Link className="back-link" to="/app/events">
        ← All events
      </Link>
      <div className="event-hero">
        <p className="eyebrow">{context.organizationName}</p>
        <h1>{event.title}</h1>
        <p className="event-description">{event.description}</p>
        <div className="event-metadata">
          <span>
            <span className="metadata-icon" aria-hidden="true">
              ◷
            </span>
            <span>
              <time dateTime={event.startsAt}>
                {date(event.startsAt, context.timeZone)}
              </time>
              <small>
                {time(event.startsAt, context.timeZone)} –{' '}
                {time(event.endsAt, context.timeZone)}
              </small>
            </span>
          </span>
          <span>
            <span className="metadata-icon" aria-hidden="true">
              ⌂
            </span>
            <span>
              {event.location}
              <small>A place to come together</small>
            </span>
          </span>
        </div>
      </div>
      <div className="event-detail-grid">
        <section aria-labelledby="teams-heading">
          <div className="section-heading">
            <div>
              <p className="eyebrow">Better together</p>
              <h2 id="teams-heading">The people making it happen.</h2>
            </div>
          </div>
          {notice && (
            <p className="notice" role="status">
              {notice}
            </p>
          )}
          {error && (
            <div className="inline-error" role="alert">
              <p>{error.message}</p>
              {error instanceof ApiError && error.status === 401 && (
                <Link className="text-link" to="/demo">
                  Return to demo entry
                </Link>
              )}
            </div>
          )}
          {teams.length ? (
            teams.map((team) => (
              <section className="team-section" key={team} aria-label={team}>
                <div className="team-heading">
                  <h3>{team}</h3>
                  <span>
                    {
                      event.assignments.filter(
                        (assignment) => assignment.teamName === team,
                      ).length
                    }{' '}
                    on the team
                  </span>
                </div>
                {event.assignments
                  .filter((assignment) => assignment.teamName === team)
                  .map((assignment) => (
                    <article
                      key={assignment.id}
                      className={`assignment ${assignment.isCurrentUser ? 'assignment-yours' : ''}`}
                      data-testid="assignment"
                      data-current-user={assignment.isCurrentUser}
                      data-status={assignment.status}
                    >
                      <div className="assignment-main">
                        <span
                          className={`avatar ${assignment.isCurrentUser ? 'avatar-clay' : 'avatar-sage'}`}
                          aria-hidden="true"
                        >
                          {assignment.volunteerName
                            .split(' ')
                            .slice(0, 2)
                            .map((part) => part[0])
                            .join('')}
                        </span>
                        <div className="assignment-person">
                          <h4>
                            {assignment.volunteerName}
                            {assignment.isCurrentUser && (
                              <span className="you-label">You</span>
                            )}
                          </h4>
                          <p>{assignment.roleName}</p>
                        </div>
                        <Status value={assignment.status} />
                      </div>
                      <Conflicts
                        conflicts={assignment.conflicts}
                        timeZone={context.timeZone}
                      />
                      {assignment.isCurrentUser && (
                        <div className="assignment-response">
                          <p>
                            {started
                              ? 'This event has started, so responses are closed.'
                              : assignment.status === 'Pending'
                                ? 'Can you lend a hand? Let your team know.'
                                : 'Plans changed? You can update your response before the event starts.'}
                          </p>
                          {!started && (
                            <div
                              className="response-buttons"
                              aria-label={`Your response for ${assignment.roleName}`}
                            >
                              <button
                                className="button button-small"
                                disabled={
                                  saving !== null ||
                                  assignment.status === 'Accepted'
                                }
                                onClick={() =>
                                  void respond(assignment, 'Accepted')
                                }
                              >
                                {saving === assignment.id
                                  ? 'Saving…'
                                  : assignment.status === 'Accepted'
                                    ? 'Accepted'
                                    : 'Accept'}{' '}
                                <span aria-hidden="true">✓</span>
                              </button>
                              <button
                                className="button button-outline button-small"
                                disabled={
                                  saving !== null ||
                                  assignment.status === 'Declined'
                                }
                                onClick={() =>
                                  void respond(assignment, 'Declined')
                                }
                              >
                                {assignment.status === 'Declined'
                                  ? 'Declined'
                                  : 'Decline'}{' '}
                                <span aria-hidden="true">−</span>
                              </button>
                            </div>
                          )}
                        </div>
                      )}
                    </article>
                  ))}
              </section>
            ))
          ) : (
            <div className="empty-state">
              <h3>A team is still coming together.</h3>
              <p>No volunteer assignments have been added to this event yet.</p>
            </div>
          )}
        </section>
        <aside className="event-sidebar">
          <span className="outline-spark" aria-hidden="true">
            <Spark />
          </span>
          <h2>Every little part matters.</h2>
          <p>
            Your response helps the people organizing this gathering know who’s
            able to be there.
          </p>
          <div className="sidebar-rule" />
          <p className="eyebrow">You’re exploring as</p>
          <strong>{context.displayName}</strong>
          <p className="muted">
            A fictional volunteer in our shared development demo. You can
            respond to your own assignments.
          </p>
          <Link className="text-link" to="/app">
            Back to your overview{' '}
            <span aria-hidden="true">
              <Arrow />
            </span>
          </Link>
          <Link className="text-link" to="/app/availability">
            Manage my unavailable time →
          </Link>
        </aside>
      </div>
    </>
  );
}
