using ErrorOr;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using KulturHub.Application.Errors;
using KulturHub.Application.Features.Auth.ValidateInvitation;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using Moq;

namespace KulturHub.UnitTests.Features.Auth.ValidateInvitation;

public class ValidateInvitationHandlerTests
{
    // Rules:
    // - Validation runs first; invalid input short-circuits before the repository is touched.
    // - Invitation not found -> InvitationErrors.NotFound.
    // - Invitation expired -> InvitationErrors.Expired.
    // - Invitation already used -> InvitationErrors.AlreadyUsed.
    // - On success, no state-changing repository method is ever invoked.

    private readonly Mock<IInvitationRepository> _invitationRepositoryMock = new();
    private readonly Mock<IValidator<ValidateInvitationInput>> _validatorMock = new();
    private readonly ValidateInvitationHandler _sut;

    public ValidateInvitationHandlerTests()
    {
        _sut = new ValidateInvitationHandler(
            _invitationRepositoryMock.Object,
            _validatorMock.Object);
    }

    private static ValidateInvitationInput ValidInput() =>
        new("K3P-R2A");

    private static Invitation ValidInvitation(bool expired = false, bool used = false) =>
        Invitation.Reconstitute(
            Guid.NewGuid(),
            "K3P-R2A",
            used ? Guid.NewGuid() : null,
            DateTime.UtcNow.AddDays(-1),
            expired ? DateTime.UtcNow.AddSeconds(-1) : DateTime.UtcNow.AddDays(7));

    [Fact]
    public async Task ExecuteAsync_WhenAllInputsAreValid_ShouldReturnSuccess()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<ValidateInvitationInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _invitationRepositoryMock
            .Setup(r => r.GetByCodeAsync("K3P-R2A", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidInvitation());

        var result = await _sut.ExecuteAsync(ValidInput());

        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WhenInputIsInvalid_ShouldReturnValidationErrors_AndNotCallRepository()
    {
        var failures = new[]
        {
            new ValidationFailure("InvitationCode", "InvitationCode is required."),
        };
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<ValidateInvitationInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(failures));

        var result = await _sut.ExecuteAsync(ValidInput());

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == "InvitationCode");
        result.Errors[0].Type.Should().Be(ErrorType.Validation);

        _invitationRepositoryMock.Verify(
            r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInvitationIsMissing_ShouldReturnInvitationNotFound()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<ValidateInvitationInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _invitationRepositoryMock
            .Setup(r => r.GetByCodeAsync("K3P-R2A", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Invitation?)null);

        var result = await _sut.ExecuteAsync(ValidInput());

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == InvitationErrors.NotFound.Code);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInvitationIsExpired_ShouldReturnInvitationExpired()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<ValidateInvitationInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _invitationRepositoryMock
            .Setup(r => r.GetByCodeAsync("K3P-R2A", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidInvitation(expired: true));

        var result = await _sut.ExecuteAsync(ValidInput());

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == InvitationErrors.Expired.Code);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInvitationIsAlreadyUsed_ShouldReturnInvitationAlreadyUsed()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<ValidateInvitationInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _invitationRepositoryMock
            .Setup(r => r.GetByCodeAsync("K3P-R2A", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidInvitation(used: true));

        var result = await _sut.ExecuteAsync(ValidInput());

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == InvitationErrors.AlreadyUsed.Code);
    }

    [Fact]
    public async Task ExecuteAsync_OnSuccess_ShouldNotCallAnyStateChangingRepositoryMethod()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<ValidateInvitationInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _invitationRepositoryMock
            .Setup(r => r.GetByCodeAsync("K3P-R2A", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidInvitation());

        await _sut.ExecuteAsync(ValidInput());

        _invitationRepositoryMock.Verify(
            r => r.MarkAsUsedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _invitationRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<Invitation>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
