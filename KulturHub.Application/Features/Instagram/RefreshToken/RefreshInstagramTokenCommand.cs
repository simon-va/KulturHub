using ErrorOr;
using MediatR;

namespace KulturHub.Application.Features.Instagram.RefreshToken;

public record RefreshInstagramTokenCommand : IRequest<ErrorOr<bool>>;
