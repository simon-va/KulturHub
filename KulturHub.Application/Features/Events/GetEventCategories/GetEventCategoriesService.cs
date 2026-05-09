using ErrorOr;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Application.Features.Events.GetEventCategories;

public class GetEventCategoriesService(IEventCategoryRepository eventCategoryRepository) : IGetEventCategoriesService
{
    public async Task<ErrorOr<IEnumerable<EventCategoryResponse>>> GetEventCategoriesAsync()
    {
        var categories = await eventCategoryRepository.GetAllAsync();

        return categories
            .Select(c => new EventCategoryResponse(c.Id, c.Name, c.Color))
            .ToList();
    }
}
