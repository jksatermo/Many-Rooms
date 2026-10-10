import { Arrow, Spark } from '../components/Icons';
import { useCallback } from 'react';
import { Link, useOutletContext } from 'react-router-dom';
import { api } from '../api';
import { useResource } from '../useResource';
import type { DemoContext } from '../types';
import { ErrorState, EventCard, Loading, Status } from '../components/Ui';
import { date } from '../format';

export function Dashboard() {
  const context = useOutletContext<DemoContext>();
  const result = useResource(
    useCallback(
      (signal: AbortSignal) => api.dashboard(context.organizationId, signal),
      [context.organizationId],
    ),
  );
  if (result.loading) return <Loading label="Getting your community ready…" />;
  if (result.error)
    return <ErrorState error={result.error} onRetry={result.reload} />;
  const data = result.data!;
  const pending = data.assignments.filter(
    (assignment) => assignment.isCurrentUser && assignment.status === 'Pending',
  );
  return (
    <>
      <div className="page-heading">
        <div>
          <p className="eyebrow">{data.organization.name}</p>
          <h1>
            Welcome back, {data.currentUser.displayName.split(' ')[0]}
            <span className="heading-period">.</span>
          </h1>
          <p>There’s a place for you here. Let’s see what’s coming up.</p>
        </div>
        <div className="community-symbol" aria-hidden="true">
          <Spark />
        </div>
      </div>
      <section className="summary-grid" aria-label="Your upcoming schedule">
        <div>
          <span>On the calendar</span>
          <strong>
            {data.summary.upcomingEvents.toString().padStart(2, '0')}
          </strong>
          <small>
            upcoming {data.summary.upcomingEvents === 1 ? 'event' : 'events'}
          </small>
        </div>
        <div>
          <span>A little nudge</span>
          <strong>
            {data.summary.pendingResponses.toString().padStart(2, '0')}
          </strong>
          <small>
            {data.summary.pendingResponses === 1 ? 'response' : 'responses'}{' '}
            needed from you
          </small>
        </div>
        <div>
          <span>Count me in</span>
          <strong>
            {data.summary.acceptedAssignments.toString().padStart(2, '0')}
          </strong>
          <small>
            accepted{' '}
            {data.summary.acceptedAssignments === 1
              ? 'assignment'
              : 'assignments'}
          </small>
        </div>
      </section>
      <div className="dashboard-columns">
        <section>
          <div className="section-heading">
            <div>
              <p className="eyebrow">Make a little room</p>
              <h2>Coming together.</h2>
            </div>
            <Link className="text-link" to="/app/events">
              All events{' '}
              <span aria-hidden="true">
                <Arrow />
              </span>
            </Link>
          </div>
          <div className="event-list">
            {data.events.length ? (
              data.events.map((event) => (
                <EventCard
                  key={event.id}
                  event={event}
                  timeZone={context.timeZone}
                />
              ))
            ) : (
              <div className="empty-state">
                <h3>A little breathing room.</h3>
                <p>There aren’t any upcoming events right now.</p>
              </div>
            )}
          </div>
        </section>
        <aside className="response-panel">
          <div className="section-heading">
            <div>
              <p className="eyebrow">Your next small step</p>
              <h2>Can you be there?</h2>
            </div>
            <span className="outline-spark" aria-hidden="true">
              <Spark />
            </span>
          </div>
          <p className="muted panel-intro">
            A quick response helps your team plan.
          </p>
          {pending.length ? (
            pending.map((assignment) => (
              <Link
                className="pending-card"
                key={assignment.id}
                to={`/app/events/${assignment.eventId}`}
              >
                <Status value={assignment.status} />
                <h3>{assignment.eventTitle}</h3>
                <p>
                  {assignment.roleName} · {assignment.teamName}
                </p>
                <p className="muted">
                  {date(assignment.eventStartsAt, context.timeZone)}
                </p>
                <span className="text-link">
                  View your assignment{' '}
                  <span aria-hidden="true">
                    <Arrow />
                  </span>
                </span>
              </Link>
            ))
          ) : (
            <div className="all-set">
              <span aria-hidden="true">✓</span>
              <h3>You’re all caught up.</h3>
              <p>
                No assignments are waiting on your response. Thank you for
                showing up.
              </p>
            </div>
          )}
        </aside>
      </div>
    </>
  );
}
