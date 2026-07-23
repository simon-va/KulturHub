using ErrorOr;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using KulturHub.Application.Errors;
using KulturHub.Application.Features.Auth.SignIn;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using Moq;

namespace KulturHub.UnitTests.Features.Auth.SignIn;

public class SignInHandlerTests
{
    // Rules:
    // - Validation runs first; invalid input short-circuits before any other dependency is touched.
    // - Auth provider errors (e.g. InvalidCredentials) are propagated unchanged; the repository is not queried.
    // - If the auth provider succeeds but no local user row exists, AuthErrors.NotFound is returned.
    // - On success the response contains the session tokens plus the first/last name from the database.

    private readonly Mock<IAuthProvider> _authProviderMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IValidator<SignInInput>> _validatorMock = new();

    private readonly SignInHandler _sut;

    public SignInHandlerTests()
    {
        _sut = new SignInHandler(
            _authProviderMock.Object,
            _userRepositoryMock.Object,
            _validatorMock.Object);
    }

    private static SignInInput ValidInput() =>
        new("max@example.com", "Secret123!");

    private static AuthProviderSession ValidSession() =>
        new("access", "refresh", Guid.NewGuid());

    private static User ExistingUser(Guid userId) =>
        User.Reconstitute(userId, "max@example.com", "Max", "Mustermann");

    [Fact]
    public async Task ExecuteAsync_WhenAllInputsAreValid_ShouldReturnSignInResponse()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<SignInInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        var session = ValidSession();
        _authProviderMock
            .Setup(p => p.SignInAsync("max@example.com", "Secret123!", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(session.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExistingUser(session.UserId));

        var result = await _sut.ExecuteAsync(ValidInput());

        result.IsError.Should().BeFalse();
        var response = result.Value;
        response.AccessToken.Should().Be(session.AccessToken);
        response.RefreshToken.Should().Be(session.RefreshToken);
        response.UserId.Should().Be(session.UserId);
        response.FirstName.Should().Be("Max");
        response.LastName.Should().Be("Mustermann");

        _authProviderMock.Verify(
            p => p.SignInAsync("max@example.com", "Secret123!", It.IsAny<CancellationToken>()),
            Times.Once);
        _userRepositoryMock.Verify(
            r => r.GetByIdAsync(session.UserId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInputIsInvalid_ShouldReturnValidationErrors_AndNotCallDependencies()
    {
        var failures = new[]
        {
            new ValidationFailure("Email", "Email must be a valid email address."),
            new ValidationFailure("Password", "Password is required."),
        };
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<SignInInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(failures));

        var result = await _sut.ExecuteAsync(ValidInput());

        result.IsError.Should().BeTrue();
        result.Errors.Should().AllSatisfy(e => e.Type.Should().Be(ErrorType.Validation));
        result.Errors.Should().Contain(e => e.Code == "Email");
        result.Errors.Should().Contain(e => e.Code == "Password");

        _authProviderMock.Verify(
            p => p.SignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _userRepositoryMock.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAuthProviderReturnsInvalidCredentials_ShouldPropagateError_AndNotQueryRepository()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<SignInInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _authProviderMock
            .Setup(p => p.SignInAsync("max@example.com", "Secret123!", It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthErrors.InvalidCredentials);

        var result = await _sut.ExecuteAsync(ValidInput());

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == AuthErrors.InvalidCredentials.Code);
        result.Errors[0].Type.Should().Be(ErrorType.Unauthorized);

        _userRepositoryMock.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAuthProviderReturnsOtherError_ShouldPropagateError_AndNotQueryRepository()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<SignInInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _authProviderMock
            .Setup(p => p.SignInAsync("max@example.com", "Secret123!", It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthErrors.SignInFailed);

        var result = await _sut.ExecuteAsync(ValidInput());

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == AuthErrors.SignInFailed.Code);

        _userRepositoryMock.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserDoesNotExistInRepository_ShouldReturnAuthNotFound()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<SignInInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        var session = ValidSession();
        _authProviderMock
            .Setup(p => p.SignInAsync("max@example.com", "Secret123!", It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(session.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _sut.ExecuteAsync(ValidInput());

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == AuthErrors.NotFound.Code);
        result.Errors[0].Type.Should().Be(ErrorType.NotFound);
    }
}
