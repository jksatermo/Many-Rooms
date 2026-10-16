using ManyRooms.Domain;

namespace ManyRooms.Application;

/// <summary>Leader scheduling use cases. Writes share one organization transaction with assignment creation.</summary>
public sealed class LeaderSchedulingService(ILeaderSchedulingStore store, TimeProvider clock)
{
    public async Task<IReadOnlyList<TeamSummaryDto>> TeamsAsync(Guid org, Guid user, CancellationToken ct)
    {
        await Authorize(org, user, ct);
        var teams = await store.ListTeamsAsync(org, ct);
        var members = await store.ListMembersAsync(org, ct);
        var assignments = await store.ListAssignmentsAsync(org, ct);
        return teams.OrderBy(x => x.Name).Select(x => new TeamSummaryDto(x.Id, x.Name, x.Description,
            members.Count(m => m.TeamId == x.Id && m.IsActive), assignments.Count(a => a.TeamMember.TeamId == x.Id &&
                !a.IsCancelled && a.Status != AssignmentStatus.Declined && a.Event.StartsAt > clock.GetUtcNow()))).ToArray();
    }

    public async Task<TeamDetailsDto> TeamAsync(Guid org, Guid teamId, Guid user, CancellationToken ct)
    {
        await Authorize(org, user, ct);
        var team = await Team(org, teamId, ct);
        var members = (await store.ListMembersAsync(org, ct)).Where(x => x.TeamId == teamId && x.IsActive)
            .OrderBy(x => x.Membership.User.DisplayName).Select(MemberDto).ToArray();
        var assignments = await store.ListAssignmentsAsync(org, ct);
        var unavailable = await store.ListUnavailableAsync(org, ct);
        return new(team.Id, team.Name, team.Description, members, assignments.Where(x => x.TeamMember.TeamId == teamId &&
                x.Event.StartsAt > clock.GetUtcNow()).OrderBy(x => x.Event.StartsAt).ThenBy(x => x.Id)
            .Select(x => x.ToDto(user, ScheduleConflictDetector.Detect(x, assignments, unavailable))).ToArray());
    }

    public async Task<IReadOnlyList<OrganizationMemberDto>> OrganizationMembersAsync(Guid org, Guid user, CancellationToken ct)
    {
        await Authorize(org, user, ct);
        return (await store.ListMembershipsAsync(org, ct)).OrderBy(x => x.User.DisplayName)
            .Select(x => new OrganizationMemberDto(x.Id, x.UserId, x.User.DisplayName)).ToArray();
    }

    public Task<TeamMemberDto> AddMemberAsync(Guid org, Guid teamId, Guid user, AddTeamMemberRequest request, CancellationToken ct) =>
        store.InOrganizationTransactionAsync(org, async () =>
        {
            await Authorize(org, user, ct);
            var team = await Team(org, teamId, ct);
            var membership = (await store.ListMembershipsAsync(org, ct)).SingleOrDefault(x => x.Id == request.MembershipId)
                ?? throw new SchedulingNotFoundException();
            var member = (await store.ListMembersAsync(org, ct)).SingleOrDefault(x => x.TeamId == teamId && x.MembershipId == membership.Id);
            if (member?.IsActive == true) throw new AssignmentConflictException("This volunteer already belongs to the team.");
            var isNew = member is null;
            member ??= new() { OrganizationId = org, TeamId = teamId, Team = team, MembershipId = membership.Id, Membership = membership, CreatedAt = clock.GetUtcNow() };
            member.PositionName = OptionalRole(request.PositionName);
            member.IsActive = true;
            member.UpdatedAt = clock.GetUtcNow();
            if (!isNew) member.Version++;
            await store.SaveSchedulingAsync(member, isNew, null, ct);
            return MemberDto(member);
        }, ct);

    public Task<TeamMemberDto> UpdateMemberAsync(Guid org, Guid teamId, Guid memberId, Guid user, UpdateTeamMemberRequest request, CancellationToken ct) =>
        store.InOrganizationTransactionAsync(org, async () =>
        {
            await Authorize(org, user, ct);
            var member = await Member(org, teamId, memberId, ct);
            Version(member.Version, request.ExpectedVersion);
            member.PositionName = OptionalRole(request.PositionName);
            member.UpdatedAt = clock.GetUtcNow();
            member.Version++;
            await store.SaveSchedulingAsync(member, false, null, ct);
            return MemberDto(member);
        }, ct);

    public async Task RemoveMemberAsync(Guid org, Guid teamId, Guid memberId, Guid user, int expectedVersion, CancellationToken ct) =>
        await store.InOrganizationTransactionAsync(org, async () =>
        {
            await Authorize(org, user, ct);
            var member = await Member(org, teamId, memberId, ct);
            Version(member.Version, expectedVersion);
            if ((await store.ListAssignmentsAsync(org, ct)).Any(x => x.TeamMemberId == memberId && !x.IsCancelled &&
                    x.Status != AssignmentStatus.Declined && x.Event.StartsAt > clock.GetUtcNow()))
                throw new AssignmentConflictException("Resolve or cancel this volunteer's future assignments before removing them from the team.");
            member.IsActive = false;
            member.Version++;
            member.UpdatedAt = clock.GetUtcNow();
            await store.SaveSchedulingAsync(member, false, null, ct);
            return true;
        }, ct);

    public async Task<IReadOnlyList<StaffingRequirementDto>> StaffingAsync(Guid org, Guid eventId, Guid user, CancellationToken ct)
    {
        await Authorize(org, user, ct);
        _ = await store.FindEventAsync(org, eventId, ct) ?? throw new SchedulingNotFoundException();
        return await StaffingProjection(org, eventId, ct);
    }

    public Task<StaffingRequirementDto> CreateStaffingAsync(Guid org, Guid eventId, Guid user, CreateStaffingRequest request, CancellationToken ct) =>
        store.InOrganizationTransactionAsync(org, async () =>
        {
            await Authorize(org, user, ct);
            await MutableEvent(org, eventId, ct);
            _ = await Team(org, request.TeamId, ct);
            var role = OptionalRole(request.RoleName);
            ValidateDesired(request.DesiredCount);
            await CheckStaffingMix(org, eventId, request.TeamId, role, null, ct);
            var item = new EventStaffingRequirement { OrganizationId = org, EventId = eventId, TeamId = request.TeamId,
                RoleName = role, NormalizedRoleName = StaffingPolicy.Normalize(role), DesiredCount = request.DesiredCount,
                CreatedAt = clock.GetUtcNow(), UpdatedAt = clock.GetUtcNow() };
            await store.SaveSchedulingAsync(item, true, null, ct);
            return (await StaffingProjection(org, eventId, ct)).Single(x => x.Id == item.Id);
        }, ct);

    public Task<StaffingRequirementDto> UpdateStaffingAsync(Guid org, Guid eventId, Guid id, Guid user, UpdateStaffingRequest request, CancellationToken ct) =>
        store.InOrganizationTransactionAsync(org, async () =>
        {
            await Authorize(org, user, ct);
            await MutableEvent(org, eventId, ct);
            var item = (await store.ListStaffingAsync(org, eventId, ct)).SingleOrDefault(x => x.Id == id) ?? throw new SchedulingNotFoundException();
            Version(item.Version, request.ExpectedVersion);
            var role = OptionalRole(request.RoleName);
            ValidateDesired(request.DesiredCount);
            await CheckStaffingMix(org, eventId, item.TeamId, role, id, ct);
            item.RoleName = role;
            item.NormalizedRoleName = StaffingPolicy.Normalize(role);
            item.DesiredCount = request.DesiredCount;
            item.Version++;
            item.UpdatedAt = clock.GetUtcNow();
            await store.SaveSchedulingAsync(item, false, null, ct);
            return (await StaffingProjection(org, eventId, ct)).Single(x => x.Id == id);
        }, ct);

    public async Task RemoveStaffingAsync(Guid org, Guid eventId, Guid id, Guid user, int expectedVersion, CancellationToken ct) =>
        await store.InOrganizationTransactionAsync(org, async () =>
        {
            await Authorize(org, user, ct);
            await MutableEvent(org, eventId, ct);
            var item = (await store.ListStaffingAsync(org, eventId, ct)).SingleOrDefault(x => x.Id == id) ?? throw new SchedulingNotFoundException();
            Version(item.Version, expectedVersion);
            await store.DeleteStaffingAsync(item, ct);
            return true;
        }, ct);

    public async Task<AssignmentPreviewDto> PreviewEditAsync(Guid org, Guid id, Guid user, UpdateAssignmentRequest request, CancellationToken ct)
    {
        await Authorize(org, user, ct);
        var current = await Assignment(org, id, ct);
        await CheckEditable(current, request.ExpectedVersion, ct);
        var member = await ReplacementMember(org, current, request.TeamMemberId, ct);
        var role = RequiredRole(request.RoleName);
        await CheckDuplicate(org, current, member.Id, role, ct);
        var preview = new VolunteerAssignment { Id = current.Id, OrganizationId = org, EventId = current.EventId, Event = current.Event,
            TeamMemberId = member.Id, TeamMember = member, RoleName = role };
        return new(await store.GetConflictsAsync(preview, ct));
    }

    public Task<AssignmentDto> UpdateAssignmentAsync(Guid org, Guid id, Guid user, UpdateAssignmentRequest request, CancellationToken ct) =>
        store.InOrganizationTransactionAsync(org, async () =>
        {
            await Authorize(org, user, ct);
            var item = await Assignment(org, id, ct);
            await CheckEditable(item, request.ExpectedVersion, ct);
            var member = await ReplacementMember(org, item, request.TeamMemberId, ct);
            var role = RequiredRole(request.RoleName);
            await CheckDuplicate(org, item, member.Id, role, ct);
            if (item.TeamMemberId == member.Id && item.RoleName == role)
                return item.ToDto(user, await store.GetConflictsAsync(item, ct));
            var beforeName = item.TeamMember.Membership.User.DisplayName;
            var beforeRole = item.RoleName;
            var action = member.Id != item.TeamMemberId ? "Reassigned" : "RoleChanged";
            item.TeamMemberId = member.Id;
            item.TeamMember = member;
            item.RoleName = role;
            item.Status = AssignmentStatus.Pending;
            item.RespondedAt = null;
            item.Version++;
            item.UpdatedAt = clock.GetUtcNow();
            var actor = await store.FindMembershipAsync(org, user, ct) ?? throw new SchedulingAccessException();
            await store.SaveSchedulingAsync(item, false, AssignmentHistory.Capture(item, actor.Id, action, clock.GetUtcNow(), beforeName, beforeRole), ct);
            return item.ToDto(user, await store.GetConflictsAsync(item, ct));
        }, ct);

    public Task<AssignmentDto> CancelAsync(Guid org, Guid id, Guid user, int expectedVersion, CancellationToken ct) =>
        store.InOrganizationTransactionAsync(org, async () =>
        {
            await Authorize(org, user, ct);
            var item = await Assignment(org, id, ct);
            await CheckEditable(item, expectedVersion, ct);
            item.IsCancelled = true;
            item.CancelledAt = clock.GetUtcNow();
            item.UpdatedAt = clock.GetUtcNow();
            item.Version++;
            var actor = await store.FindMembershipAsync(org, user, ct) ?? throw new SchedulingAccessException();
            await store.SaveSchedulingAsync(item, false, AssignmentHistory.Capture(item, actor.Id, "Cancelled", clock.GetUtcNow()), ct);
            return item.ToDto(user);
        }, ct);

    public async Task<IReadOnlyList<AssignmentHistoryDto>> HistoryAsync(Guid org, Guid user, Guid? eventId, Guid? assignmentId, CancellationToken ct)
    {
        await Authorize(org, user, ct);
        if (eventId.HasValue) _ = await store.FindEventAsync(org, eventId.Value, ct) ?? throw new SchedulingNotFoundException();
        if (assignmentId.HasValue) _ = await Assignment(org, assignmentId.Value, ct);
        return await store.ListHistoryAsync(org, eventId, assignmentId, ct);
    }

    public async Task<IReadOnlyList<AssignmentDto>> ConflictsAsync(Guid org, Guid user, CancellationToken ct)
    {
        await Authorize(org, user, ct);
        var assignments = await store.ListAssignmentsAsync(org, ct);
        var unavailable = await store.ListUnavailableAsync(org, ct);
        return assignments.Where(x => !x.IsCancelled && x.Event.StartsAt > clock.GetUtcNow())
            .Select(x => x.ToDto(user, ScheduleConflictDetector.Detect(x, assignments, unavailable)))
            .Where(x => x.Status == "Declined" || x.Conflicts.Count > 0).OrderBy(x => x.EventStartsAt)
            .ThenBy(x => x.VolunteerName, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Id).ToArray();
    }

    public async Task<IReadOnlyList<ReplacementCandidateDto>> ReplacementsAsync(Guid org, Guid id, Guid user, CancellationToken ct)
    {
        await Authorize(org, user, ct);
        var target = await Assignment(org, id, ct);
        await CheckEditable(target, target.Version, ct);
        var assignments = await store.ListAssignmentsAsync(org, ct);
        var unavailable = await store.ListUnavailableAsync(org, ct);
        var candidates = (await store.ListMembersAsync(org, ct)).Where(x => x.IsActive && x.TeamId == target.TeamMember.TeamId &&
            x.MembershipId != target.TeamMember.MembershipId);
        return ReplacementPolicy.Suggest(target, candidates, assignments, unavailable);
    }

    private async Task<IReadOnlyList<StaffingRequirementDto>> StaffingProjection(Guid org, Guid eventId, CancellationToken ct)
    {
        var teams = (await store.ListTeamsAsync(org, ct)).ToDictionary(x => x.Id);
        var assignments = await store.ListAssignmentsAsync(org, ct);
        var unavailable = await store.ListUnavailableAsync(org, ct);
        var conflicts = assignments.Where(x => x.EventId == eventId && ScheduleConflictDetector.Detect(x, assignments, unavailable).Count > 0)
            .Select(x => x.Id).ToHashSet();
        return (await store.ListStaffingAsync(org, eventId, ct)).Select(x => StaffingPolicy.Summarize(x, teams[x.TeamId].Name, assignments, conflicts))
            .OrderBy(x => x.TeamName).ThenBy(x => x.RoleName).ThenBy(x => x.Id).ToArray();
    }

    private async Task CheckStaffingMix(Guid org, Guid eventId, Guid teamId, string? role, Guid? excludeId, CancellationToken ct)
    {
        var key = StaffingPolicy.Normalize(role);
        if ((await store.ListStaffingAsync(org, eventId, ct)).Any(x => x.TeamId == teamId && x.Id != excludeId &&
                (x.NormalizedRoleName.Length == 0 || key.Length == 0 || x.NormalizedRoleName == key)))
            throw new AssignmentConflictException("Use either a whole-team requirement or separate named roles. Each role can have one requirement.");
    }
    private async Task CheckDuplicate(Guid org, VolunteerAssignment current, Guid memberId, string role, CancellationToken ct)
    {
        if ((await store.ListAssignmentsAsync(org, ct)).Any(x => x.Id != current.Id && !x.IsCancelled &&
            x.EventId == current.EventId && x.TeamMemberId == memberId && StaffingPolicy.Normalize(x.RoleName) == StaffingPolicy.Normalize(role)))
            throw new AssignmentConflictException("This volunteer already has that role for this event.");
    }
    private async Task<TeamMember> ReplacementMember(Guid org, VolunteerAssignment current, Guid memberId, CancellationToken ct)
    {
        var member = await store.FindTeamMemberAsync(org, memberId, ct) ?? throw new SchedulingNotFoundException();
        if (member.TeamId != current.TeamMember.TeamId) throw new ArgumentException("A replacement must belong to the same team.");
        return member;
    }
    private Task CheckEditable(VolunteerAssignment item, int version, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Version(item.Version, version);
        if (item.IsCancelled) throw new AssignmentConflictException("This assignment was cancelled.");
        if (item.Event.StartsAt <= clock.GetUtcNow()) throw new AssignmentConflictException("Assignments cannot be changed after the event starts.");
        return Task.CompletedTask;
    }
    private async Task<Event> MutableEvent(Guid org, Guid eventId, CancellationToken ct)
    {
        var item = await store.FindEventAsync(org, eventId, ct) ?? throw new SchedulingNotFoundException();
        if (item.StartsAt <= clock.GetUtcNow()) throw new AssignmentConflictException("Staffing cannot be changed after the event starts.");
        return item;
    }
    private async Task<VolunteerAssignment> Assignment(Guid org, Guid id, CancellationToken ct) =>
        (await store.ListAssignmentsAsync(org, ct)).SingleOrDefault(x => x.Id == id) ?? throw new SchedulingNotFoundException();
    private async Task<Team> Team(Guid org, Guid id, CancellationToken ct) =>
        (await store.ListTeamsAsync(org, ct)).SingleOrDefault(x => x.Id == id) ?? throw new SchedulingNotFoundException();
    private async Task<TeamMember> Member(Guid org, Guid teamId, Guid id, CancellationToken ct) =>
        (await store.ListMembersAsync(org, ct)).SingleOrDefault(x => x.TeamId == teamId && x.Id == id && x.IsActive) ?? throw new SchedulingNotFoundException();
    private async Task Authorize(Guid org, Guid user, CancellationToken ct)
    {
        if (!await store.IsLeaderAsync(org, user, ct)) throw new SchedulingAccessException();
    }
    private static void Version(int actual, int expected)
    {
        if (expected < 1) throw new ArgumentException("ExpectedVersion must be a positive integer.");
        if (actual != expected) throw new AssignmentConflictException("This schedule item changed. Refresh before trying again.");
    }
    private static string? OptionalRole(string? role)
    {
        var result = string.IsNullOrWhiteSpace(role) ? null : role.Trim();
        if (result?.Length > 120) throw new ArgumentException("Role or position must contain at most 120 characters.");
        return result;
    }
    private static string RequiredRole(string? role) => OptionalRole(role) ?? throw new ArgumentException("RoleName is required.");
    private static void ValidateDesired(int value)
    {
        if (value is < 1 or > 100) throw new ArgumentException("DesiredCount must be between 1 and 100.");
    }
    private static TeamMemberDto MemberDto(TeamMember x) => new(x.Id, x.TeamId, x.Team.Name, x.MembershipId,
        x.Membership.UserId, x.Membership.User.DisplayName, x.PositionName, x.Version);
}
