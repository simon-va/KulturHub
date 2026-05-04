using ErrorOr;
using MediatR;

namespace KulturHub.Application.Features.WeeklyPost.GenerateWeeklyPost;

public record GenerateWeeklyPostCommand : IRequest<ErrorOr<Guid>>;
