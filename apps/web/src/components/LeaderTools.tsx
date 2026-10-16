import { useCallback, useId, useState } from 'react';
import type { ReactNode } from 'react';
import { Link, useOutletContext } from 'react-router-dom';
import { api, ApiError } from '../api';
import type {
  Assignment,
  AssignmentHistory,
  DemoContext,
  ScheduleConflict,
  TeamMember,
} from '../types';
import { useResource } from '../useResource';
import { date, time } from '../format';
import { Conflicts } from './Conflicts';
import { ErrorState, Loading } from './Ui';

export function LeaderOnly({ children }: { children: ReactNode }) {
  const context = useOutletContext<DemoContext>();
  return context.role === 'Leader' ? (
    children
  ) : (
    <div className="state-box">
      <h1>This is the demo leader view.</h1>
      <p>Choose the explicitly labeled leader persona to explore scheduling.</p>
      <Link className="button" to="/demo">
        Choose a demo persona
      </Link>
    </div>
  );
}
export const message = (cause: unknown) =>
  cause instanceof Error
    ? cause.message
    : 'This change could not be saved. Please try again.';

export function HistoryList({
  entries,
  timeZone,
}: {
  entries: AssignmentHistory[];
  timeZone: string;
}) {
  return entries.length ? (
    <ol className="history-list">
      {entries.map((entry) => (
        <li key={entry.id}>
          <strong>
            {entry.action} · {entry.volunteerName}
          </strong>
          <p>
            {entry.previousVolunteerName &&
            entry.previousVolunteerName !== entry.volunteerName
              ? `${entry.previousVolunteerName} → `
              : ''}
            {entry.previousRoleName && entry.previousRoleName !== entry.roleName
              ? `${entry.previousRoleName} → `
              : ''}
            {entry.roleName}
          </p>
          <small>
            {entry.actorName ||
              (entry.action === 'Imported'
                ? 'Existing data'
                : 'Actor not recorded')}{' '}
            · {date(entry.occurredAt, timeZone)} ·{' '}
            {time(entry.occurredAt, timeZone)} · version{' '}
            {entry.assignmentVersion}
          </small>
        </li>
      ))}
    </ol>
  ) : (
    <p className="form-help">No assignment changes recorded yet.</p>
  );
}
export function EventHistory({
  context,
  eventId,
}: {
  context: DemoContext;
  eventId: string;
}) {
  const result = useResource(
    useCallback(
      (signal: AbortSignal) =>
        api.eventHistory(context.organizationId, eventId, signal),
      [context.organizationId, eventId],
    ),
  );
  return (
    <section className="scheduling-section" aria-labelledby="history-heading">
      <h2 id="history-heading">How the schedule changed.</h2>
      {result.loading ? (
        <Loading label="Loading assignment history…" />
      ) : result.error ? (
        <ErrorState error={result.error} onRetry={result.reload} />
      ) : (
        <HistoryList entries={result.data!} timeZone={context.timeZone} />
      )}
    </section>
  );
}

export function AssignmentEditor({
  context,
  assignment,
  members,
  onSaved,
  onClose,
  candidatesOnly = false,
}: {
  context: DemoContext;
  assignment: Assignment;
  members: TeamMember[];
  onSaved: (updated: Assignment) => void;
  onClose: () => void;
  candidatesOnly?: boolean;
}) {
  const id = useId();
  const [teamMemberId, setMember] = useState(
    candidatesOnly ? '' : assignment.teamMemberId,
  );
  const [roleName, setRole] = useState(assignment.roleName);
  const [preview, setPreview] = useState<ScheduleConflict[] | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [stale, setStale] = useState(false);
  const [confirmCancel, setConfirmCancel] = useState(false);
  const candidates = useResource(
    useCallback(
      (signal: AbortSignal) =>
        candidatesOnly
          ? api.replacementCandidates(
              context.organizationId,
              assignment.id,
              signal,
            )
          : Promise.resolve([]),
      [candidatesOnly, context.organizationId, assignment.id],
    ),
    { refreshOnFocus: false },
  );
  async function save(cancel = false) {
    setBusy(true);
    setError('');
    try {
      if (cancel)
        onSaved(await api.cancelAssignment(context.organizationId, assignment));
      else if (preview === null)
        setPreview(
          (
            await api.previewAssignmentEdit(
              context.organizationId,
              assignment,
              { teamMemberId, roleName: roleName.trim() },
            )
          ).conflicts,
        );
      else
        onSaved(
          await api.updateAssignment(context.organizationId, assignment, {
            teamMemberId,
            roleName: roleName.trim(),
          }),
        );
    } catch (cause) {
      setError(message(cause));
      setPreview(null);
      if (cause instanceof ApiError && cause.status === 409) setStale(true);
    } finally {
      setBusy(false);
    }
  }
  return (
    <div className="form-panel assignment-editor">
      <h3>{candidatesOnly ? 'Choose a replacement' : 'Edit assignment'}</h3>
      <p className="form-help">
        A volunteer or role change resets the response to awaiting response.
        Previous responses remain in history. No notification is sent.
      </p>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          void save();
        }}
      >
        <fieldset disabled={busy || stale}>
          <label htmlFor={`${id}-member`}>
            {candidatesOnly ? 'Suggested volunteer' : 'Assigned volunteer'}
          </label>
          {candidates.loading ? (
            <Loading label="Finding available team members…" />
          ) : candidates.error ? (
            <ErrorState error={candidates.error} onRetry={candidates.reload} />
          ) : (
            <>
              <select
                id={`${id}-member`}
                required
                value={teamMemberId}
                onChange={(e) => {
                  setMember(e.target.value);
                  setPreview(null);
                }}
              >
                <option value="">Choose a team member</option>
                {candidatesOnly
                  ? candidates.data!.map((member) => (
                      <option
                        key={member.teamMemberId}
                        value={member.teamMemberId}
                      >
                        {member.volunteerName} ·{' '}
                        {member.upcomingAssignmentCount} upcoming
                        {member.roleMatch ? ' · position match' : ''}
                      </option>
                    ))
                  : members
                      .filter((member) => member.teamId === assignment.teamId)
                      .map((member) => (
                        <option key={member.id} value={member.id}>
                          {member.volunteerName}
                          {member.positionName
                            ? ` · ${member.positionName}`
                            : ''}
                        </option>
                      ))}
              </select>
              {candidatesOnly && (
                <p className="form-help">
                  {candidates.data!.length
                    ? 'Suggestions have no detected time conflicts. Position matches come first, then fewer assignments in the following 28 days. A position match is a preference, not a qualification.'
                    : 'No available replacement was found in this team. Review the team or adjust the schedule manually.'}{' '}
                  Availability can change before saving.
                </p>
              )}
            </>
          )}
          <label htmlFor={`${id}-role`}>Assignment role</label>
          <input
            id={`${id}-role`}
            value={roleName}
            maxLength={120}
            required
            onChange={(e) => {
              setRole(e.target.value);
              setPreview(null);
            }}
          />
          {preview !== null && (
            <div className="assignment-preview" role="status">
              <h3>Change preview</h3>
              {preview.length ? (
                <Conflicts conflicts={preview} timeZone={context.timeZone} />
              ) : (
                <p>No schedule conflicts found.</p>
              )}
              <p className="form-help">
                Conflicts are advisory. The server checks the current schedule
                again on save.
              </p>
            </div>
          )}
          <div className="form-actions">
            <button
              className="button button-small"
              disabled={!teamMemberId || !roleName.trim() || !!candidates.error}
              type="submit"
            >
              {busy
                ? 'Working…'
                : preview === null
                  ? 'Preview change'
                  : 'Save change'}
            </button>
            <button
              type="button"
              className="button button-outline button-small"
              onClick={() => setConfirmCancel(true)}
            >
              Cancel assignment
            </button>
          </div>
          {confirmCancel && (
            <div className="confirm-panel">
              <p>
                Cancel this assignment? It will remain visible in history and
                stop reserving this volunteer’s time.
              </p>
              <button
                className="button button-small"
                type="button"
                onClick={() => void save(true)}
              >
                Confirm cancellation
              </button>
              <button
                className="quiet-link"
                type="button"
                onClick={() => setConfirmCancel(false)}
              >
                Keep assignment
              </button>
            </div>
          )}
        </fieldset>
      </form>
      {error && (
        <p role="alert" className="inline-error">
          {error}
          {stale
            ? ' Close this editor and refresh the schedule before trying again.'
            : ''}
        </p>
      )}
      <button
        disabled={busy}
        className="quiet-link editor-close"
        onClick={onClose}
      >
        Close editor
      </button>
    </div>
  );
}
