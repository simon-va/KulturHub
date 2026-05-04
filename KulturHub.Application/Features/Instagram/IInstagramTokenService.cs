using ErrorOr;

namespace KulturHub.Application.Features.Instagram;

public interface IInstagramTokenService
{
    Task<ErrorOr<bool>> RefreshTokenAsync(CancellationToken cancellationToken);
}
