using ErrorOr;
using FluentAssertions;
using KulturHub.Application.Features.Memberships.ListMyPendingMemberships;
using KulturHub.Application.Ports;
using Moq;

namespace KulturHub.UnitTests.Features.Memberships.ListMyPendingMemberships;

public class ListMyPendingMembershipsHandlerTests
{
    // Rules:
    // - ExecuteAsync forwards the userId to the repository.
    // - ExecuteAsync returns the list items from the repository as-is.
    // - If the userId is Guid.Empty, a Validation error is returned and the
    //   repository is not called.
    // - The repository's CancellationToken is forwarded to the repository call.

    private readonly Mock<IMembershipRepository> _membershipRepositoryMock = new();
    private readonly ListMyPendingMembershipsHandler _sut;

    public ListMyPendingMembershipsHandlerTests()
    {
        _sut = new ListMyPendingMembershipsHandler(_membershipRepositoryMock.Object);
    }

    private static PendingMembershipListItem ValidItem(
        string? inviterFirstName = null,
        string? inviterLastName = null,
        string organisationName = "Test Org")
    {
        var inviterName = (inviterFirstName, inviterLastName) switch
        {
            (null or "", null or "") => null,
            ({ } f, null or "") => f,
            (null or "", { } l) => l,
            ({ } f, { } l) => $"{f} {l}".Trim(),
        };

        return new PendingMembershipListItem(
            Guid.NewGuid(),
            inviterName,
            organisationName);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPassUserIdToRepository()
    {
        var userId = Guid.NewGuid();
        _membershipRepositoryMock
            .Setup(r => r.ListPendingByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PendingMembershipListItem>());

        await _sut.ExecuteAsync(userId);

        _membershipRepositoryMock.Verify(
            r => r.ListPendingByUserIdAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEmptyList_WhenUserHasNoPendingMemberships()
    {
        var userId = Guid.NewGuid();
        _membershipRepositoryMock
            .Setup(r => r.ListPendingByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PendingMembershipListItem>());

        var result = await _sut.ExecuteAsync(userId);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPassThroughItems_FromRepository()
    {
        var userId = Guid.NewGuid();
        var item = ValidItem("Anna", "Müller", "Kulturverein");
        _membershipRepositoryMock
            .Setup(r => r.ListPendingByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { item });

        var result = await _sut.ExecuteAsync(userId);

        result.IsError.Should().BeFalse();
        var items = result.Value;
        items.Should().HaveCount(1);
        items[0].Should().BeSameAs(item);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserIdIsEmpty_ShouldReturnValidationError_AndNotCallRepository()
    {
        var result = await _sut.ExecuteAsync(Guid.Empty);

        result.IsError.Should().BeTrue();
        result.Errors.Should().AllSatisfy(e => e.Type.Should().Be(ErrorType.Validation));
        result.Errors.Should().ContainSingle(e => e.Code == "UserId");

        _membershipRepositoryMock.Verify(
            r => r.ListPendingByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldForwardCancellationToken()
    {
        var userId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        _membershipRepositoryMock
            .Setup(r => r.ListPendingByUserIdAsync(userId, cts.Token))
            .ReturnsAsync(Array.Empty<PendingMembershipListItem>());

        await _sut.ExecuteAsync(userId, cts.Token);

        _membershipRepositoryMock.Verify(
            r => r.ListPendingByUserIdAsync(userId, cts.Token),
            Times.Once);
    }
}
