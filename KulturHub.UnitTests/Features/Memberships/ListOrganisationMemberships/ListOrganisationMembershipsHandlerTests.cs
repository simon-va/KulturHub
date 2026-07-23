using ErrorOr;
using FluentAssertions;
using KulturHub.Application.Features.Memberships.ListOrganisationMemberships;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using Moq;

namespace KulturHub.UnitTests.Features.Memberships.ListOrganisationMemberships;

public class ListOrganisationMembershipsHandlerTests
{
    // Rules:
    // - ExecuteAsync forwards the organisationId to the repository.
    // - ExecuteAsync returns the list items from the repository as-is.
    // - If the organisationId is Guid.Empty, a Validation error is returned and the
    //   repository is not called.
    // - The repository's CancellationToken is forwarded to the repository call.

    private readonly Mock<IMembershipRepository> _membershipRepositoryMock = new();
    private readonly ListOrganisationMembershipsHandler _sut;

    public ListOrganisationMembershipsHandlerTests()
    {
        _sut = new ListOrganisationMembershipsHandler(_membershipRepositoryMock.Object);
    }

    private static MembershipListItem ValidItem(
        Guid organisationId,
        string? firstName = null,
        string? lastName = null,
        string? email = null,
        MembershipStatus status = MembershipStatus.Accepted)
    {
        var fullName = (firstName, lastName) switch
        {
            (null or "", null or "") => null,
            ({ } f, null or "") => f,
            (null or "", { } l) => l,
            ({ } f, { } l) => $"{f} {l}".Trim(),
        };

        return new MembershipListItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            fullName,
            email,
            status,
            DateTime.UtcNow.AddDays(-1));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPassOrganisationIdToRepository()
    {
        var organisationId = Guid.NewGuid();
        _membershipRepositoryMock
            .Setup(r => r.ListActiveByOrganisationIdAsync(organisationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MembershipListItem>());

        await _sut.ExecuteAsync(organisationId);

        _membershipRepositoryMock.Verify(
            r => r.ListActiveByOrganisationIdAsync(organisationId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEmptyList_WhenOrganisationHasNoMemberships()
    {
        var organisationId = Guid.NewGuid();
        _membershipRepositoryMock
            .Setup(r => r.ListActiveByOrganisationIdAsync(organisationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MembershipListItem>());

        var result = await _sut.ExecuteAsync(organisationId);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPassThroughItems_FromRepository()
    {
        var organisationId = Guid.NewGuid();
        var item = ValidItem(organisationId, "Anna", "Müller", "anna@example.com");
        _membershipRepositoryMock
            .Setup(r => r.ListActiveByOrganisationIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { item });

        var result = await _sut.ExecuteAsync(organisationId);

        result.IsError.Should().BeFalse();
        var items = result.Value;
        items.Should().HaveCount(1);
        items[0].Should().BeSameAs(item);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOrganisationIdIsEmpty_ShouldReturnValidationError_AndNotCallRepository()
    {
        var result = await _sut.ExecuteAsync(Guid.Empty);

        result.IsError.Should().BeTrue();
        result.Errors.Should().AllSatisfy(e => e.Type.Should().Be(ErrorType.Validation));
        result.Errors.Should().ContainSingle(e => e.Code == "OrganisationId");

        _membershipRepositoryMock.Verify(
            r => r.ListActiveByOrganisationIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldForwardCancellationToken()
    {
        var organisationId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        _membershipRepositoryMock
            .Setup(r => r.ListActiveByOrganisationIdAsync(organisationId, cts.Token))
            .ReturnsAsync(Array.Empty<MembershipListItem>());

        await _sut.ExecuteAsync(organisationId, cts.Token);

        _membershipRepositoryMock.Verify(
            r => r.ListActiveByOrganisationIdAsync(organisationId, cts.Token),
            Times.Once);
    }
}
