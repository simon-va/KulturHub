using ErrorOr;
using FluentAssertions;
using KulturHub.Application.Errors;
using KulturHub.Application.Features.Platform.Memberships.DeleteMembership;
using KulturHub.Domain.Memberships;
using KulturHub.Domain.Organisations;
using KulturHub.Domain.Users;
using KulturHub.Infrastructure.Persistence;
using KulturHub.UnitTests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace KulturHub.UnitTests.Features.Application.Platform.Memberships.DeleteMembership;

public class DeleteMembershipHandlerTests
{
    private static readonly DateTime NowUtc = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime LaterUtc = new(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ActorUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TargetUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static (DeleteMembershipHandler Sut, AppDbContext Db, FakeTimeProvider Clock) CreateSut(
        IEnumerable<Organisation> organisations,
        IEnumerable<User> users,
        IEnumerable<Membership> memberships,
        DateTime? clockNow = null)
    {
        var db = TestDbContextFactory.CreateInMemory();
        db.Organisations.AddRange(organisations);
        db.Users.AddRange(users);
        db.Memberships.AddRange(memberships);
        db.SaveChanges();

        var clock = new FakeTimeProvider(clockNow ?? NowUtc);
        var handler = new DeleteMembershipHandler(
            db, clock, NullLogger<DeleteMembershipHandler>.Instance);

        return (handler, db, clock);
    }

    private static Organisation CreateOrganisation(string name) =>
        Organisation.Create(name, new FakeTimeProvider(NowUtc)).Value;

    private static User CreateUser(Guid id, string firstName, string lastName, string email) =>
        User.Create(UserId.From(id), email, firstName, lastName, new FakeTimeProvider(NowUtc)).Value;

    private static Membership CreateMembership(Guid userId, Organisation organisation, MembershipStatus status) =>
        Membership.Create(UserId.From(userId), organisation.Id, status, new FakeTimeProvider(NowUtc)).Value;

    private static DeleteMembershipCommand ValidCommand(Guid membershipId, Guid actorUserId, Guid organisationId) =>
        new(membershipId, actorUserId, organisationId);

    [Fact]
    public async Task Handle_WhenMembershipDoesNotExist_ShouldReturnNotFoundError()
    {
        var org = CreateOrganisation("Org");
        var actor = CreateUser(ActorUserId, "Actor", "User", "actor@example.com");
        var (sut, db, _) = CreateSut([org], [actor], []);

        var result = await sut.HandleAsync(
            ValidCommand(Guid.NewGuid(), ActorUserId, org.Id.Value),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
        result.FirstError.Code.Should().Be("Membership.NotFound");
        db.ChangeLogs.Count().Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenMembershipIsSoftDeleted_ShouldReturnNotFoundError()
    {
        var org = CreateOrganisation("Org");
        var actor = CreateUser(ActorUserId, "Actor", "User", "actor@example.com");
        var target = CreateUser(TargetUserId, "Target", "User", "target@example.com");
        var actorMembership = CreateMembership(ActorUserId, org, MembershipStatus.Accepted);
        var targetMembership = CreateMembership(TargetUserId, org, MembershipStatus.Accepted);
        var (sut, db, _) = CreateSut([org], [actor, target], [actorMembership, targetMembership]);

        db.Entry(targetMembership).Property(nameof(Membership.IsDeleted)).CurrentValue = true;
        db.SaveChanges();

        var result = await sut.HandleAsync(
            ValidCommand(targetMembership.Id.Value, ActorUserId, org.Id.Value),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Membership.NotFound");
        db.ChangeLogs.Count().Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenMembershipIsInDifferentOrganisation_ShouldReturnNotFoundError()
    {
        var orgA = CreateOrganisation("OrgA");
        var orgB = CreateOrganisation("OrgB");
        var actor = CreateUser(ActorUserId, "Actor", "User", "actor@example.com");
        var target = CreateUser(TargetUserId, "Target", "User", "target@example.com");
        var actorMembershipA = CreateMembership(ActorUserId, orgA, MembershipStatus.Accepted);
        var targetMembershipB = CreateMembership(TargetUserId, orgB, MembershipStatus.Accepted);
        var (sut, db, _) = CreateSut([orgA, orgB], [actor, target], [actorMembershipA, targetMembershipB]);

        var result = await sut.HandleAsync(
            ValidCommand(targetMembershipB.Id.Value, ActorUserId, orgA.Id.Value),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Membership.NotFound");
        db.ChangeLogs.Count().Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenLastActiveMember_ShouldReturnConflictError()
    {
        var org = CreateOrganisation("Org");
        var actor = CreateUser(ActorUserId, "Actor", "User", "actor@example.com");
        var actorMembership = CreateMembership(ActorUserId, org, MembershipStatus.Accepted);
        var (sut, db, _) = CreateSut([org], [actor], [actorMembership]);

        var result = await sut.HandleAsync(
            ValidCommand(actorMembership.Id.Value, ActorUserId, org.Id.Value),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        result.FirstError.Code.Should().Be("Membership.LastActiveMember");

        var stored = db.Memberships.Single();
        stored.IsDeleted.Should().BeFalse();
        db.ChangeLogs.Count().Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenPendingMembershipAlongsideOtherAcceptedMembers_ShouldAllowDelete()
    {
        var org = CreateOrganisation("Org");
        var actor = CreateUser(ActorUserId, "Actor", "User", "actor@example.com");
        var otherAccepted = CreateUser(OtherUserId, "Other", "User", "other@example.com");
        var pendingTarget = CreateUser(TargetUserId, "Pending", "Target", "pending@example.com");
        var actorMembership = CreateMembership(ActorUserId, org, MembershipStatus.Accepted);
        var otherMembership = CreateMembership(OtherUserId, org, MembershipStatus.Accepted);
        var pendingMembership = CreateMembership(TargetUserId, org, MembershipStatus.Pending);
        var (sut, db, _) = CreateSut(
            [org],
            [actor, otherAccepted, pendingTarget],
            [actorMembership, otherMembership, pendingMembership],
            LaterUtc);

        var result = await sut.HandleAsync(
            ValidCommand(pendingMembership.Id.Value, ActorUserId, org.Id.Value),
            CancellationToken.None);

        result.IsError.Should().BeFalse();

        var stored = db.Memberships.IgnoreQueryFilters().Single(m => m.Id == pendingMembership.Id);
        stored.IsDeleted.Should().BeTrue();
        stored.DeletedAt.Should().Be(LaterUtc);
    }

    [Fact]
    public async Task Handle_WhenNonLastActiveMember_ShouldSoftDeleteAndWriteChangeLog()
    {
        var org = CreateOrganisation("Org");
        var actor = CreateUser(ActorUserId, "Actor", "User", "actor@example.com");
        var target = CreateUser(TargetUserId, "Target", "User", "target@example.com");
        var actorMembership = CreateMembership(ActorUserId, org, MembershipStatus.Accepted);
        var targetMembership = CreateMembership(TargetUserId, org, MembershipStatus.Accepted);
        var (sut, db, _) = CreateSut([org], [actor, target], [actorMembership, targetMembership], LaterUtc);

        var result = await sut.HandleAsync(
            ValidCommand(targetMembership.Id.Value, ActorUserId, org.Id.Value),
            CancellationToken.None);

        result.IsError.Should().BeFalse();

        var stored = db.Memberships.IgnoreQueryFilters().Single(m => m.Id == targetMembership.Id);
        stored.IsDeleted.Should().BeTrue();
        stored.DeletedAt.Should().Be(LaterUtc);

        db.Memberships.Count().Should().Be(1);
        db.ChangeLogs.Count().Should().Be(1);
        var changeLog = db.ChangeLogs.Single();
        changeLog.Message.Should().Be("Mitglied entfernt");
        changeLog.OrganisationId.Value.Should().Be(org.Id.Value);
        changeLog.CreatedBy.Value.Should().Be(ActorUserId);
        changeLog.CreatedAt.Should().Be(LaterUtc);
        changeLog.Data["membershipId"].Should().Be(targetMembership.Id.Value.ToString());
        changeLog.Data["userId"].Should().Be(TargetUserId.ToString());
        changeLog.Data["status"].Should().Be("Accepted");
    }

    [Fact]
    public async Task Handle_WhenSelfDeleting_ShouldBeAllowed()
    {
        var org = CreateOrganisation("Org");
        var actor = CreateUser(ActorUserId, "Actor", "User", "actor@example.com");
        var other = CreateUser(OtherUserId, "Other", "User", "other@example.com");
        var actorMembership = CreateMembership(ActorUserId, org, MembershipStatus.Accepted);
        var otherMembership = CreateMembership(OtherUserId, org, MembershipStatus.Accepted);
        var (sut, db, _) = CreateSut([org], [actor, other], [actorMembership, otherMembership], LaterUtc);

        var result = await sut.HandleAsync(
            ValidCommand(actorMembership.Id.Value, ActorUserId, org.Id.Value),
            CancellationToken.None);

        result.IsError.Should().BeFalse();

        var stored = db.Memberships.IgnoreQueryFilters().Single(m => m.Id == actorMembership.Id);
        stored.IsDeleted.Should().BeTrue();
        stored.DeletedAt.Should().Be(LaterUtc);
    }

    [Fact]
    public async Task Handle_WhenOnlyAcceptedMemberIsPending_ShouldAllowDelete()
    {
        var org = CreateOrganisation("Org");
        var actor = CreateUser(ActorUserId, "Actor", "User", "actor@example.com");
        var pendingTarget = CreateUser(TargetUserId, "Pending", "Target", "pending@example.com");
        var actorMembership = CreateMembership(ActorUserId, org, MembershipStatus.Accepted);
        var pendingMembership = CreateMembership(TargetUserId, org, MembershipStatus.Pending);
        var (sut, db, _) = CreateSut(
            [org],
            [actor, pendingTarget],
            [actorMembership, pendingMembership],
            LaterUtc);

        var result = await sut.HandleAsync(
            ValidCommand(pendingMembership.Id.Value, ActorUserId, org.Id.Value),
            CancellationToken.None);

        result.IsError.Should().BeFalse();

        var stored = db.Memberships.IgnoreQueryFilters().Single(m => m.Id == pendingMembership.Id);
        stored.IsDeleted.Should().BeTrue();
        stored.DeletedAt.Should().Be(LaterUtc);
        db.ChangeLogs.Count().Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenOnlyAcceptedMemberIsRejected_ShouldAllowDelete()
    {
        var org = CreateOrganisation("Org");
        var actor = CreateUser(ActorUserId, "Actor", "User", "actor@example.com");
        var rejectedTarget = CreateUser(TargetUserId, "Rejected", "Target", "rejected@example.com");
        var actorMembership = CreateMembership(ActorUserId, org, MembershipStatus.Accepted);
        var rejectedMembership = CreateMembership(TargetUserId, org, MembershipStatus.Rejected);
        var (sut, db, _) = CreateSut(
            [org],
            [actor, rejectedTarget],
            [actorMembership, rejectedMembership],
            LaterUtc);

        var result = await sut.HandleAsync(
            ValidCommand(rejectedMembership.Id.Value, ActorUserId, org.Id.Value),
            CancellationToken.None);

        result.IsError.Should().BeFalse();

        var stored = db.Memberships.IgnoreQueryFilters().Single(m => m.Id == rejectedMembership.Id);
        stored.IsDeleted.Should().BeTrue();
        stored.DeletedAt.Should().Be(LaterUtc);
        db.ChangeLogs.Count().Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenMultipleAcceptedMembersAndDeletingPending_ShouldAllowDelete()
    {
        var org = CreateOrganisation("Org");
        var actor = CreateUser(ActorUserId, "Actor", "User", "actor@example.com");
        var otherAccepted = CreateUser(OtherUserId, "Other", "User", "other@example.com");
        var pendingTarget = CreateUser(TargetUserId, "Pending", "Target", "pending@example.com");
        var actorMembership = CreateMembership(ActorUserId, org, MembershipStatus.Accepted);
        var otherMembership = CreateMembership(OtherUserId, org, MembershipStatus.Accepted);
        var pendingMembership = CreateMembership(TargetUserId, org, MembershipStatus.Pending);
        var (sut, db, _) = CreateSut(
            [org],
            [actor, otherAccepted, pendingTarget],
            [actorMembership, otherMembership, pendingMembership],
            LaterUtc);

        var result = await sut.HandleAsync(
            ValidCommand(pendingMembership.Id.Value, ActorUserId, org.Id.Value),
            CancellationToken.None);

        result.IsError.Should().BeFalse();

        var stored = db.Memberships.IgnoreQueryFilters().Single(m => m.Id == pendingMembership.Id);
        stored.IsDeleted.Should().BeTrue();
        stored.DeletedAt.Should().Be(LaterUtc);
    }
}
