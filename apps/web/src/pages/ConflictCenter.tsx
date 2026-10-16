import { useCallback, useState } from 'react';
import { Link, useOutletContext } from 'react-router-dom';
import { api } from '../api';
import { AssignmentEditor, LeaderOnly } from '../components/LeaderTools';
import { Conflicts } from '../components/Conflicts';
import { ErrorState, Loading, Status } from '../components/Ui';
import { date, time } from '../format';
import type { Assignment, DemoContext } from '../types';
import { useResource } from '../useResource';

export function ConflictCenter() {
  return (
    <LeaderOnly>
      <ConflictList />
    </LeaderOnly>
  );
}
function ConflictList() {
  const context = useOutletContext<DemoContext>();
  const [editing, setEditing] = useState<Assignment | null>(null);
  const [notice, setNotice] = useState('');
  const result = useResource(
    useCallback(
      (signal: AbortSignal) => api.conflicts(context.organizationId, signal),
      [context.organizationId],
    ),
    { refreshOnFocus: !editing },
  );
  return (
    <>
      <div className="page-heading">
        <div>
          <p className="eyebrow">Demo leader · {context.organizationName}</p>
          <h1>A clear next step.</h1>
          <p>
            Review schedule conflicts and declined assignments, then help each
            person find a workable place.
          </p>
        </div>
        <button
          className="button button-outline button-small"
          disabled={!!editing}
          onClick={result.reload}
        >
          Refresh schedule
        </button>
      </div>
      <p className="form-help">
        Nothing is changed automatically. Suggestions only include active
        members of the same team without a detected time conflict. Saving
        rechecks the schedule and asks the new volunteer to respond.
      </p>
      {notice && (
        <p role="status" className="notice">
          {notice}
        </p>
      )}
      {result.loading ? (
        <Loading label="Checking upcoming assignments…" />
      ) : result.error ? (
        <ErrorState error={result.error} onRetry={result.reload} />
      ) : result.data!.length ? (
        <div className="conflict-center-list">
          {result.data!.map((assignment) => (
            <article
              className="assignment"
              key={assignment.id}
              data-testid="conflict-assignment"
            >
              <div className="assignment-main">
                <div className="assignment-person">
                  <p className="eyebrow">{assignment.eventTitle}</p>
                  <h2>{assignment.volunteerName}</h2>
                  <p>
                    {assignment.teamName} · {assignment.roleName}
                  </p>
                  <p>
                    {date(assignment.eventStartsAt, context.timeZone)} ·{' '}
                    {time(assignment.eventStartsAt, context.timeZone)}
                  </p>
                </div>
                <Status value={assignment.status} />
              </div>
              {assignment.status === 'Declined' && (
                <p className="declined-note">Declined — replacement needed.</p>
              )}
              <Conflicts
                conflicts={assignment.conflicts}
                timeZone={context.timeZone}
              />
              <div className="assignment-tools">
                <Link
                  className="text-link"
                  to={`/app/leader/events/${assignment.eventId}`}
                >
                  Open event →
                </Link>
                <button
                  disabled={editing !== null && editing.id !== assignment.id}
                  className="button button-outline button-small"
                  onClick={() => setEditing(assignment)}
                >
                  Find a replacement
                </button>
              </div>
              {editing?.id === assignment.id && (
                <AssignmentEditor
                  key={`${assignment.id}-${assignment.version}`}
                  context={context}
                  assignment={editing}
                  members={[]}
                  candidatesOnly
                  onClose={() => {
                    setEditing(null);
                    result.reload();
                  }}
                  onSaved={(saved) => {
                    setEditing(null);
                    setNotice(
                      saved.isCancelled
                        ? 'Assignment cancelled. History is preserved.'
                        : `Assignment updated for ${saved.volunteerName}. ${saved.conflicts.length ? 'New conflicts remain visible below.' : 'The volunteer’s response is now pending.'}`,
                    );
                    result.reload();
                  }}
                />
              )}
            </article>
          ))}
        </div>
      ) : (
        <div className="empty-state">
          <h2>Room to breathe.</h2>
          <p>No upcoming conflicts or declined assignments need attention.</p>
        </div>
      )}
    </>
  );
}
