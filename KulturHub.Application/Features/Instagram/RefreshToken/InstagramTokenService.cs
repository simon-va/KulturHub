using ErrorOr;
using KulturHub.Application.Errors;
using KulturHub.Application.Ports;
using KulturHub.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace KulturHub.Application.Features.Instagram.RefreshToken;

public class InstagramTokenService(
    IInstagramTokenRepository tokenRepository,
    IInstagramTokenRefresher tokenRefresher,
    ILogger<InstagramTokenService> logger) : IInstagramTokenService
{
    public async Task<ErrorOr<bool>> RefreshTokenAsync(CancellationToken cancellationToken)
    {
        var token = await tokenRepository.GetCurrentTokenAsync();

        if (token is null)
        {
            logger.LogWarning("No Instagram token found in database, skipping refresh.");
            return false;
        }

        if (token.ExpiresAt > DateTime.UtcNow.AddDays(7))
        {
            logger.LogInformation(
                "Instagram token is valid until {ExpiresAt:d}, no refresh needed.",
                token.ExpiresAt);
            return true;
        }

        try
        {
            var (newAccessToken, newExpiresAt) = await tokenRefresher.RefreshAsync(token.AccessToken, cancellationToken);

            token.Refresh(newAccessToken, newExpiresAt);

            await tokenRepository.UpdateTokenAsync(token);

            logger.LogInformation(
                "Instagram token refreshed successfully, new expiry: {ExpiresAt:d}.",
                token.ExpiresAt);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to refresh Instagram access token.");
            return InstagramErrors.RefreshFailed(ex.Message);
        }
    }
}
