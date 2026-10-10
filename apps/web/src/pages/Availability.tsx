import { useCallback, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { useOutletContext } from 'react-router-dom';
import { api, ApiError } from '../api';
import { localDateTimeToUtc, toLocalDateTime } from '../availabilityTime';
import { ErrorState, Loading } from '../components/Ui';
import { date, time } from '../format';
import type { DemoContext, Unavailability } from '../types';
import { useResource } from '../useResource';

export function Availability() {
  const context = useOutletContext<DemoContext>();
  const [busy, setBusy] = useState(false);
  const result = useResource(
    useCallback(
      (signal: AbortSignal) => api.availability(context.organizationId, signal),
      [context.organizationId],
    ),
    { refreshOnFocus: !busy },
  );
  const [editing, setEditing] = useState<Unavailability | null>(null);
  const [startsAt, setStartsAt] = useState('');
  const [endsAt, setEndsAt] = useState('');
  const [note, setNote] = useState('');
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [removing, setRemoving] = useState<string | null>(null);
  const startInput = useRef<HTMLInputElement>(null);
  const localZone = Intl.DateTimeFormat().resolvedOptions().timeZone;

  function resetForm() {
    setEditing(null);
    setStartsAt('');
    setEndsAt('');
    setNote('');
  }
  function edit(item: Unavailability) {
    setEditing(item);
    setStartsAt(toLocalDateTime(item.startsAt));
    setEndsAt(toLocalDateTime(item.endsAt));
    setNote(item.note ?? '');
    setError('');
    setNotice('');
    setRemoving(null);
    startInput.current?.focus();
  }
  function showError(cause: unknown) {
    if (cause instanceof ApiError && cause.status === 409) {
      resetForm();
      setRemoving(null);
      result.reload();
      setError(
        'This unavailable time changed in another session. We’ve refreshed your list. Review it before trying again.',
      );
    } else
      setError(
        cause instanceof Error
          ? cause.message
          : 'We could not save this change. Please try again.',
      );
  }
  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy) return;
    setError('');
    setNotice('');
    try {
      // Preserve the exact original instant when only the note changes (including
      // seconds and a later occurrence of a repeated daylight saving hour).
      const start =
          editing && startsAt === toLocalDateTime(editing.startsAt)
            ? editing.startsAt
            : localDateTimeToUtc(startsAt),
        end =
          editing && endsAt === toLocalDateTime(editing.endsAt)
            ? editing.endsAt
            : localDateTimeToUtc(endsAt);
      if (new Date(end) <= new Date(start))
        throw new Error('End must be later than start.');
      const input = { startsAt: start, endsAt: end, note: note.trim() || null };
      setBusy(true);
      if (editing)
        await api.updateAvailability(context.organizationId, editing, input);
      else await api.createAvailability(context.organizationId, input);
      setNotice(
        editing
          ? 'Your unavailable time has been updated.'
          : 'Your unavailable time has been saved.',
      );
      resetForm();
      result.reload();
    } catch (cause) {
      showError(cause);
    } finally {
      setBusy(false);
    }
  }
  async function remove(item: Unavailability) {
    if (busy) return;
    setBusy(true);
    setError('');
    setNotice('');
    try {
      await api.removeAvailability(context.organizationId, item);
      if (editing?.id === item.id) resetForm();
      setRemoving(null);
      setNotice('Your unavailable time has been removed.');
      result.reload();
    } catch (cause) {
      showError(cause);
    } finally {
      setBusy(false);
    }
  }
  return (
    <>
      <div className="page-heading">
        <div>
          <p className="eyebrow">Make room for life</p>
          <h1>
            My availability<span className="heading-period">.</span>
          </h1>
          <p>
            Let your team know when you can’t be there. Existing assignments
            stay in place; overlaps appear as a gentle heads-up.
          </p>
        </div>
      </div>
      {notice && (
        <p className="notice" role="status">
          {notice}
        </p>
      )}
      {error && (
        <p className="inline-error" role="alert">
          {error}
        </p>
      )}
      <div className="availability-grid">
        <section
          className="form-panel"
          aria-labelledby="availability-form-heading"
        >
          <h2 id="availability-form-heading">
            {editing ? 'Update unavailable time.' : 'Set time aside.'}
          </h2>
          <p className="form-help" id="availability-zone">
            Enter times in your device’s zone: <strong>{localZone}</strong>.
            Saved times are shown in {context.timeZone}.
          </p>
          <form
            onSubmit={(event) => void save(event)}
            aria-describedby="availability-zone availability-dst"
          >
            <fieldset disabled={busy}>
              <label htmlFor="availability-start">Start</label>
              <input
                ref={startInput}
                id="availability-start"
                type="datetime-local"
                required
                value={startsAt}
                onChange={(event) => setStartsAt(event.target.value)}
              />
              <label htmlFor="availability-end">End</label>
              <input
                id="availability-end"
                type="datetime-local"
                required
                value={endsAt}
                onChange={(event) => setEndsAt(event.target.value)}
              />
              <label htmlFor="availability-note">
                Private note <span className="muted">(optional)</span>
              </label>
              <textarea
                id="availability-note"
                maxLength={500}
                rows={3}
                value={note}
                onChange={(event) => setNote(event.target.value)}
                aria-describedby="availability-note-help"
              />
              <p className="form-help" id="availability-note-help">
                Only this demo persona can read the note. Leaders see the time
                conflict, not the note. Please use fictional details.
              </p>
              <p className="form-help" id="availability-dst">
                During the repeated daylight saving hour, the earlier occurrence
                is used. Times skipped when clocks move forward are rejected.
              </p>
              <div className="form-actions">
                <button className="button button-small" type="submit">
                  {busy
                    ? 'Saving…'
                    : editing
                      ? 'Update unavailable time'
                      : 'Save unavailable time'}
                </button>
                {editing && (
                  <button
                    className="button button-outline button-small"
                    type="button"
                    onClick={resetForm}
                  >
                    Cancel edit
                  </button>
                )}
              </div>
            </fieldset>
          </form>
        </section>
        <section aria-labelledby="availability-list-heading">
          <div className="section-heading">
            <div>
              <p className="eyebrow">A little breathing room</p>
              <h2 id="availability-list-heading">Your unavailable time.</h2>
            </div>
          </div>
          {result.loading ? (
            <Loading label="Loading your unavailable time…" />
          ) : result.error ? (
            <ErrorState error={result.error} onRetry={result.reload} />
          ) : result.data?.length ? (
            <div className="availability-list">
              {result.data.map((item) => (
                <article
                  className="availability-card"
                  key={item.id}
                  data-testid="availability-entry"
                >
                  <span className="pill">Unavailable</span>
                  <h3>
                    <time dateTime={item.startsAt}>
                      {date(item.startsAt, context.timeZone)} ·{' '}
                      {time(item.startsAt, context.timeZone)}
                    </time>
                  </h3>
                  <p>
                    to{' '}
                    <time dateTime={item.endsAt}>
                      {date(item.endsAt, context.timeZone)} ·{' '}
                      {time(item.endsAt, context.timeZone)}
                    </time>
                  </p>
                  {item.note && <p className="private-note">{item.note}</p>}
                  <div className="form-actions">
                    {removing === item.id ? (
                      <>
                        <span className="form-help">
                          Remove this unavailable time?
                        </span>
                        <button
                          disabled={busy}
                          className="button button-outline button-small"
                          onClick={() => void remove(item)}
                        >
                          Confirm removal
                        </button>
                        <button
                          disabled={busy}
                          className="quiet-link"
                          onClick={() => setRemoving(null)}
                        >
                          Keep it
                        </button>
                      </>
                    ) : (
                      <>
                        <button
                          disabled={busy}
                          className="button button-outline button-small"
                          onClick={() => edit(item)}
                        >
                          Edit
                        </button>
                        <button
                          disabled={busy}
                          className="quiet-link"
                          onClick={() => setRemoving(item.id)}
                        >
                          Remove
                        </button>
                      </>
                    )}
                  </div>
                </article>
              ))}
            </div>
          ) : (
            <div className="empty-state">
              <h3>No unavailable time yet.</h3>
              <p>Add a date and time range when you can’t volunteer.</p>
            </div>
          )}
        </section>
      </div>
    </>
  );
}
