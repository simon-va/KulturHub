using ErrorOr;

namespace KulturHub.Application.Features.WeeklyPost;

public interface IWeeklyPostService
{
    Task<ErrorOr<Guid>> GenerateWeeklyPostAsync(CancellationToken cancellationToken);
}
