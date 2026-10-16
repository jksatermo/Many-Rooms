import { useCallback, useId, useState } from 'react';
import { api, ApiError } from '../api';
import type { DemoContext, StaffingRequirement } from '../types';
import { useResource } from '../useResource';
import { ErrorState, Loading } from './Ui';
import { message } from './LeaderTools';

export function Staffing({
  context,
  eventId,
}: {
  context: DemoContext;
  eventId: string;
}) {
  const [busy, setBusy] = useState(false);
  const [editing, setEditing] = useState<StaffingRequirement | null>(null);
  const [teamId, setTeam] = useState('');
  const [role, setRole] = useState('');
  const [count, setCount] = useState(1);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [remove, setRemove] = useState<StaffingRequirement | null>(null);
  const id = useId();
  const requirements = useResource(
    useCallback(
      (signal: AbortSignal) =>
        api.staffing(context.organizationId, eventId, signal),
      [context.organizationId, eventId],
    ),
    { refreshOnFocus: !busy && !editing },
  );
  const teams = useResource(
    useCallback(
      (signal: AbortSignal) => api.teams(context.organizationId, signal),
      [context.organizationId],
    ),
    { refreshOnFocus: false },
  );
  const reset = () => {
    setEditing(null);
    setTeam('');
    setRole('');
    setCount(1);
  };
  async function save(deleting?: StaffingRequirement) {
    setBusy(true);
    setError('');
    try {
      if (deleting) {
        await api.removeStaffing(context.organizationId, deleting);
        setRemove(null);
        setNotice(
          'Staffing requirement removed. Existing assignments are unchanged.',
        );
      } else {
        const input = {
          teamId,
          roleName: role.trim() || null,
          desiredCount: count,
        };
        if (editing)
          await api.updateStaffing(context.organizationId, editing, input);
        else await api.createStaffing(context.organizationId, eventId, input);
        setNotice('Staffing requirement saved.');
        reset();
      }
      requirements.reload();
    } catch (cause) {
      setError(message(cause));
      if (cause instanceof ApiError && cause.status === 409) {
        reset();
        setRemove(null);
        requirements.reload();
      }
    } finally {
      setBusy(false);
    }
  }
  return (
    <section className="scheduling-section" aria-labelledby="staffing-heading">
      <div className="section-heading">
        <div>
          <p className="eyebrow">A little clarity for your team</p>
          <h2 id="staffing-heading">The help you need.</h2>
        </div>
      </div>
      <p className="form-help">
        Pending and accepted volunteers count toward coverage. Declined,
        cancelled, and duplicate assignments for the same member do not fill
        extra spots. Conflicts are advisory.
      </p>
      {notice && (
        <p role="status" className="notice">
          {notice}
        </p>
      )}
      {requirements.loading ? (
        <Loading label="Loading staffing needs…" />
      ) : requirements.error ? (
        <ErrorState error={requirements.error} onRetry={requirements.reload} />
      ) : requirements.data!.length ? (
        <div className="staffing-grid">
          {requirements.data!.map((item) => (
            <article
              className="availability-card"
              key={item.id}
              data-testid="staffing-requirement"
            >
              <p className="eyebrow">{item.teamName}</p>
              <h3>{item.roleName || 'Whole team'}</h3>
              <p className="coverage-count">
                <strong>
                  {item.assigned} / {item.desiredCount}
                </strong>{' '}
                assigned · {item.openSpots} open{' '}
                {item.openSpots === 1 ? 'spot' : 'spots'}
              </p>
              <p>
                {item.accepted} accepted · {item.pending} awaiting response ·{' '}
                {item.declined} declined · {item.conflicted} in conflict
              </p>
              <div className="form-actions">
                <button
                  disabled={busy}
                  className="quiet-link"
                  onClick={() => {
                    setEditing(item);
                    setTeam(item.teamId);
                    setRole(item.roleName || '');
                    setCount(item.desiredCount);
                    setError('');
                  }}
                >
                  Edit need
                </button>
                <button
                  disabled={busy}
                  className="quiet-link"
                  onClick={() => setRemove(item)}
                >
                  Remove need
                </button>
              </div>
              {remove?.id === item.id && (
                <div className="confirm-panel">
                  <p>Remove this staffing target? Assignments will remain.</p>
                  <button
                    disabled={busy}
                    className="button button-small"
                    onClick={() => void save(item)}
                  >
                    Confirm removal
                  </button>
                  <button
                    disabled={busy}
                    className="quiet-link"
                    onClick={() => setRemove(null)}
                  >
                    Keep need
                  </button>
                </div>
              )}
            </article>
          ))}
        </div>
      ) : (
        <p className="empty-state">
          No staffing targets yet. Add a team or role below.
        </p>
      )}
      <form
        className="form-panel staffing-form"
        onSubmit={(e) => {
          e.preventDefault();
          void save();
        }}
      >
        <h3>{editing ? 'Edit staffing need' : 'Add a staffing need'}</h3>
        <fieldset disabled={busy || teams.loading || !!teams.error}>
          <label htmlFor={`${id}-team`}>Staffing team</label>
          <select
            id={`${id}-team`}
            required
            disabled={!!editing}
            value={teamId}
            onChange={(e) => setTeam(e.target.value)}
          >
            <option value="">Choose a team</option>
            {teams.data?.map((team) => (
              <option key={team.id} value={team.id}>
                {team.name}
              </option>
            ))}
          </select>
          <label htmlFor={`${id}-role`}>Staffing role (optional)</label>
          <input
            id={`${id}-role`}
            maxLength={120}
            placeholder="Leave blank for a whole-team target"
            value={role}
            onChange={(e) => setRole(e.target.value)}
          />
          <label htmlFor={`${id}-count`}>Volunteers needed</label>
          <input
            id={`${id}-count`}
            type="number"
            min={1}
            max={100}
            required
            value={count}
            onChange={(e) => setCount(e.target.valueAsNumber)}
          />
          <p className="form-help">
            Use either one whole-team target or individual role targets for a
            team; combining them would double count coverage.
          </p>
          <div className="form-actions">
            <button className="button button-small" type="submit">
              {busy
                ? 'Saving…'
                : editing
                  ? 'Update staffing need'
                  : 'Save staffing need'}
            </button>
            {editing && (
              <button type="button" className="quiet-link" onClick={reset}>
                Cancel edit
              </button>
            )}
          </div>
        </fieldset>
        {teams.error && (
          <ErrorState error={teams.error} onRetry={teams.reload} />
        )}{' '}
        {error && (
          <p role="alert" className="inline-error">
            {error}
          </p>
        )}
      </form>
    </section>
  );
}
