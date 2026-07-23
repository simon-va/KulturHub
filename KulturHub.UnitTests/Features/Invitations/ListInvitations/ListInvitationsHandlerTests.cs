using FluentAssertions;
using KulturHub.Application.Features.Invitations.ListInvitations;
using KulturHub.Application.Ports;
using Moq;

namespace KulturHub.UnitTests.Features.Invitations.ListInvitations;

public class ListInvitationsHandlerTests
{
    // Rules:
    // - ExecuteAsync forwards the IncludeUsed/IncludeExpired flags to the repository.
    // - ExecuteAsync returns the list items from the repository as-is.

    private readonly Mock<IInvitationRepository> _invitationRepositoryMock = new();
    private readonly ListInvitationsHandler _sut;

    public ListInvitationsHandlerTests()
    {
        _sut = new ListInvitationsHandler(_invitationRepositoryMock.Object);
    }

    private static InvitationListItem ValidItem(bool expired = false, bool used = false) =>
        new(
            Guid.NewGuid(),
            "K3P-R2A",
            DateTime.UtcNow.AddDays(-1),
            expired ? DateTime.UtcNow.AddSeconds(-1) : DateTime.UtcNow.AddDays(7),
            used,
            expired,
            used ? Guid.NewGuid() : null,
            used ? "Anna Müller" : null);

    [Fact]
    public async Task ExecuteAsync_ShouldPassDefaultFilter_WhenNoFlagsProvided()
    {
        _invitationRepositoryMock
            .Setup(r => r.ListAsync(It.IsAny<InvitationFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<InvitationListItem>());

        var result = await _sut.ExecuteAsync(new ListInvitationsQuery(false, false));

        result.IsError.Should().BeFalse();
        _invitationRepositoryMock.Verify(
            r => r.ListAsync(
                It.Is<InvitationFilter>(f => f.IncludeUsed == false && f.IncludeExpired == false),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPassThroughIncludeFlags()
    {
        _invitationRepositoryMock
            .Setup(r => r.ListAsync(It.IsAny<InvitationFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<InvitationListItem>());

        var result = await _sut.ExecuteAsync(new ListInvitationsQuery(true, true));

        result.IsError.Should().BeFalse();
        _invitationRepositoryMock.Verify(
            r => r.ListAsync(
                It.Is<InvitationFilter>(f => f.IncludeUsed == true && f.IncludeExpired == true),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPassThroughItems_FromRepository_WhenInvitationIsUnused()
    {
        var item = ValidItem();
        _invitationRepositoryMock
            .Setup(r => r.ListAsync(It.IsAny<InvitationFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { item });

        var result = await _sut.ExecuteAsync(new ListInvitationsQuery(true, true));

        result.IsError.Should().BeFalse();
        var items = result.Value;
        items.Should().HaveCount(1);
        items[0].Should().BeSameAs(item);
        items[0].IsUsed.Should().BeFalse();
        items[0].IsExpired.Should().BeFalse();
        items[0].UsedById.Should().BeNull();
        items[0].UsedByFullName.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPassThroughItems_FromRepository_WhenInvitationIsUsed()
    {
        var userId = Guid.NewGuid();
        var item = new InvitationListItem(
            Guid.NewGuid(),
            "K3P-R2A",
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(7),
            IsUsed: true,
            IsExpired: false,
            UsedById: userId,
            UsedByFullName: "Anna Müller");

        _invitationRepositoryMock
            .Setup(r => r.ListAsync(It.IsAny<InvitationFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { item });

        var result = await _sut.ExecuteAsync(new ListInvitationsQuery(true, true));

        result.IsError.Should().BeFalse();
        var items = result.Value;
        items.Should().HaveCount(1);
        items[0].IsUsed.Should().BeTrue();
        items[0].UsedById.Should().Be(userId);
        items[0].UsedByFullName.Should().Be("Anna Müller");
    }
}
