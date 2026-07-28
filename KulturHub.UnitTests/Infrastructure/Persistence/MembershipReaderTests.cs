using FluentAssertions;
using KulturHub.Domain.Memberships;
using KulturHub.Domain.Organisations;
using KulturHub.Domain.Users;
using KulturHub.Infrastructure.Persistence;
using KulturHub.UnitTests.TestHelpers;
using Microsoft.Extensions.Time.Testing;

namespace KulturHub.UnitTests.Infrastructure.Persistence;

public class MembershipReaderTests
{
    private static readonly DateTime NowUtc = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OrganisationGuid = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherOrganisationGuid = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static (MembershipReader Sut, AppDbContext Db) CreateSut(
        IEnumerable<Membership> memberships)
    {
        var db = TestDbContextFactory.CreateInMemory();
        db.Memberships.AddRange(memberships);
        db.SaveChanges();

        return (new MembershipReader(db), db);
    }

    private static Membership CreateMembership(MembershipStatus status)
    {
        var membership = Membership.Create(
            UserId.From(UserGuid),
            OrganisationId.From(OrganisationGuid),
            MembershipStatus.Pending,
            new FakeTimeProvider(NowUtc)).Value;

        if (status != MembershipStatus.Pending)
        {
            membership.GetType()
                .GetProperty(nameof(Membership.Status))!
                .SetValue(membership, status);
        }

        return membership;
    }

    [Fact]
    public async Task IsMemberAsync_WhenMembershipIsAccepted_ShouldReturnTrue()
    {
        var membership = Membership.Create(
            UserId.From(UserGuid),
            OrganisationId.From(OrganisationGuid),
            MembershipStatus.Accepted,
            new FakeTimeProvider(NowUtc)).Value;
        var (sut, _) = CreateSut([membership]);

        var result = await sut.IsMemberAsync(UserGuid, OrganisationGuid, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsMemberAsync_WhenMembershipIsPending_ShouldReturnFalse()
    {
        var (sut, _) = CreateSut([CreateMembership(MembershipStatus.Pending)]);

        var result = await sut.IsMemberAsync(UserGuid, OrganisationGuid, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsMemberAsync_WhenMembershipIsRejected_ShouldReturnFalse()
    {
        var (sut, _) = CreateSut([CreateMembership(MembershipStatus.Rejected)]);

        var result = await sut.IsMemberAsync(UserGuid, OrganisationGuid, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsMemberAsync_WhenMembershipIsSoftDeleted_ShouldReturnFalse()
    {
        var membership = Membership.Create(
            UserId.From(UserGuid),
            OrganisationId.From(OrganisationGuid),
            MembershipStatus.Accepted,
            new FakeTimeProvider(NowUtc)).Value;
        var (sut, db) = CreateSut([membership]);

        db.Entry(membership).Property(nameof(Membership.IsDeleted)).CurrentValue = true;
        db.SaveChanges();

        var result = await sut.IsMemberAsync(UserGuid, OrganisationGuid, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsMemberAsync_WhenUserHasNoMembership_ShouldReturnFalse()
    {
        var (sut, _) = CreateSut([]);

        var result = await sut.IsMemberAsync(UserGuid, OrganisationGuid, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsMemberAsync_WhenOtherOrganisation_ShouldReturnFalse()
    {
        var membership = Membership.Create(
            UserId.From(UserGuid),
            OrganisationId.From(OtherOrganisationGuid),
            MembershipStatus.Accepted,
            new FakeTimeProvider(NowUtc)).Value;
        var (sut, _) = CreateSut([membership]);

        var result = await sut.IsMemberAsync(UserGuid, OrganisationGuid, CancellationToken.None);

        result.Should().BeFalse();
    }
}