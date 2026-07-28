namespace KulturHub.Application.Ports;

public sealed record AuthProviderSession(
    string AccessToken,
    string RefreshToken,
    Guid UserId);