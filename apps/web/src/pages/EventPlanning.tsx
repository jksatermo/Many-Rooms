import { useCallback, useState } from 'react';
import { Link, useNavigate, useOutletContext } from 'react-router-dom';
import { api } from '../api';
import { localDateTimeToUtc } from '../availabilityTime';
import { LeaderOnly, message } from '../components/LeaderTools';
import { ErrorState, Loading } from '../components/Ui';
import type { DemoContext, EventTemplate, GenerationResult } from '../types';
import { useResource } from '../useResource';

const days = [
  'Sunday',
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
];
export function EventPlanning() {
  return (
    <LeaderOnly>
      <Planning />
    </LeaderOnly>
  );
}
function Planning() {
  const context = useOutletContext<DemoContext>();
  const navigate = useNavigate();
  const [weekly, setWeekly] = useState(false);
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [location, setLocation] = useState('');
  const [start, setStart] = useState('');
  const [end, setEnd] = useState('');
  const [day, setDay] = useState(0);
  const [clock, setClock] = useState('10:00');
  const [duration, setDuration] = useState(90);
  const [startsOn, setStartsOn] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const templates = useResource(
    useCallback(
      (signal: AbortSignal) =>
        api.eventTemplates(context.organizationId, signal),
      [context.organizationId],
    ),
    { refreshOnFocus: !busy },
  );
  async function save() {
    setBusy(true);
    setError('');
    try {
      const common = {
        title: title.trim(),
        description: description.trim(),
        location: location.trim(),
      };
      if (weekly) {
        await api.createEventTemplate(context.organizationId, {
          ...common,
          dayOfWeek: day,
          localStartTime: clock,
          durationMinutes: duration,
          startsOn,
        });
        setNotice(
          'Weekly template saved. Choose an end date below to generate events.',
        );
        setTitle('');
        setDescription('');
        setLocation('');
        templates.reload();
      } else {
        const startsAt = localDateTimeToUtc(start);
        const endsAt = localDateTimeToUtc(end);
        if (new Date(endsAt) <= new Date(startsAt))
          throw new Error('End must be later than start.');
        const event = await api.createEvent(context.organizationId, {
          ...common,
          startsAt,
          endsAt,
        });
        navigate(`/app/leader/events/${event.id}`);
      }
    } catch (cause) {
      setError(message(cause));
    } finally {
      setBusy(false);
    }
  }
  return (
    <>
      <Link className="back-link" to="/app/events">
        ← All events
      </Link>
      <div className="page-heading">
        <div>
          <p className="eyebrow">Demo leader · {context.organizationName}</p>
          <h1>Make space to gather.</h1>
          <p>Add a gathering once, or plan a short run of weekly events.</p>
        </div>
      </div>
      {notice && (
        <p role="status" className="notice">
          {notice}
        </p>
      )}
      <div className="leader-grid">
        <section className="form-panel">
          <h2>Plan an event.</h2>
          <div
            className="form-actions"
            role="group"
            aria-label="Event frequency"
          >
            <button
              type="button"
              disabled={busy}
              className={`button button-small ${weekly ? 'button-outline' : ''}`}
              aria-pressed={!weekly}
              onClick={() => {
                setWeekly(false);
                setError('');
              }}
            >
              One-time event
            </button>
            <button
              type="button"
              disabled={busy}
              className={`button button-small ${weekly ? '' : 'button-outline'}`}
              aria-pressed={weekly}
              onClick={() => {
                setWeekly(true);
                setError('');
              }}
            >
              Weekly template
            </button>
          </div>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              void save();
            }}
          >
            <fieldset disabled={busy}>
              <label htmlFor="plan-title">Event title</label>
              <input
                id="plan-title"
                required
                maxLength={160}
                value={title}
                onChange={(e) => setTitle(e.target.value)}
              />
              <label htmlFor="plan-description">Description</label>
              <textarea
                id="plan-description"
                rows={3}
                maxLength={4000}
                value={description}
                onChange={(e) => setDescription(e.target.value)}
              />
              <label htmlFor="plan-location">Location</label>
              <input
                id="plan-location"
                required
                maxLength={240}
                value={location}
                onChange={(e) => setLocation(e.target.value)}
              />
              {weekly ? (
                <>
                  <p className="form-help">
                    Weekly times use {context.timeZone}, your community’s time
                    zone.
                  </p>
                  <label htmlFor="plan-day">Day of week</label>
                  <select
                    id="plan-day"
                    value={day}
                    onChange={(e) => setDay(Number(e.target.value))}
                  >
                    {days.map((name, i) => (
                      <option key={name} value={i}>
                        {name}
                      </option>
                    ))}
                  </select>
                  <label htmlFor="plan-clock">Local start time</label>
                  <input
                    id="plan-clock"
                    type="time"
                    required
                    value={clock}
                    onChange={(e) => setClock(e.target.value)}
                  />
                  <label htmlFor="plan-duration">Duration in minutes</label>
                  <input
                    id="plan-duration"
                    type="number"
                    min={15}
                    max={720}
                    required
                    value={duration}
                    onChange={(e) => setDuration(e.target.valueAsNumber)}
                  />
                  <label htmlFor="plan-starts">Starting on or after</label>
                  <input
                    id="plan-starts"
                    type="date"
                    required
                    value={startsOn}
                    onChange={(e) => setStartsOn(e.target.value)}
                  />
                </>
              ) : (
                <>
                  <p className="form-help">
                    Enter times in your device’s zone:{' '}
                    {Intl.DateTimeFormat().resolvedOptions().timeZone}. The
                    event will display in {context.timeZone}.
                  </p>
                  <label htmlFor="plan-start">Event start</label>
                  <input
                    id="plan-start"
                    type="datetime-local"
                    required
                    value={start}
                    onChange={(e) => setStart(e.target.value)}
                  />
                  <label htmlFor="plan-end">Event end</label>
                  <input
                    id="plan-end"
                    type="datetime-local"
                    required
                    value={end}
                    onChange={(e) => setEnd(e.target.value)}
                  />
                </>
              )}
              {error && (
                <p role="alert" className="inline-error">
                  {error}
                </p>
              )}
              <button className="button button-small">
                {busy
                  ? 'Saving…'
                  : weekly
                    ? 'Save weekly template'
                    : 'Create event'}
              </button>
            </fieldset>
          </form>
        </section>
        <section aria-labelledby="templates-heading">
          <h2 id="templates-heading">Weekly rhythms.</h2>
          <p className="form-help">
            Generate at most 12 weeks at a time. Existing occurrences are
            preserved. Volunteers and staffing targets are not copied to new
            events.
          </p>
          {templates.loading ? (
            <Loading label="Loading weekly templates…" />
          ) : templates.error ? (
            <ErrorState error={templates.error} onRetry={templates.reload} />
          ) : templates.data!.length ? (
            templates.data!.map((template) => (
              <GenerateTemplate
                key={template.id}
                template={template}
                organizationId={context.organizationId}
              />
            ))
          ) : (
            <p className="empty-state">No weekly templates yet.</p>
          )}
        </section>
      </div>
    </>
  );
}
function GenerateTemplate({
  template,
  organizationId,
}: {
  template: EventTemplate;
  organizationId: string;
}) {
  const [through, setThrough] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [result, setResult] = useState<GenerationResult | null>(null);
  async function generate() {
    setBusy(true);
    setError('');
    setResult(null);
    try {
      setResult(await api.generateEvents(organizationId, template.id, through));
    } catch (cause) {
      setError(message(cause));
    } finally {
      setBusy(false);
    }
  }
  return (
    <article className="availability-card template-card">
      <h3>{template.title}</h3>
      <p>
        {days[template.dayOfWeek]} · {template.localStartTime} ·{' '}
        {template.timeZone}
      </p>
      <p>
        {template.durationMinutes} minutes · from {template.startsOn} ·{' '}
        {template.location}
      </p>
      <form
        className="form-panel compact-form"
        onSubmit={(e) => {
          e.preventDefault();
          void generate();
        }}
      >
        <fieldset disabled={busy}>
          <label htmlFor={`through-${template.id}`}>
            Generate through date
          </label>
          <input
            id={`through-${template.id}`}
            type="date"
            required
            min={template.startsOn}
            value={through}
            onChange={(e) => setThrough(e.target.value)}
          />
          <button className="button button-small">
            {busy ? 'Generating…' : 'Generate events'}
          </button>
        </fieldset>
      </form>
      {error && (
        <p role="alert" className="inline-error">
          {error}
        </p>
      )}
      {result && (
        <div className="generation-result" role="status">
          <p>
            {result.createdEvents.length} events created ·{' '}
            {result.existingCount} already exist.
          </p>
          {result.skippedDates.length > 0 && (
            <p>
              Skipped local dates: {result.skippedDates.join(', ')}. A local
              clock change can make the requested start time unavailable.
            </p>
          )}
          {result.createdEvents.map((event) => (
            <Link
              className="text-link"
              key={event.id}
              to={`/app/leader/events/${event.id}`}
            >
              Open {event.title} ·{' '}
              {new Intl.DateTimeFormat('en-US', {
                month: 'short',
                day: 'numeric',
                timeZone: template.timeZone,
              }).format(new Date(event.startsAt))}{' '}
              →
            </Link>
          ))}
        </div>
      )}
    </article>
  );
}
