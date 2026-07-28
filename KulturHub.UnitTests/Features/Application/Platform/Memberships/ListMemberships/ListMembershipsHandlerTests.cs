using FluentAssertions;
using KulturHub.Application.Features.Platform.Memberships.ListMemberships;
using KulturHub.Domain.Memberships;
using KulturHub.Domain.Organisations;
using KulturHub.Domain.Users;
using KulturHub.Infrastructure.Persistence;
using KulturHub.UnitTests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace KulturHub.UnitTests.Features.Application.Platform.Memberships.ListMemberships;

public class ListMembershipsHandlerTests
{
    private static readonly DateTime NowUtc = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid CurrentUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static (ListMembershipsHandler Sut, AppDbContext Db) CreateSut(
        IEnumerable<Organisation> organisations,
        IEnumerable<User> users,
        IEnumerable<Membership> memberships)
    {
        var db = TestDbContextFactory.CreateInMemory();
        db.Organisations.AddRange(organisations);
        db.Users.AddRange(users);
        db.Memberships.AddRange(memberships);
        db.SaveChanges();

        var handler = new ListMembershipsHandler(
            db, NullLogger<ListMembershipsHandler>.Instance);

        return (handler, db);
    }

    private static Organisation CreateOrganisation(string name) =>
        Organisation.Create(name, new FakeTimeProvider(NowUtc)).Value;

    private static User CreateUser(string firstName, string lastName, string email) =>
        User.Create(UserId.New(), email, firstName, lastName, new FakeTimeProvider(NowUtc)).Value;

    private static Membership CreateMembership(Guid userId, Organisation organisation) =>
        Membership.Create(UserId.From(userId), organisation.Id, new FakeTimeProvider(NowUtc)).Value;

    [Fact]
    public async Task Handle_WhenMultipleMembersExist_ShouldReturnAllSortedByFullName()
    {
        var org = CreateOrganisation("Org");
        var alice = CreateUser("Alice", "Anders", "alice@example.com");
        var charlie = CreateUser("Charlie", "Cook", "charlie@example.com");
        var bob = CreateUser("Bob", "Brown", "bob@example.com");
        var memberships = new[]
        {
            CreateMembership(alice.Id.Value, org),
            CreateMembership(charlie.Id.Value, org),
            CreateMembership(bob.Id.Value, org),
        };
        var (sut, _) = CreateSut([org], [alice, charlie, bob], memberships);

        var result = await sut.HandleAsync(org.Id.Value, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(3);
        result.Value.Select(m => m.FullName)
            .Should().ContainInOrder("Alice Anders", "Bob Brown", "Charlie Cook");
    }

    [Fact]
    public async Task Handle_WhenNoMembers_ShouldReturnEmptyList()
    {
        var org = CreateOrganisation("Empty");
        var (sut, _) = CreateSut([org], [], []);

        var result = await sut.HandleAsync(org.Id.Value, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenMembershipIsSoftDeleted_ShouldExcludeIt()
    {
        var org = CreateOrganisation("Org");
        var alice = CreateUser("Alice", "Anders", "alice@example.com");
        var bob = CreateUser("Bob", "Brown", "bob@example.com");
        var memberships = new[]
        {
            CreateMembership(alice.Id.Value, org),
            CreateMembership(bob.Id.Value, org),
        };
        var (sut, db) = CreateSut([org], [alice, bob], memberships);

        db.Entry(memberships[1]).Property(nameof(Membership.IsDeleted)).CurrentValue = true;
        db.SaveChanges();

        var result = await sut.HandleAsync(org.Id.Value, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(1);
        result.Value.Single().UserId.Should().Be(alice.Id.Value);
    }

    [Fact]
    public async Task Handle_WhenMembershipReferencesMissingUser_ShouldExcludeMembershipViaInnerJoin()
    {
        var org = CreateOrganisation("Org");
        var alice = CreateUser("Alice", "Anders", "alice@example.com");
        var memberships = new[]
        {
            CreateMembership(alice.Id.Value, org),
            CreateMembership(OtherUserId, org),
        };
        var (sut, _) = CreateSut([org], [alice], memberships);

        var result = await sut.HandleAsync(org.Id.Value, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(1);
        result.Value.Single().UserId.Should().Be(alice.Id.Value);
    }

    [Fact]
    public async Task Handle_WhenOtherOrganisationsHaveMembers_ShouldReturnOnlyRequestedOrganisations()
    {
        var orgA = CreateOrganisation("OrgA");
        var orgB = CreateOrganisation("OrgB");
        var alice = CreateUser("Alice", "Anders", "alice@example.com");
        var bob = CreateUser("Bob", "Brown", "bob@example.com");
        var memberships = new[]
        {
            CreateMembership(alice.Id.Value, orgA),
            CreateMembership(bob.Id.Value, orgB),
        };
        var (sut, _) = CreateSut([orgA, orgB], [alice, bob], memberships);

        var result = await sut.HandleAsync(orgA.Id.Value, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(1);
        result.Value.Single().UserId.Should().Be(alice.Id.Value);
        result.Value.Single().FullName.Should().Be("Alice Anders");
    }

    [Fact]
    public async Task Handle_ShouldComposeFullNameFromFirstAndLastName()
    {
        var org = CreateOrganisation("Org");
        var user = CreateUser("Diana", "Doe", "diana@example.com");
        var memberships = new[]
        {
            CreateMembership(user.Id.Value, org),
        };
        var (sut, _) = CreateSut([org], [user], memberships);

        var result = await sut.HandleAsync(org.Id.Value, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Single().FullName.Should().Be("Diana Doe");
        result.Value.Single().Email.Should().Be("diana@example.com");
        result.Value.Single().UserId.Should().Be(user.Id.Value);
        result.Value.Single().JoinedAt.Should().Be(NowUtc);
    }
}