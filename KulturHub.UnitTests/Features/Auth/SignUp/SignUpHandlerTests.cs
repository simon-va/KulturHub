using ErrorOr;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using KulturHub.Application.Errors;
using KulturHub.Application.Features.Auth.SignUp;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace KulturHub.UnitTests.Features.Auth.SignUp;

public class SignUpHandlerTests
{
    // Rules:
    // - Validation runs first; invalid input short-circuits before any other dependency is touched.
    // - Invitation not found -> InvitationErrors.NotFound.
    // - Invitation expired -> InvitationErrors.Expired.
    // - Invitation already used -> InvitationErrors.AlreadyUsed.
    // - Auth provider errors are propagated unchanged.
    // - On successful auth provider call, the user is inserted and the invitation is marked as used.
    // - If marking the invitation as used returns InvitationErrors.AlreadyUsed (concurrent claim),
    //   the auth user is rolled back via IUserAdminClient and the error is propagated.
    // - If inserting the user or marking the invitation throws any other exception,
    //   the auth user is rolled back and AuthErrors.DatabaseInsertFailed is returned.

    private readonly Mock<IAuthProvider> _authProviderMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IInvitationRepository> _invitationRepositoryMock = new();
    private readonly Mock<IUserAdminClient> _userAdminClientMock = new();
    private readonly Mock<IValidator<SignUpInput>> _validatorMock = new();
    private readonly Mock<ILogger<SignUpHandler>> _loggerMock = new();

    private readonly SignUpHandler _sut;

    public SignUpHandlerTests()
    {
        _sut = new SignUpHandler(
            _authProviderMock.Object,
            _userRepositoryMock.Object,
            _invitationRepositoryMock.Object,
            _userAdminClientMock.Object,
            _validatorMock.Object,
            _loggerMock.Object);
    }

    private static SignUpInput ValidInput() =>
        new("Max", "Mustermann", "max@example.com", "Secret123!", "K3P-R2A");

    private static Invitation ValidInvitation(bool expired = false, bool used = false) =>
        Invitation.Reconstitute(
            Guid.NewGuid(),
            "K3P-R2A",
            used ? Guid.NewGuid() : null,
            DateTime.UtcNow.AddDays(-1),
            expired ? DateTime.UtcNow.AddSeconds(-1) : DateTime.UtcNow.AddDays(7));

    private static AuthProviderSession ValidSession() =>
        new("access", "refresh", Guid.NewGuid());

    [Fact]
    public async Task ExecuteAsync_WhenAllInputsAreValid_ShouldReturnSignUpResponse()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<SignUpInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        var invitation = ValidInvitation();
        _invitationRepositoryMock
            .Setup(r => r.GetByCodeAsync("K3P-R2A", It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        var session = ValidSession();
        _authProviderMock
            .Setup(p => p.SignUpAsync("max@example.com", "Secret123!"))
            .ReturnsAsync(session);
        _userRepositoryMock
            .Setup(r => r.InsertUserAsync(It.IsAny<User>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _invitationRepositoryMock
            .Setup(r => r.MarkAsUsedAsync(invitation.Id, session.UserId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success);

        var result = await _sut.ExecuteAsync(ValidInput());

        result.IsError.Should().BeFalse();
        var response = result.Value;
        response.AccessToken.Should().Be(session.AccessToken);
        response.RefreshToken.Should().Be(session.RefreshToken);
        response.UserId.Should().Be(session.UserId);
        response.FirstName.Should().Be("Max");
        response.LastName.Should().Be("Mustermann");

        _userRepositoryMock.Verify(
            r => r.InsertUserAsync(
                It.Is<User>(u => u.UserId == session.UserId
                    && u.FirstName == "Max"
                    && u.LastName == "Mustermann"
                    && u.Email == "max@example.com"),
                It.IsAny<IUnitOfWorkTransaction?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _invitationRepositoryMock.Verify(
            r => r.MarkAsUsedAsync(invitation.Id, session.UserId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _userAdminClientMock.Verify(c => c.DeleteUserAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInputIsInvalid_ShouldReturnValidationErrors_AndNotCallDependencies()
    {
        var failures = new[]
        {
            new ValidationFailure("FirstName", "FirstName is required."),
            new ValidationFailure("Email", "Email must be a valid email address."),
        };
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<SignUpInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(failures));

        var result = await _sut.ExecuteAsync(ValidInput());

        result.IsError.Should().BeTrue();
        result.Errors.Should().AllSatisfy(e => e.Type.Should().Be(ErrorType.Validation));
        result.Errors.Should().Contain(e => e.Code == "FirstName");
        result.Errors.Should().Contain(e => e.Code == "Email");

        _invitationRepositoryMock.Verify(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _authProviderMock.Verify(p => p.SignUpAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _userRepositoryMock.Verify(r => r.InsertUserAsync(It.IsAny<User>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()), Times.Never);
        _invitationRepositoryMock.Verify(r => r.MarkAsUsedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInvitationIsMissing_ShouldReturnInvitationNotFound_AndNotCallAuthProvider()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<SignUpInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _invitationRepositoryMock
            .Setup(r => r.GetByCodeAsync("K3P-R2A", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Invitation?)null);

        var result = await _sut.ExecuteAsync(ValidInput());

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == InvitationErrors.NotFound.Code);
        _authProviderMock.Verify(p => p.SignUpAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInvitationIsExpired_ShouldReturnInvitationExpired()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<SignUpInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _invitationRepositoryMock
            .Setup(r => r.GetByCodeAsync("K3P-R2A", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidInvitation(expired: true));

        var result = await _sut.ExecuteAsync(ValidInput());

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == InvitationErrors.Expired.Code);
        _authProviderMock.Verify(p => p.SignUpAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInvitationIsAlreadyUsed_ShouldReturnInvitationAlreadyUsed()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<SignUpInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _invitationRepositoryMock
            .Setup(r => r.GetByCodeAsync("K3P-R2A", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidInvitation(used: true));

        var result = await _sut.ExecuteAsync(ValidInput());

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == InvitationErrors.AlreadyUsed.Code);
        _authProviderMock.Verify(p => p.SignUpAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAuthProviderReturnsError_ShouldPropagateError_AndNotInsertUser()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<SignUpInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _invitationRepositoryMock
            .Setup(r => r.GetByCodeAsync("K3P-R2A", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidInvitation());
        _authProviderMock
            .Setup(p => p.SignUpAsync("max@example.com", "Secret123!"))
            .ReturnsAsync(AuthErrors.AlreadyRegistered);

        var result = await _sut.ExecuteAsync(ValidInput());

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == AuthErrors.AlreadyRegistered.Code);
        _userRepositoryMock.Verify(r => r.InsertUserAsync(It.IsAny<User>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()), Times.Never);
        _invitationRepositoryMock.Verify(r => r.MarkAsUsedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDatabaseInsertThrows_ShouldRollBackAuthUser_AndReturnDatabaseInsertFailed()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<SignUpInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        var invitation = ValidInvitation();
        _invitationRepositoryMock
            .Setup(r => r.GetByCodeAsync("K3P-R2A", It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        var session = ValidSession();
        _authProviderMock
            .Setup(p => p.SignUpAsync("max@example.com", "Secret123!"))
            .ReturnsAsync(session);
        _userRepositoryMock
            .Setup(r => r.InsertUserAsync(It.IsAny<User>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("connection lost"));

        var result = await _sut.ExecuteAsync(ValidInput());

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Code.Should().StartWith("Auth.DatabaseInsertFailed");
        result.Errors[0].Description.Should().Contain("connection lost");

        _userAdminClientMock.Verify(c => c.DeleteUserAsync(session.UserId), Times.Once);
        _invitationRepositoryMock.Verify(
            r => r.MarkAsUsedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInvitationIsClaimedConcurrently_ShouldRollBackAuthUser_AndReturnAlreadyUsed()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<SignUpInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        var invitation = ValidInvitation();
        _invitationRepositoryMock
            .Setup(r => r.GetByCodeAsync("K3P-R2A", It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        var session = ValidSession();
        _authProviderMock
            .Setup(p => p.SignUpAsync("max@example.com", "Secret123!"))
            .ReturnsAsync(session);
        _userRepositoryMock
            .Setup(r => r.InsertUserAsync(It.IsAny<User>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _invitationRepositoryMock
            .Setup(r => r.MarkAsUsedAsync(invitation.Id, session.UserId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(InvitationErrors.AlreadyUsed);

        var result = await _sut.ExecuteAsync(ValidInput());

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == InvitationErrors.AlreadyUsed.Code);
        result.Errors[0].Type.Should().Be(ErrorType.Conflict);

        _userAdminClientMock.Verify(c => c.DeleteUserAsync(session.UserId), Times.Once);
    }
}
