using ErrorOr;
using FluentAssertions;
using KulturHub.Application.Errors;
using KulturHub.Application.Features.Platform.Memberships.InviteMembership;
using KulturHub.Domain.Memberships;
using KulturHub.Domain.Organisations;
using KulturHub.Domain.Users;
using KulturHub.Infrastructure.Persistence;
using KulturHub.Infrastructure.Users;
using KulturHub.UnitTests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace KulturHub.UnitTests.Features.Application.Platform.Memberships.InviteMembership;

public class InviteMembershipHandlerTests
{
    private static readonly DateTime NowUtc = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid InviterUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid InviteeUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private const string InviteeEmail = "invitee@example.com";

    private static (InviteMembershipHandler Sut, AppDbContext Db) CreateSut(
        IEnumerable<Organisation> organisations,
        IEnumerable<User> users,
        IEnumerable<Membership> memberships)
    {
        var db = TestDbContextFactory.CreateInMemory();
        db.Organisations.AddRange(organisations);
        db.Users.AddRange(users);
        db.Memberships.AddRange(memberships);
        db.SaveChanges();

        var clock = new FakeTimeProvider(NowUtc);
        var userReader = new UserReader(db);
        var handler = new InviteMembershipHandler(
            db, userReader, clock, NullLogger<InviteMembershipHandler>.Instance);

        return (handler, db);
    }

    private static Organisation CreateOrganisation(string name) =>
        Organisation.Create(name, new FakeTimeProvider(NowUtc)).Value;

    private static User CreateUser(Guid id, string firstName, string lastName, string email)
    {
        var userResult = User.Create(UserId.From(id), email, firstName, lastName, new FakeTimeProvider(NowUtc));
        return userResult.Value;
    }

    private static Membership CreateMembership(Guid userId, Organisation organisation) =>
        Membership.Create(UserId.From(userId), organisation.Id, MembershipStatus.Pending, new FakeTimeProvider(NowUtc)).Value;

    private static InviteMembershipCommand ValidCommand(Guid organisationId) =>
        new(organisationId, InviterUserId, InviteeEmail);

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ShouldReturnNotFoundError()
    {
        var org = CreateOrganisation("Org");
        var (sut, db) = CreateSut([org], [], []);

        var result = await sut.HandleAsync(ValidCommand(org.Id.Value), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
        result.FirstError.Code.Should().Be("Membership.UserNotFoundByEmail");
        db.Memberships.Count().Should().Be(0);
        db.ChangeLogs.Count().Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenMembershipAlreadyExists_ShouldReturnConflictError()
    {
        var org = CreateOrganisation("Org");
        var invitee = CreateUser(InviteeUserId, "Invitee", "User", InviteeEmail);
        var memberships = new[] { CreateMembership(invitee.Id.Value, org) };
        var (sut, db) = CreateSut([org], [invitee], memberships);

        var result = await sut.HandleAsync(ValidCommand(org.Id.Value), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        result.FirstError.Code.Should().Be("Membership.AlreadyExists");
        db.Memberships.Count().Should().Be(1);
        db.ChangeLogs.Count().Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenPreviousMembershipWasSoftDeleted_ShouldAllowNewInvitation()
    {
        var org = CreateOrganisation("Org");
        var invitee = CreateUser(InviteeUserId, "Invitee", "User", InviteeEmail);
        var softDeleted = CreateMembership(invitee.Id.Value, org);
        var (sut, db) = CreateSut([org], [invitee], [softDeleted]);

        db.Entry(softDeleted).Property(nameof(Membership.IsDeleted)).CurrentValue = true;
        db.SaveChanges();

        var result = await sut.HandleAsync(ValidCommand(org.Id.Value), CancellationToken.None);

        result.IsError.Should().BeFalse();
        db.Memberships.Count().Should().Be(1);
        db.Memberships.IgnoreQueryFilters().Count().Should().Be(2);
        db.ChangeLogs.Count().Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenRequestIsValid_ShouldCreatePendingMembershipAndChangeLog()
    {
        var org = CreateOrganisation("Org");
        var invitee = CreateUser(InviteeUserId, "Invitee", "User", InviteeEmail);
        var (sut, db) = CreateSut([org], [invitee], []);

        var result = await sut.HandleAsync(ValidCommand(org.Id.Value), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.UserId.Should().Be(invitee.Id.Value);
        result.Value.FullName.Should().Be("Invitee User");
        result.Value.Email.Should().Be(InviteeEmail);
        result.Value.Status.Should().Be(MembershipStatus.Pending);
        result.Value.InvitedAt.Should().Be(NowUtc);
        result.Value.DecidedAt.Should().BeNull();

        db.Memberships.Count().Should().Be(1);
        var membership = db.Memberships.Single();
        membership.Status.Should().Be(MembershipStatus.Pending);
        membership.UserId.Value.Should().Be(invitee.Id.Value);
        membership.OrganisationId.Value.Should().Be(org.Id.Value);
        membership.InvitedAt.Should().Be(NowUtc);
        membership.DecidedAt.Should().BeNull();

        db.ChangeLogs.Count().Should().Be(1);
        var changeLog = db.ChangeLogs.Single();
        changeLog.Message.Should().Be("Nutzer wurde eingeladen");
        changeLog.OrganisationId.Value.Should().Be(org.Id.Value);
        changeLog.CreatedBy.Value.Should().Be(InviterUserId);
        changeLog.CreatedAt.Should().Be(NowUtc);
        changeLog.Data.Should().ContainKey("email");
        changeLog.Data["email"].Should().Be(InviteeEmail);
        changeLog.Data.Should().ContainKey("status");
        changeLog.Data["status"].Should().Be("Pending");
    }
}