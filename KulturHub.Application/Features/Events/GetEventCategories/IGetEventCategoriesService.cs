using ErrorOr;

namespace KulturHub.Application.Features.Events.GetEventCategories;

public interface IGetEventCategoriesService
{
    Task<ErrorOr<IEnumerable<EventCategoryResponse>>> GetEventCategoriesAsync();
}
