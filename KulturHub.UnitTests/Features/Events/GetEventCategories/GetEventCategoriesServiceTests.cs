using FluentAssertions;
using KulturHub.Application.Features.Events.GetEventCategories;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Interfaces;
using Moq;

namespace KulturHub.UnitTests.Features.Events.GetEventCategories;

public class GetEventCategoriesServiceTests
{
    // Rules:
    // - Returns all event categories from the repository
    // - Returns an empty list when no categories exist

    private readonly Mock<IEventCategoryRepository> _eventCategoryRepositoryMock = new();
    private readonly GetEventCategoriesService _service;

    public GetEventCategoriesServiceTests()
    {
        _service = new GetEventCategoriesService(_eventCategoryRepositoryMock.Object);
    }

    [Fact]
    public async Task GetEventCategoriesAsync_WhenCategoriesExist_ShouldReturnAllCategories()
    {
        var categories = new[]
        {
            EventCategory.Reconstitute(1, "Music", "#FF0000"),
            EventCategory.Reconstitute(2, "Theater", "#00FF00")
        };
        _eventCategoryRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(categories);

        var result = await _service.GetEventCategoriesAsync();

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(2);
        result.Value.First().Name.Should().Be("Music");
        result.Value.First().Color.Should().Be("#FF0000");
    }

    [Fact]
    public async Task GetEventCategoriesAsync_WhenNoCategoriesExist_ShouldReturnEmptyList()
    {
        _eventCategoryRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(Array.Empty<EventCategory>());

        var result = await _service.GetEventCategoriesAsync();

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }
}
