import { useCallback, useId, useState } from 'react';
import { Link, useOutletContext, useParams } from 'react-router-dom';
import { api, ApiError } from '../api';
import { LeaderOnly, message } from '../components/LeaderTools';
import { ErrorState, Loading, Status } from '../components/Ui';
import { date } from '../format';
import type { DemoContext, TeamMember } from '../types';
import { useResource } from '../useResource';

export function Teams() {
  return (
    <LeaderOnly>
      <TeamList />
    </LeaderOnly>
  );
}
function TeamList() {
  const context = useOutletContext<DemoContext>();
  const result = useResource(
    useCallback(
      (signal: AbortSignal) => api.teams(context.organizationId, signal),
      [context.organizationId],
    ),
  );
  return (
    <>
      <div className="page-heading">
        <div>
          <p className="eyebrow">Demo leader · {context.organizationName}</p>
          <h1>People make the room.</h1>
          <p>
            Keep teams and volunteer positions ready for the next gathering.
          </p>
        </div>
      </div>
      {result.loading ? (
        <Loading />
      ) : result.error ? (
        <ErrorState error={result.error} onRetry={result.reload} />
      ) : result.data?.length ? (
        <div className="team-card-grid">
          {result.data.map((team) => (
            <Link
              className="availability-card team-link"
              key={team.id}
              to={`/app/leader/teams/${team.id}`}
            >
              <h2>{team.name}</h2>
              <p>{team.description}</p>
              <p>
                {team.memberCount} active members ·{' '}
                {team.upcomingAssignmentCount} upcoming assignments
              </p>
              <span className="text-link">Open team →</span>
            </Link>
          ))}
        </div>
      ) : (
        <p className="empty-state">No teams have been added yet.</p>
      )}
    </>
  );
}
export function TeamDetails() {
  return (
    <LeaderOnly>
      <TeamDetailContent />
    </LeaderOnly>
  );
}
function TeamDetailContent() {
  const context = useOutletContext<DemoContext>();
  const { teamId = '' } = useParams();
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState('');
  const [error, setError] = useState('');
  const [membershipId, setMembership] = useState('');
  const [position, setPosition] = useState('');
  const result = useResource(
    useCallback(
      (signal: AbortSignal) => api.team(context.organizationId, teamId, signal),
      [context.organizationId, teamId],
    ),
    { refreshOnFocus: !busy },
  );
  const people = useResource(
    useCallback(
      (signal: AbortSignal) =>
        api.organizationMembers(context.organizationId, signal),
      [context.organizationId],
    ),
    { refreshOnFocus: false },
  );
  const changed = (text: string) => {
    setNotice(text);
    result.reload();
  };
  async function add() {
    setBusy(true);
    setError('');
    try {
      await api.addTeamMember(
        context.organizationId,
        teamId,
        membershipId,
        position.trim() || null,
      );
      setMembership('');
      setPosition('');
      changed('Team member added.');
    } catch (cause) {
      setError(message(cause));
    } finally {
      setBusy(false);
    }
  }
  if (result.loading) return <Loading label="Loading the team…" />;
  if (result.error)
    return <ErrorState error={result.error} onRetry={result.reload} />;
  const team = result.data!;
  return (
    <>
      <Link className="back-link" to="/app/leader/teams">
        ← All teams
      </Link>
      <div className="page-heading">
        <div>
          <p className="eyebrow">Demo leader team</p>
          <h1>{team.name}</h1>
          <p>{team.description}</p>
        </div>
      </div>
      {notice && (
        <p role="status" className="notice">
          {notice}
        </p>
      )}
      <div className="leader-grid">
        <section aria-label="Team members">
          <h2>A place for each person.</h2>
          <p className="form-help">
            Positions describe how someone usually helps. Event assignments
            remain separate.
          </p>
          {team.members.length ? (
            team.members.map((member) => (
              <MemberRow
                key={`${member.id}-${member.version}`}
                member={member}
                context={context}
                onChanged={changed}
              />
            ))
          ) : (
            <p className="empty-state">No active team members yet.</p>
          )}
        </section>
        <section className="form-panel">
          <h2>Add a team member.</h2>
          {people.loading ? (
            <Loading />
          ) : people.error ? (
            <ErrorState error={people.error} onRetry={people.reload} />
          ) : (
            <form
              onSubmit={(e) => {
                e.preventDefault();
                void add();
              }}
            >
              <fieldset disabled={busy}>
                <label htmlFor="new-team-member">Organization member</label>
                <select
                  id="new-team-member"
                  required
                  value={membershipId}
                  onChange={(e) => setMembership(e.target.value)}
                >
                  <option value="">Choose a volunteer</option>
                  {people
                    .data!.filter(
                      (person) =>
                        !team.members.some(
                          (member) => member.membershipId === person.id,
                        ),
                    )
                    .map((person) => (
                      <option key={person.id} value={person.id}>
                        {person.volunteerName}
                      </option>
                    ))}
                </select>
                <label htmlFor="new-team-position">Position (optional)</label>
                <input
                  id="new-team-position"
                  maxLength={120}
                  value={position}
                  onChange={(e) => setPosition(e.target.value)}
                />
                {error && (
                  <p role="alert" className="inline-error">
                    {error}
                  </p>
                )}
                <button className="button button-small" type="submit">
                  {busy ? 'Adding…' : 'Add member'}
                </button>
              </fieldset>
            </form>
          )}
        </section>
      </div>
      <section className="scheduling-section">
        <h2>Upcoming assignments.</h2>
        {team.upcomingAssignments.length ? (
          <div className="availability-list">
            {team.upcomingAssignments.map((a) => (
              <article className="availability-card" key={a.id}>
                <Status value={a.status} cancelled={a.isCancelled} />
                <h3>
                  <Link
                    className="text-link"
                    to={`/app/leader/events/${a.eventId}`}
                  >
                    {a.eventTitle}
                  </Link>
                </h3>
                <p>
                  {a.volunteerName} · {a.roleName} ·{' '}
                  {date(a.eventStartsAt, context.timeZone)}
                </p>
              </article>
            ))}
          </div>
        ) : (
          <p className="form-help">No upcoming assignments for this team.</p>
        )}
      </section>
    </>
  );
}
function MemberRow({
  member,
  context,
  onChanged,
}: {
  member: TeamMember;
  context: DemoContext;
  onChanged: (text: string) => void;
}) {
  const id = useId();
  const [editing, setEditing] = useState(false);
  const [confirm, setConfirm] = useState(false);
  const [position, setPosition] = useState(member.positionName || '');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [stale, setStale] = useState(false);
  async function save(remove = false) {
    setBusy(true);
    setError('');
    try {
      if (remove) await api.removeTeamMember(context.organizationId, member);
      else
        await api.updateTeamMember(
          context.organizationId,
          member,
          position.trim() || null,
        );
      onChanged(
        remove
          ? 'Team member removed. Existing assignment history is preserved.'
          : 'Position updated.',
      );
    } catch (cause) {
      setError(message(cause));
      if (cause instanceof ApiError && cause.status === 409) setStale(true);
    } finally {
      setBusy(false);
    }
  }
  return (
    <article className="availability-card member-row">
      <h3>{member.volunteerName}</h3>
      <p>{member.positionName || 'No position set'}</p>
      {editing && (
        <form
          className="form-panel compact-form"
          onSubmit={(e) => {
            e.preventDefault();
            void save();
          }}
        >
          <fieldset disabled={busy || stale}>
            <label htmlFor={id}>Position for {member.volunteerName}</label>
            <input
              id={id}
              maxLength={120}
              value={position}
              onChange={(e) => setPosition(e.target.value)}
            />
            <button className="button button-small">
              {busy ? 'Saving…' : 'Save position'}
            </button>
          </fieldset>
        </form>
      )}
      <div className="form-actions">
        <button
          disabled={busy}
          className="quiet-link"
          onClick={() => setEditing(!editing)}
        >
          {editing ? 'Close position editor' : 'Edit position'}
        </button>
        <button
          disabled={busy || stale}
          className="quiet-link"
          onClick={() => setConfirm(true)}
        >
          Remove member
        </button>
      </div>
      {confirm && (
        <div className="confirm-panel">
          <p>
            Remove {member.volunteerName} from this team? Future pending or
            accepted assignments must be resolved first.
          </p>
          <button
            disabled={busy || stale}
            className="button button-small"
            onClick={() => void save(true)}
          >
            Confirm removal
          </button>
          <button
            disabled={busy}
            className="quiet-link"
            onClick={() => setConfirm(false)}
          >
            Keep member
          </button>
        </div>
      )}
      {error && (
        <p role="alert" className="inline-error">
          {error}
          {stale
            ? ' Refresh this team after resolving the schedule or to load the latest version.'
            : ''}
        </p>
      )}
    </article>
  );
}
