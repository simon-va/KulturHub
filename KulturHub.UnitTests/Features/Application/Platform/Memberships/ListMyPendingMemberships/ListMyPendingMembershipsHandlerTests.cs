using FluentAssertions;
using KulturHub.Application.Features.Platform.Memberships.ListMyPendingMemberships;
using KulturHub.Domain.Memberships;
using KulturHub.Domain.Organisations;
using KulturHub.Domain.Users;
using KulturHub.Infrastructure.Persistence;
using KulturHub.UnitTests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace KulturHub.UnitTests.Features.Application.Platform.Memberships.ListMyPendingMemberships;

public class ListMyPendingMembershipsHandlerTests
{
    private static readonly DateTime NowUtc = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid CurrentUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static (ListMyPendingMembershipsHandler Sut, AppDbContext Db) CreateSut(
        IEnumerable<Organisation> organisations,
        IEnumerable<User> users,
        IEnumerable<Membership> memberships)
    {
        var db = TestDbContextFactory.CreateInMemory();
        db.Organisations.AddRange(organisations);
        db.Users.AddRange(users);
        db.Memberships.AddRange(memberships);
        db.SaveChanges();

        var handler = new ListMyPendingMembershipsHandler(
            db, NullLogger<ListMyPendingMembershipsHandler>.Instance);

        return (handler, db);
    }

    private static Organisation CreateOrganisation(string name) =>
        Organisation.Create(name, new FakeTimeProvider(NowUtc)).Value;

    private static Membership CreateMembership(
        Guid userId,
        Organisation organisation,
        MembershipStatus status = MembershipStatus.Pending) =>
        Membership.Create(UserId.From(userId), organisation.Id, status, new FakeTimeProvider(NowUtc)).Value;

    [Fact]
    public async Task Handle_WhenMultiplePendingMembershipsExist_ShouldReturnAllSortedByOrganisationName()
    {
        var charlieOrg = CreateOrganisation("Charlie");
        var alphaOrg = CreateOrganisation("Alpha");
        var bravoOrg = CreateOrganisation("Bravo");
        var memberships = new[]
        {
            CreateMembership(CurrentUserId, charlieOrg),
            CreateMembership(CurrentUserId, alphaOrg),
            CreateMembership(CurrentUserId, bravoOrg),
        };
        var (sut, _) = CreateSut([charlieOrg, alphaOrg, bravoOrg], [], memberships);

        var result = await sut.HandleAsync(CurrentUserId, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(3);
        result.Value.Select(p => p.OrganisationName)
            .Should().BeEquivalentTo(new[] { "Alpha", "Bravo", "Charlie" });
        result.Value.Should().OnlyContain(p => p.OrganisationId != Guid.Empty);
        result.Value.Should().OnlyContain(p => p.MembershipId != Guid.Empty);
    }

    [Fact]
    public async Task Handle_WhenNoMembershipsExist_ShouldReturnEmptyList()
    {
        var (sut, _) = CreateSut([], [], []);

        var result = await sut.HandleAsync(CurrentUserId, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenOtherUsersHavePendingMemberships_ShouldReturnOnlyCurrentUsers()
    {
        var mine = CreateOrganisation("Mine");
        var others = CreateOrganisation("Others");
        var memberships = new[]
        {
            CreateMembership(CurrentUserId, mine),
            CreateMembership(OtherUserId, others),
        };
        var (sut, _) = CreateSut([mine, others], [], memberships);

        var result = await sut.HandleAsync(CurrentUserId, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(1);
        result.Value.Single().OrganisationName.Should().Be("Mine");
        result.Value.Single().OrganisationId.Should().Be(mine.Id.Value);
        result.Value.Single().MembershipId.Should().Be(memberships[0].Id.Value);
    }

    [Fact]
    public async Task Handle_WhenMembershipsHaveMixedStatuses_ShouldReturnOnlyPending()
    {
        var alpha = CreateOrganisation("Alpha");
        var bravo = CreateOrganisation("Bravo");
        var charlie = CreateOrganisation("Charlie");
        var memberships = new[]
        {
            CreateMembership(CurrentUserId, alpha, MembershipStatus.Pending),
            CreateMembership(CurrentUserId, bravo, MembershipStatus.Accepted),
            CreateMembership(CurrentUserId, charlie, MembershipStatus.Rejected),
        };
        var (sut, _) = CreateSut([alpha, bravo, charlie], [], memberships);

        var result = await sut.HandleAsync(CurrentUserId, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(1);
        result.Value.Single().OrganisationName.Should().Be("Alpha");
    }

    [Fact]
    public async Task Handle_WhenMembershipIsSoftDeleted_ShouldExcludeIt()
    {
        var active = CreateOrganisation("Active");
        var removed = CreateOrganisation("Removed");
        var memberships = new[]
        {
            CreateMembership(CurrentUserId, active),
            CreateMembership(CurrentUserId, removed),
        };
        var (sut, db) = CreateSut([active, removed], [], memberships);

        db.Entry(memberships[1]).Property(nameof(Membership.IsDeleted)).CurrentValue = true;
        db.SaveChanges();

        var result = await sut.HandleAsync(CurrentUserId, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(1);
        result.Value.Single().OrganisationName.Should().Be("Active");
    }

    [Fact]
    public async Task Handle_WhenOrganisationIsSoftDeleted_ShouldExcludeIt()
    {
        var active = CreateOrganisation("Active");
        var softDeleted = CreateOrganisation("SoftDeleted");
        var memberships = new[]
        {
            CreateMembership(CurrentUserId, active),
            CreateMembership(CurrentUserId, softDeleted),
        };
        var (sut, db) = CreateSut([active, softDeleted], [], memberships);

        db.Entry(softDeleted).Property(nameof(Organisation.IsDeleted)).CurrentValue = true;
        db.SaveChanges();

        var result = await sut.HandleAsync(CurrentUserId, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(1);
        result.Value.Single().OrganisationName.Should().Be("Active");
    }
}