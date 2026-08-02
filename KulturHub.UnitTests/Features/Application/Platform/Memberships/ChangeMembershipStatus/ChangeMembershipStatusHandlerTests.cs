using ErrorOr;
using FluentAssertions;
using KulturHub.Application.Errors;
using KulturHub.Application.Features.Platform.Memberships.ChangeMembershipStatus;
using KulturHub.Domain.ChangeLogs;
using KulturHub.Domain.Memberships;
using KulturHub.Domain.Organisations;
using KulturHub.Domain.Users;
using KulturHub.Infrastructure.Persistence;
using KulturHub.UnitTests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace KulturHub.UnitTests.Features.Application.Platform.Memberships.ChangeMembershipStatus;

public class ChangeMembershipStatusHandlerTests
{
    private static readonly DateTime NowUtc = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime LaterUtc = new(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid InviteeUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static (ChangeMembershipStatusHandler Sut, AppDbContext Db, FakeTimeProvider Clock) CreateSut(
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
        var handler = new ChangeMembershipStatusHandler(
            db, clock, NullLogger<ChangeMembershipStatusHandler>.Instance);

        return (handler, db, clock);
    }

    private static Organisation CreateOrganisation(string name) =>
        Organisation.Create(name, new FakeTimeProvider(NowUtc)).Value;

    private static User CreateUser(Guid id, string firstName, string lastName, string email)
    {
        var userResult = User.Create(UserId.From(id), email, firstName, lastName, new FakeTimeProvider(NowUtc));
        return userResult.Value;
    }

    private static Membership CreatePendingMembership(Guid userId, Organisation organisation) =>
        Membership.Create(UserId.From(userId), organisation.Id, MembershipStatus.Pending, new FakeTimeProvider(NowUtc)).Value;

    private static ChangeMembershipStatusCommand AcceptCommand(Guid membershipId, Guid callerUserId) =>
        new(membershipId, callerUserId, MembershipChangeStatus.Accepted);

    [Fact]
    public async Task Handle_WhenMembershipDoesNotExist_ShouldReturnNotFoundError()
    {
        var org = CreateOrganisation("Org");
        var (sut, db, _) = CreateSut([org], [], []);

        var result = await sut.HandleAsync(AcceptCommand(Guid.NewGuid(), InviteeUserId), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
        result.FirstError.Code.Should().Be("Membership.NotFound");
        db.Memberships.Count().Should().Be(0);
        db.ChangeLogs.Count().Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenMembershipIsSoftDeleted_ShouldReturnNotFoundError()
    {
        var org = CreateOrganisation("Org");
        var invitee = CreateUser(InviteeUserId, "Invitee", "User", "invitee@example.com");
        var membership = CreatePendingMembership(invitee.Id.Value, org);
        var (sut, db, _) = CreateSut([org], [invitee], [membership]);

        db.Entry(membership).Property(nameof(Membership.IsDeleted)).CurrentValue = true;
        db.SaveChanges();

        var result = await sut.HandleAsync(AcceptCommand(membership.Id.Value, InviteeUserId), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Membership.NotFound");
        db.ChangeLogs.Count().Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotTheInvitee_ShouldReturnForbiddenError()
    {
        var org = CreateOrganisation("Org");
        var invitee = CreateUser(InviteeUserId, "Invitee", "User", "invitee@example.com");
        var membership = CreatePendingMembership(invitee.Id.Value, org);
        var (sut, db, _) = CreateSut([org], [invitee], [membership]);

        var result = await sut.HandleAsync(AcceptCommand(membership.Id.Value, OtherUserId), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Forbidden);
        result.FirstError.Code.Should().Be("Membership.Forbidden");

        var stored = db.Memberships.Single();
        stored.Status.Should().Be(MembershipStatus.Pending);
        stored.DecidedAt.Should().BeNull();
        db.ChangeLogs.Count().Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenMembershipIsAlreadyAccepted_ShouldReturnMustBePendingError()
    {
        var org = CreateOrganisation("Org");
        var invitee = CreateUser(InviteeUserId, "Invitee", "User", "invitee@example.com");
        var accepted = Membership.Create(
            UserId.From(invitee.Id.Value),
            org.Id,
            MembershipStatus.Accepted,
            new FakeTimeProvider(NowUtc)).Value;
        var (sut, db, _) = CreateSut([org], [invitee], [accepted]);

        var result = await sut.HandleAsync(AcceptCommand(accepted.Id.Value, InviteeUserId), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Membership.MustBePending");
        db.ChangeLogs.Count().Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenPending_ShouldAcceptAndSetDecidedAtAndWriteChangeLog()
    {
        var org = CreateOrganisation("Org");
        var invitee = CreateUser(InviteeUserId, "Invitee", "User", "invitee@example.com");
        var membership = CreatePendingMembership(invitee.Id.Value, org);
        var (sut, db, clock) = CreateSut([org], [invitee], [membership], LaterUtc);

        var result = await sut.HandleAsync(AcceptCommand(membership.Id.Value, InviteeUserId), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Status.Should().Be(MembershipStatus.Accepted);
        result.Value.DecidedAt.Should().Be(LaterUtc);
        result.Value.InvitedAt.Should().Be(NowUtc);

        var stored = db.Memberships.Single();
        stored.Status.Should().Be(MembershipStatus.Accepted);
        stored.DecidedAt.Should().Be(LaterUtc);

        db.ChangeLogs.Count().Should().Be(1);
        var changeLog = db.ChangeLogs.Single();
        changeLog.Message.Should().Be("Einladung angenommen");
        changeLog.OrganisationId.Value.Should().Be(org.Id.Value);
        changeLog.CreatedBy.Value.Should().Be(InviteeUserId);
        changeLog.CreatedAt.Should().Be(LaterUtc);
        changeLog.Category.Should().Be(KulturHub.Domain.ChangeLogs.ChangeLogCategory.Organisation);
        changeLog.Data["from"].Should().Be("Pending");
        changeLog.Data["to"].Should().Be("Accepted");
    }

    [Fact]
    public async Task Handle_WhenPending_ShouldRejectAndSetDecidedAtAndWriteChangeLog()
    {
        var org = CreateOrganisation("Org");
        var invitee = CreateUser(InviteeUserId, "Invitee", "User", "invitee@example.com");
        var membership = CreatePendingMembership(invitee.Id.Value, org);
        var (sut, db, _) = CreateSut([org], [invitee], [membership]);

        var command = new ChangeMembershipStatusCommand(membership.Id.Value, InviteeUserId, MembershipChangeStatus.Rejected);

        var result = await sut.HandleAsync(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Status.Should().Be(MembershipStatus.Rejected);
        result.Value.DecidedAt.Should().Be(NowUtc);

        var stored = db.Memberships.Single();
        stored.Status.Should().Be(MembershipStatus.Rejected);
        stored.DecidedAt.Should().Be(NowUtc);

        db.ChangeLogs.Count().Should().Be(1);
        var changeLog = db.ChangeLogs.Single();
        changeLog.Message.Should().Be("Einladung abgelehnt");
        changeLog.Category.Should().Be(KulturHub.Domain.ChangeLogs.ChangeLogCategory.Organisation);
        changeLog.Data["from"].Should().Be("Pending");
        changeLog.Data["to"].Should().Be("Rejected");
    }
}