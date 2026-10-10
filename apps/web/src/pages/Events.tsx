import { useCallback } from 'react';
import { useOutletContext } from 'react-router-dom';
import { api } from '../api';
import { useResource } from '../useResource';
import type { DemoContext } from '../types';
import { ErrorState, EventCard, Loading } from '../components/Ui';

export function Events() {
  const context = useOutletContext<DemoContext>();
  const result = useResource(
    useCallback(
      (signal: AbortSignal) => api.events(context.organizationId, signal),
      [context.organizationId],
    ),
  );
  return (
    <>
      <div className="page-heading">
        <div>
          <p className="eyebrow">{context.organizationName}</p>
          <h1>
            Good things ahead<span className="heading-period">.</span>
          </h1>
          <p>
            {context.role === 'Leader'
              ? 'Demo leader schedule · See responses, review conflicts, and make room for your team.'
              : 'A place to gather, connect, and lend a hand.'}
          </p>
        </div>
      </div>
      <section className="events-page">
        <div className="section-heading">
          <h2>Upcoming events</h2>
          <span className="muted">Community calendar</span>
        </div>
        {result.loading ? (
          <Loading label="Loading upcoming events…" />
        ) : result.error ? (
          <ErrorState error={result.error} onRetry={result.reload} />
        ) : result.data?.length ? (
          <div className="event-list">
            {result.data.map((event) => (
              <EventCard
                key={event.id}
                event={event}
                timeZone={context.timeZone}
                leader={context.role === 'Leader'}
              />
            ))}
          </div>
        ) : (
          <div className="empty-state">
            <h3>The calendar is clear for now.</h3>
            <p>Upcoming community events will appear here.</p>
          </div>
        )}
      </section>
    </>
  );
}
