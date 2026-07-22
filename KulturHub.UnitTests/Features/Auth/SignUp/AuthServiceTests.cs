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

public class AuthServiceTests
{
    // Rules:
    // - Validation runs first; invalid input short-circuits before any other dependency is touched.
    // - Invitation not found -> InvitationErrors.NotFound.
    // - Invitation expired -> InvitationErrors.Expired.
    // - Invitation already used -> InvitationErrors.AlreadyUsed.
    // - Auth provider errors are propagated unchanged.
    // - On successful auth provider call, user is inserted into the database.
    // - If the database insert throws InvitationAlreadyUsedException (concurrent claim), the auth user
    //   is rolled back via ISupabaseAdminClient and InvitationErrors.AlreadyUsed is returned.
    // - If the database insert throws any other exception, the auth user is rolled back
    //   and AuthErrors.DatabaseInsertFailed is returned.

    private readonly Mock<IAuthProvider> _authProviderMock = new();
    private readonly Mock<IAuthRepository> _authRepositoryMock = new();
    private readonly Mock<ISupabaseAdminClient> _supabaseAdminClientMock = new();
    private readonly Mock<IValidator<SignUpInput>> _validatorMock = new();
    private readonly Mock<ILogger<AuthService>> _loggerMock = new();

    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _sut = new AuthService(
            _authProviderMock.Object,
            _authRepositoryMock.Object,
            _supabaseAdminClientMock.Object,
            _validatorMock.Object,
            _loggerMock.Object);
    }

    private static SignUpInput ValidInput() =>
        new("Max", "Mustermann", "max@example.com", "Secret123!", "INVITE01");

    private static Invitation ValidInvitation(bool expired = false, bool used = false) =>
        Invitation.Reconstitute(
            Guid.NewGuid(),
            "INVITE01",
            used ? Guid.NewGuid() : null,
            DateTime.UtcNow.AddDays(-1),
            expired ? DateTime.UtcNow.AddSeconds(-1) : DateTime.UtcNow.AddDays(7));

    private static AuthProviderSession ValidSession() =>
        new("access", "refresh", Guid.NewGuid());

    [Fact]
    public async Task SignUpAsync_WhenAllInputsAreValid_ShouldReturnAuthResponse()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<SignUpInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        var invitation = ValidInvitation();
        _authRepositoryMock
            .Setup(r => r.GetInvitationByCodeAsync("INVITE01"))
            .ReturnsAsync(invitation);
        var session = ValidSession();
        _authProviderMock
            .Setup(p => p.SignUpAsync("max@example.com", "Secret123!"))
            .ReturnsAsync(session);
        _authRepositoryMock
            .Setup(r => r.InsertUserAsync(It.IsAny<User>(), invitation.Id))
            .Returns(Task.CompletedTask);

        var result = await _sut.SignUpAsync(ValidInput());

        result.IsError.Should().BeFalse();
        var response = result.Value;
        response.AccessToken.Should().Be(session.AccessToken);
        response.RefreshToken.Should().Be(session.RefreshToken);
        response.UserId.Should().Be(session.UserId);
        response.FirstName.Should().Be("Max");
        response.LastName.Should().Be("Mustermann");

        _authRepositoryMock.Verify(
            r => r.InsertUserAsync(
                It.Is<User>(u => u.UserId == session.UserId && u.FirstName == "Max" && u.LastName == "Mustermann"),
                invitation.Id),
            Times.Once);
        _supabaseAdminClientMock.Verify(c => c.DeleteUserAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task SignUpAsync_WhenInputIsInvalid_ShouldReturnValidationErrors_AndNotCallDependencies()
    {
        var failures = new[]
        {
            new ValidationFailure("FirstName", "FirstName is required."),
            new ValidationFailure("Email", "Email must be a valid email address."),
        };
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<SignUpInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(failures));

        var result = await _sut.SignUpAsync(ValidInput());

        result.IsError.Should().BeTrue();
        result.Errors.Should().AllSatisfy(e => e.Type.Should().Be(ErrorType.Validation));
        result.Errors.Should().Contain(e => e.Code == "FirstName");
        result.Errors.Should().Contain(e => e.Code == "Email");

        _authRepositoryMock.Verify(r => r.GetInvitationByCodeAsync(It.IsAny<string>()), Times.Never);
        _authProviderMock.Verify(p => p.SignUpAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _authRepositoryMock.Verify(r => r.InsertUserAsync(It.IsAny<User>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task SignUpAsync_WhenInvitationIsMissing_ShouldReturnInvitationNotFound_AndNotCallAuthProvider()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<SignUpInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _authRepositoryMock
            .Setup(r => r.GetInvitationByCodeAsync("INVITE01"))
            .ReturnsAsync((Invitation?)null);

        var result = await _sut.SignUpAsync(ValidInput());

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == InvitationErrors.NotFound.Code);
        _authProviderMock.Verify(p => p.SignUpAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SignUpAsync_WhenInvitationIsExpired_ShouldReturnInvitationExpired()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<SignUpInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _authRepositoryMock
            .Setup(r => r.GetInvitationByCodeAsync("INVITE01"))
            .ReturnsAsync(ValidInvitation(expired: true));

        var result = await _sut.SignUpAsync(ValidInput());

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == InvitationErrors.Expired.Code);
        _authProviderMock.Verify(p => p.SignUpAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SignUpAsync_WhenInvitationIsAlreadyUsed_ShouldReturnInvitationAlreadyUsed()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<SignUpInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _authRepositoryMock
            .Setup(r => r.GetInvitationByCodeAsync("INVITE01"))
            .ReturnsAsync(ValidInvitation(used: true));

        var result = await _sut.SignUpAsync(ValidInput());

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == InvitationErrors.AlreadyUsed.Code);
        _authProviderMock.Verify(p => p.SignUpAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SignUpAsync_WhenAuthProviderReturnsError_ShouldPropagateError_AndNotInsertUser()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<SignUpInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _authRepositoryMock
            .Setup(r => r.GetInvitationByCodeAsync("INVITE01"))
            .ReturnsAsync(ValidInvitation());
        _authProviderMock
            .Setup(p => p.SignUpAsync("max@example.com", "Secret123!"))
            .ReturnsAsync(AuthErrors.AlreadyRegistered);

        var result = await _sut.SignUpAsync(ValidInput());

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == AuthErrors.AlreadyRegistered.Code);
        _authRepositoryMock.Verify(r => r.InsertUserAsync(It.IsAny<User>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task SignUpAsync_WhenDatabaseInsertThrows_ShouldRollBackAuthUser_AndReturnDatabaseInsertFailed()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<SignUpInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        var invitation = ValidInvitation();
        _authRepositoryMock
            .Setup(r => r.GetInvitationByCodeAsync("INVITE01"))
            .ReturnsAsync(invitation);
        var session = ValidSession();
        _authProviderMock
            .Setup(p => p.SignUpAsync("max@example.com", "Secret123!"))
            .ReturnsAsync(session);
        _authRepositoryMock
            .Setup(r => r.InsertUserAsync(It.IsAny<User>(), invitation.Id))
            .ThrowsAsync(new InvalidOperationException("connection lost"));

        var result = await _sut.SignUpAsync(ValidInput());

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Code.Should().StartWith("Auth.DatabaseInsertFailed");
        result.Errors[0].Description.Should().Contain("connection lost");

        _supabaseAdminClientMock.Verify(c => c.DeleteUserAsync(session.UserId), Times.Once);
    }

    [Fact]
    public async Task SignUpAsync_WhenInvitationIsClaimedConcurrently_ShouldRollBackAuthUser_AndReturnAlreadyUsed()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<SignUpInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        var invitation = ValidInvitation();
        _authRepositoryMock
            .Setup(r => r.GetInvitationByCodeAsync("INVITE01"))
            .ReturnsAsync(invitation);
        var session = ValidSession();
        _authProviderMock
            .Setup(p => p.SignUpAsync("max@example.com", "Secret123!"))
            .ReturnsAsync(session);
        _authRepositoryMock
            .Setup(r => r.InsertUserAsync(It.IsAny<User>(), invitation.Id))
            .ThrowsAsync(new InvitationAlreadyUsedException(invitation.Id));

        var result = await _sut.SignUpAsync(ValidInput());

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == InvitationErrors.AlreadyUsed.Code);
        result.Errors[0].Type.Should().Be(ErrorType.Conflict);

        _supabaseAdminClientMock.Verify(c => c.DeleteUserAsync(session.UserId), Times.Once);
    }
}
