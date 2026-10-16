import { useCallback, useState } from 'react';
import type { FormEvent } from 'react';
import { Link, useOutletContext, useParams } from 'react-router-dom';
import { api } from '../api';
import { Conflicts } from '../components/Conflicts';
import { ErrorState, Loading, Status } from '../components/Ui';
import { date, time } from '../format';
import type { Assignment, DemoContext, ScheduleConflict } from '../types';
import { useResource } from '../useResource';
import { AssignmentEditor, EventHistory } from '../components/LeaderTools';
import { Staffing } from '../components/Staffing';

export function LeaderEvent() {
  const context = useOutletContext<DemoContext>();
  if (context.role !== 'Leader')
    return (
      <div className="state-box">
        <h1>This is the demo leader view.</h1>
        <p>
          Your volunteer session can view your own assignments and availability.
          Switch personas at demo entry to explore the leader schedule.
        </p>
        <Link className="button" to="/demo">
          Choose a demo persona
        </Link>
      </div>
    );
  return <LeaderSchedule context={context} />;
}

function LeaderSchedule({ context }: { context: DemoContext }) {
  const { eventId = '' } = useParams();
  const [submitting, setSubmitting] = useState(false);
  const [editing, setEditing] = useState<Assignment | null>(null);
  const [historyOpen, setHistoryOpen] = useState(false);
  const [scheduleRevision, setScheduleRevision] = useState(0);
  const event = useResource(
    useCallback(
      (signal: AbortSignal) =>
        api.leaderEvent(context.organizationId, eventId, signal),
      [context.organizationId, eventId],
    ),
    { refreshOnFocus: !submitting && !editing },
  );
  const members = useResource(
    useCallback(
      (signal: AbortSignal) => api.teamMembers(context.organizationId, signal),
      [context.organizationId],
    ),
    { refreshOnFocus: false },
  );
  const [notice, setNotice] = useState('');
  const added = (assignment: Assignment) => {
    setNotice(
      `Assignment saved for ${assignment.volunteerName}. ${assignment.conflicts.length ? 'Schedule conflicts are shown below; the assignment is awaiting a response.' : 'The assignment is awaiting a response.'}`,
    );
    setScheduleRevision((value) => value + 1);
    event.reload();
  };
  if (event.loading) return <Loading label="Loading the leader schedule…" />;
  if (event.error)
    return <ErrorState error={event.error} onRetry={event.reload} />;
  const data = event.data!;
  const statuses = [
    {
      label: 'Accepted',
      count: data.assignments.filter(
        (a) => !a.isCancelled && a.status === 'Accepted',
      ).length,
    },
    {
      label: 'Awaiting response',
      count: data.assignments.filter(
        (a) => !a.isCancelled && a.status === 'Pending',
      ).length,
    },
    {
      label: 'Declined',
      count: data.assignments.filter(
        (a) => !a.isCancelled && a.status === 'Declined',
      ).length,
    },
    {
      label: 'In conflict',
      count: data.assignments.filter(
        (a) => !a.isCancelled && a.conflicts.length > 0,
      ).length,
    },
  ];
  return (
    <>
      <Link className="back-link" to="/app/events">
        ← All events
      </Link>
      <div className="event-hero">
        <p className="eyebrow">
          Demo leader schedule · {context.organizationName}
        </p>
        <h1>{data.title}</h1>
        <p className="event-description">{data.description}</p>
        <div className="event-metadata">
          <span>
            <span>
              {' '}
              {date(data.startsAt, context.timeZone)}
              <small>
                {time(data.startsAt, context.timeZone)} –{' '}
                {time(data.endsAt, context.timeZone)}
              </small>
            </span>
          </span>
          <span>{data.location}</span>
        </div>
      </div>
      <section
        className="summary-grid leader-summary"
        aria-label="Assignment overview"
      >
        {statuses.map((status) => (
          <div key={status.label}>
            <span>{status.label}</span>
            <strong>{status.count.toString().padStart(2, '0')}</strong>
          </div>
        ))}
      </section>
      {notice && (
        <p className="notice" role="status">
          {notice}
        </p>
      )}
      <Staffing
        key={`staffing-${scheduleRevision}`}
        context={context}
        eventId={eventId}
      />
      <div className="leader-grid">
        <section aria-labelledby="leader-assignments-heading">
          <div className="section-heading">
            <div>
              <p className="eyebrow">Help your team plan</p>
              <h2 id="leader-assignments-heading">Everyone’s part.</h2>
            </div>
          </div>
          <p className="form-help">
            Conflicts are advisory and can appear alongside an accepted or
            pending response. Private availability notes are never shown here.
          </p>
          {data.assignments.length ? (
            data.assignments.map((assignment) => (
              <article
                className="assignment"
                key={assignment.id}
                data-testid="leader-assignment"
                data-status={assignment.status}
              >
                <div className="assignment-main">
                  <div className="assignment-person">
                    <h3>{assignment.volunteerName}</h3>
                    <p>
                      {assignment.roleName} · {assignment.teamName}
                    </p>
                  </div>
                  <Status
                    value={assignment.status}
                    cancelled={assignment.isCancelled}
                  />
                </div>
                <Conflicts
                  conflicts={assignment.conflicts}
                  timeZone={context.timeZone}
                />
                {!assignment.conflicts.length && (
                  <p className="no-conflict">
                    {assignment.isCancelled
                      ? 'Cancelled assignments no longer reserve time or fill staffing needs.'
                      : assignment.status === 'Declined'
                        ? 'Declined assignments don’t reserve this volunteer’s time.'
                        : 'No schedule conflicts found.'}
                  </p>
                )}
                {!assignment.isCancelled &&
                  new Date(data.startsAt) > new Date() && (
                    <div className="assignment-tools">
                      <button
                        className="quiet-link"
                        disabled={
                          editing !== null && editing.id !== assignment.id
                        }
                        onClick={() => setEditing(assignment)}
                      >
                        Edit or cancel assignment
                      </button>
                    </div>
                  )}
                {editing?.id === assignment.id && (
                  <AssignmentEditor
                    key={`${assignment.id}-${assignment.version}`}
                    context={context}
                    assignment={editing}
                    members={members.data || []}
                    onClose={() => {
                      setEditing(null);
                      event.reload();
                    }}
                    onSaved={(updated) => {
                      setEditing(null);
                      setNotice(
                        updated.isCancelled
                          ? 'Assignment cancelled. History is preserved.'
                          : `Assignment updated for ${updated.volunteerName}. Response reset to awaiting response.`,
                      );
                      setScheduleRevision((value) => value + 1);
                      event.reload();
                    }}
                  />
                )}
              </article>
            ))
          ) : (
            <div className="empty-state">
              <h3>A team is still coming together.</h3>
              <p>Add the first volunteer assignment for this event.</p>
            </div>
          )}
        </section>
        <section
          className="form-panel"
          aria-labelledby="new-assignment-heading"
        >
          <h2 id="new-assignment-heading">Make room on the team.</h2>
          <p className="form-help">
            Choose an existing team member. Preview the schedule before saving a
            new assignment.
          </p>
          {members.loading ? (
            <Loading label="Loading team members…" />
          ) : members.error ? (
            <ErrorState error={members.error} onRetry={members.reload} />
          ) : !members.data?.length ? (
            <p>No team members are available to assign yet.</p>
          ) : new Date(data.startsAt) <= new Date() ? (
            <p>This event has started; new assignments are closed.</p>
          ) : (
            <AssignmentForm
              key={eventId}
              context={context}
              eventId={eventId}
              members={members.data}
              onAdded={added}
              onBusyChange={setSubmitting}
            />
          )}
        </section>
      </div>
      <div className="form-actions">
        <button
          className="button button-outline button-small"
          aria-expanded={historyOpen}
          onClick={() => setHistoryOpen(!historyOpen)}
        >
          {historyOpen ? 'Hide assignment history' : 'View assignment history'}
        </button>
      </div>
      {historyOpen && (
        <EventHistory
          key={`history-${scheduleRevision}`}
          context={context}
          eventId={eventId}
        />
      )}
    </>
  );
}

function AssignmentForm({
  context,
  eventId,
  members,
  onAdded,
  onBusyChange,
}: {
  context: DemoContext;
  eventId: string;
  members: Awaited<ReturnType<typeof api.teamMembers>>;
  onAdded: (assignment: Assignment) => void;
  onBusyChange: (busy: boolean) => void;
}) {
  const [teamMemberId, setTeamMemberId] = useState('');
  const [roleName, setRoleName] = useState('');
  const [preview, setPreview] = useState<ScheduleConflict[] | null>(null);
  const [busy, setBusy] = useState<'preview' | 'save' | null>(null);
  const [error, setError] = useState('');
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy) return;
    const input = { teamMemberId, roleName: roleName.trim() };
    if (!input.teamMemberId || !input.roleName) {
      setError('Choose a team member and enter a role.');
      return;
    }
    setError('');
    onBusyChange(true);
    setBusy(preview === null ? 'preview' : 'save');
    try {
      if (preview === null) {
        const result = await api.previewAssignment(
          context.organizationId,
          eventId,
          input,
        );
        setPreview(result.conflicts);
      } else {
        const saved = await api.createAssignment(
          context.organizationId,
          eventId,
          input,
        );
        onAdded(saved);
      }
    } catch (cause) {
      setError(
        cause instanceof Error
          ? cause.message
          : 'The assignment could not be saved. Please try again.',
      );
      setPreview(null);
    } finally {
      setBusy(null);
      onBusyChange(false);
    }
  }
  return (
    <form onSubmit={(event) => void submit(event)}>
      <fieldset disabled={busy !== null}>
        <label htmlFor="assignment-member">Team member</label>
        <select
          id="assignment-member"
          required
          value={teamMemberId}
          onChange={(event) => {
            setTeamMemberId(event.target.value);
            setPreview(null);
          }}
        >
          <option value="">Choose a volunteer and team</option>
          {members.map((member) => (
            <option key={member.id} value={member.id}>
              {member.volunteerName} · {member.teamName}
            </option>
          ))}
        </select>
        <label htmlFor="assignment-role">Role</label>
        <input
          id="assignment-role"
          required
          maxLength={120}
          value={roleName}
          onChange={(event) => {
            setRoleName(event.target.value);
            setPreview(null);
          }}
          placeholder="For example, welcome table"
        />
        {preview !== null && (
          <div className="assignment-preview" role="status">
            <h3>Schedule preview</h3>
            {preview.length ? (
              <Conflicts conflicts={preview} timeZone={context.timeZone} />
            ) : (
              <p>No schedule conflicts found.</p>
            )}
            <p className="form-help">
              The server checks again when you save. This adds an assignment
              awaiting the volunteer’s response; it sends no notification.
            </p>
          </div>
        )}
        {error && (
          <p className="inline-error" role="alert">
            {error}
          </p>
        )}
        <button className="button button-small" type="submit">
          {busy === 'preview'
            ? 'Checking schedule…'
            : busy === 'save'
              ? 'Saving…'
              : preview === null
                ? 'Preview assignment'
                : 'Save assignment'}
        </button>
      </fieldset>
    </form>
  );
}
