using ErrorOr;
using FluentAssertions;
using KulturHub.Application.Errors;
using KulturHub.Application.Features.Invitations.DeleteInvitation;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace KulturHub.UnitTests.Features.Invitations.DeleteInvitation;

public class DeleteInvitationHandlerTests
{
    // Rules:
    // - ExecuteAsync returns NotFound when the invitation does not exist.
    // - ExecuteAsync returns DeleteAlreadyUsed (Conflict) when the invitation is used.
    // - ExecuteAsync returns DeleteAlreadyUsed (Conflict) when the repository reports 0 affected rows (race).
    // - ExecuteAsync returns Result.Deleted on success and calls the repository's DeleteAsync.
    // - ExecuteAsync shadow-deletes the domain entity before persisting (IsDeleted + DeletedAt are set).

    private readonly Mock<IInvitationRepository> _invitationRepositoryMock = new();
    private readonly ILogger<DeleteInvitationHandler> _logger = NullLogger<DeleteInvitationHandler>.Instance;
    private readonly DeleteInvitationHandler _sut;

    public DeleteInvitationHandlerTests()
    {
        _sut = new DeleteInvitationHandler(_invitationRepositoryMock.Object, _logger);
    }

    private static Invitation ValidInvitation(bool expired = false, bool used = false) =>
        Invitation.Reconstitute(
            Guid.NewGuid(),
            "K3P-R2A",
            used ? Guid.NewGuid() : null,
            DateTime.UtcNow.AddDays(-1),
            expired ? DateTime.UtcNow.AddSeconds(-1) : DateTime.UtcNow.AddDays(7));

    [Fact]
    public async Task ExecuteAsync_WhenInvitationDoesNotExist_ShouldReturnNotFound()
    {
        var id = Guid.NewGuid();
        _invitationRepositoryMock
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Invitation?)null);

        var result = await _sut.ExecuteAsync(id);

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == InvitationErrors.NotFound.Code);
        _invitationRepositoryMock.Verify(
            r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInvitationIsUsed_ShouldReturnDeleteAlreadyUsed_AndNotDelete()
    {
        var invitation = ValidInvitation(used: true);
        _invitationRepositoryMock
            .Setup(r => r.GetByIdAsync(invitation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var result = await _sut.ExecuteAsync(invitation.Id);

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == InvitationErrors.DeleteAlreadyUsed.Code);
        result.Errors[0].Type.Should().Be(ErrorType.Conflict);
        _invitationRepositoryMock.Verify(
            r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryReturnsZeroAffectedRows_ShouldReturnDeleteAlreadyUsed()
    {
        var invitation = ValidInvitation();
        _invitationRepositoryMock
            .Setup(r => r.GetByIdAsync(invitation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        _invitationRepositoryMock
            .Setup(r => r.DeleteAsync(invitation.Id, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _sut.ExecuteAsync(invitation.Id);

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == InvitationErrors.DeleteAlreadyUsed.Code);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOpen_ShouldReturnDeleted_AndCallRepository()
    {
        var invitation = ValidInvitation();
        _invitationRepositoryMock
            .Setup(r => r.GetByIdAsync(invitation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        _invitationRepositoryMock
            .Setup(r => r.DeleteAsync(invitation.Id, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _sut.ExecuteAsync(invitation.Id);

        result.IsError.Should().BeFalse();
        result.Value.Should().Be(Result.Deleted);
        _invitationRepositoryMock.Verify(
            r => r.DeleteAsync(invitation.Id, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_OnSuccess_ShouldShadowDeleteTheDomainEntity()
    {
        var invitation = ValidInvitation();
        invitation.IsDeleted.Should().BeFalse();
        invitation.DeletedAt.Should().BeNull();

        _invitationRepositoryMock
            .Setup(r => r.GetByIdAsync(invitation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        _invitationRepositoryMock
            .Setup(r => r.DeleteAsync(invitation.Id, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        await _sut.ExecuteAsync(invitation.Id);

        invitation.IsDeleted.Should().BeTrue();
        invitation.DeletedAt.Should().NotBeNull();
    }
}
