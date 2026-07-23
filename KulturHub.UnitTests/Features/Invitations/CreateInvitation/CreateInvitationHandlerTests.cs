using FluentAssertions;
using KulturHub.Application.Features.Invitations.CreateInvitation;
using KulturHub.Application.Ports;
using KulturHub.Application.Rules;
using KulturHub.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace KulturHub.UnitTests.Features.Invitations.CreateInvitation;

public class CreateInvitationHandlerTests
{
    // Rules:
    // - ExecuteAsync generates a new invitation and persists it via the repository.
    // - ExecuteAsync returns a failure error when the repository throws.

    private readonly Mock<IInvitationRepository> _invitationRepositoryMock = new();
    private readonly ILogger<CreateInvitationHandler> _logger = NullLogger<CreateInvitationHandler>.Instance;
    private readonly CreateInvitationHandler _sut;

    public CreateInvitationHandlerTests()
    {
        _sut = new CreateInvitationHandler(_invitationRepositoryMock.Object, _logger);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldGenerateInvitation_AndPersistViaRepository()
    {
        Invitation? persisted = null;
        _invitationRepositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<Invitation>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .Callback<Invitation, IUnitOfWorkTransaction?, CancellationToken>((inv, _, _) => persisted = inv)
            .Returns(Task.CompletedTask);

        var result = await _sut.ExecuteAsync();

        result.IsError.Should().BeFalse();
        var response = result.Value;

        persisted.Should().NotBeNull();
        persisted!.Id.Should().Be(response.Id);
        persisted.Code.Should().Be(response.Code);
        persisted.UsedBy.Should().BeNull();
        persisted.ExpiresAt.Should().BeAfter(DateTime.UtcNow);

        response.Code.Should().HaveLength(7);
        response.Code.Should().MatchRegex(InvitationCodeRules.CodePattern);
        response.IsUsed.Should().BeFalse();
        response.IsExpired.Should().BeFalse();

        _invitationRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<Invitation>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryThrows_ShouldReturnCreateFailedError()
    {
        _invitationRepositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<Invitation>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        var result = await _sut.ExecuteAsync();

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Code.Should().Be("Invitation.CreateFailed");
        result.Errors[0].Description.Should().Contain("db down");
    }
}
