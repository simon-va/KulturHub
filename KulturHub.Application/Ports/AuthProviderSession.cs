namespace KulturHub.Application.Ports;

public record AuthProviderSession(string AccessToken, string RefreshToken, Guid UserId);
