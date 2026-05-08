using FluentAssertions;
using KulturHub.Application.Errors;
using KulturHub.Application.Features.Events.UpdateEventStatus;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Enums;
using KulturHub.Domain.Interfaces;
using Moq;

namespace KulturHub.UnitTests.Features.Events.UpdateEventStatus;

public class UpdateEventStatusServiceTests
{
    // Rules:
    // - ReadyToPublish → Published: allowed
    // - Published → Draft: allowed
    // - Draft → ReadyToPublish: not allowed (only AI can do this)
    // - Any → Failed: not allowed
    // - Event not found: returns NotFound error

    private readonly Mock<IEventRepository> _eventRepositoryMock = new();
    private readonly UpdateEventStatusService _service;

    public UpdateEventStatusServiceTests()
    {
        _service = new UpdateEventStatusService(_eventRepositoryMock.Object);
    }

    [Fact]
    public async Task UpdateEventStatusAsync_WhenReadyToPublishToPublished_ShouldSucceed()
    {
        var input = new UpdateEventStatusInput(Guid.NewGuid(), Guid.NewGuid(), EventStatus.Published);
        var @event = Event.Reconstitute(
            input.EventId, input.OrganisationId, "Title",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2),
            "Address", "Description", DateTime.UtcNow,
            EventStatus.ReadyToPublish, null, null, null, 0);
        _eventRepositoryMock.Setup(x => x.GetByIdAsync(input.EventId, input.OrganisationId)).ReturnsAsync(@event);

        var result = await _service.UpdateEventStatusAsync(input);

        result.IsError.Should().BeFalse();
        @event.Status.Should().Be(EventStatus.Published);
        _eventRepositoryMock.Verify(x => x.UpdateStatusAsync(@event), Times.Once);
    }

    [Fact]
    public async Task UpdateEventStatusAsync_WhenPublishedToDraft_ShouldSucceed()
    {
        var input = new UpdateEventStatusInput(Guid.NewGuid(), Guid.NewGuid(), EventStatus.Draft);
        var @event = Event.Reconstitute(
            input.EventId, input.OrganisationId, "Title",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2),
            "Address", "Description", DateTime.UtcNow,
            EventStatus.Published, null, null, null, 0);
        _eventRepositoryMock.Setup(x => x.GetByIdAsync(input.EventId, input.OrganisationId)).ReturnsAsync(@event);

        var result = await _service.UpdateEventStatusAsync(input);

        result.IsError.Should().BeFalse();
        @event.Status.Should().Be(EventStatus.Draft);
        _eventRepositoryMock.Verify(x => x.UpdateStatusAsync(@event), Times.Once);
    }

    [Fact]
    public async Task UpdateEventStatusAsync_WhenDraftToReadyToPublish_ShouldReturnInvalidTransitionError()
    {
        var input = new UpdateEventStatusInput(Guid.NewGuid(), Guid.NewGuid(), EventStatus.ReadyToPublish);
        var @event = Event.CreateDraft(input.OrganisationId, Guid.NewGuid());
        _eventRepositoryMock.Setup(x => x.GetByIdAsync(input.EventId, input.OrganisationId)).ReturnsAsync(@event);

        var result = await _service.UpdateEventStatusAsync(input);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Event.InvalidTransition");
    }

    [Fact]
    public async Task UpdateEventStatusAsync_WhenAnyToFailed_ShouldReturnInvalidTransitionError()
    {
        var input = new UpdateEventStatusInput(Guid.NewGuid(), Guid.NewGuid(), EventStatus.Failed);
        var @event = Event.CreateDraft(input.OrganisationId, Guid.NewGuid());
        _eventRepositoryMock.Setup(x => x.GetByIdAsync(input.EventId, input.OrganisationId)).ReturnsAsync(@event);

        var result = await _service.UpdateEventStatusAsync(input);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Event.InvalidTransition");
    }

    [Fact]
    public async Task UpdateEventStatusAsync_WhenEventNotFound_ShouldReturnNotFoundError()
    {
        var input = new UpdateEventStatusInput(Guid.NewGuid(), Guid.NewGuid(), EventStatus.Published);
        _eventRepositoryMock.Setup(x => x.GetByIdAsync(input.EventId, input.OrganisationId)).ReturnsAsync((Event?)null);

        var result = await _service.UpdateEventStatusAsync(input);

        result.IsError.Should().BeTrue();
        result.FirstError.Should().BeEquivalentTo(EventErrors.NotFound(input.EventId));
    }

    [Fact]
    public async Task UpdateEventStatusAsync_WhenDraftToPublished_ShouldReturnInvalidTransitionError()
    {
        var input = new UpdateEventStatusInput(Guid.NewGuid(), Guid.NewGuid(), EventStatus.Published);
        var @event = Event.CreateDraft(input.OrganisationId, Guid.NewGuid());
        _eventRepositoryMock.Setup(x => x.GetByIdAsync(input.EventId, input.OrganisationId)).ReturnsAsync(@event);

        var result = await _service.UpdateEventStatusAsync(input);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Event.InvalidTransition");
    }
}