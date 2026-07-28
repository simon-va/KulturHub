using ErrorOr;
using FluentAssertions;
using KulturHub.Application.Errors;
using KulturHub.Application.Features.Platform.Auth.SignIn;
using KulturHub.Application.Ports;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace KulturHub.UnitTests.Features.Application.Platform.Auth.SignIn;

public class SignInHandlerTests
{
    private static SignInRequest ValidRequest() => new(
        Email: "max@example.com",
        Password: "Sicher123!");

    private static (
        SignInHandler Sut,
        Mock<IAuthProvider> Auth) CreateSut(
        AuthProviderSession? sessionToReturn = null,
        Error? authError = null)
    {
        var auth = new Mock<IAuthProvider>();
        if (authError is not null)
        {
            var errorResult = ErrorOr<AuthProviderSession>.From(new List<Error> { authError.Value });
            auth.Setup(a => a.SignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(errorResult);
        }
        else
        {
            sessionToReturn ??= new AuthProviderSession("access", "refresh", Guid.NewGuid());
            ErrorOr<AuthProviderSession> success = sessionToReturn;
            auth.Setup(a => a.SignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(success);
        }

        var handler = new SignInHandler(auth.Object, NullLogger<SignInHandler>.Instance);

        return (handler, auth);
    }

    [Fact]
    public async Task Handle_WhenRequestIsValid_ShouldReturnSessionTokens()
    {
        var session = new AuthProviderSession("access-1", "refresh-1", Guid.NewGuid());
        var (sut, auth) = CreateSut(session);

        var result = await sut.HandleAsync(ValidRequest(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.AccessToken.Should().Be("access-1");
        result.Value.RefreshToken.Should().Be("refresh-1");
        result.Value.UserId.Should().Be(session.UserId);

        auth.Verify(a => a.SignInAsync("max@example.com", "Sicher123!", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSupabaseReturnsInvalidCredentials_ShouldReturnUnauthorized()
    {
        var (sut, auth) = CreateSut(authError: AuthErrors.InvalidCredentials);

        var result = await sut.HandleAsync(ValidRequest(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Unauthorized);
        result.FirstError.Code.Should().Be("Auth.InvalidCredentials");

        auth.Verify(a => a.SignInAsync("max@example.com", "Sicher123!", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSupabaseReturnsSignInFailed_ShouldReturnFailure()
    {
        var (sut, auth) = CreateSut(authError: AuthErrors.SignInFailed);

        var result = await sut.HandleAsync(ValidRequest(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Failure);
        result.FirstError.Code.Should().Be("Auth.SignInFailed");

        auth.Verify(a => a.SignInAsync("max@example.com", "Sicher123!", It.IsAny<CancellationToken>()), Times.Once);
    }
}
