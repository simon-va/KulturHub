using FluentAssertions;
using KulturHub.Application.Errors;
using KulturHub.Application.Features.Events.GetConversation;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Enums;
using KulturHub.Domain.Interfaces;
using Moq;

namespace KulturHub.UnitTests.Features.Events.GetConversation;

public class GetConversationServiceTests
{
    // Rules:
    // - Returns conversation with messages when event exists and has a conversation
    // - Returns EventErrors.NotFound when event does not exist
    // - Returns EventErrors.NoConversation when event has no conversation

    private readonly Mock<IEventRepository> _eventRepositoryMock = new();
    private readonly Mock<IMessageRepository> _messageRepositoryMock = new();
    private readonly GetConversationService _service;

    public GetConversationServiceTests()
    {
        _service = new GetConversationService(
            _eventRepositoryMock.Object,
            _messageRepositoryMock.Object);
    }

    [Fact]
    public async Task GetConversationAsync_WhenEventNotFound_ShouldReturnNotFoundError()
    {
        var input = new GetConversationInput(Guid.NewGuid(), Guid.NewGuid());
        _eventRepositoryMock.Setup(x => x.GetByIdAsync(input.EventId, input.OrganisationId)).ReturnsAsync((Event?)null);

        var result = await _service.GetConversationAsync(input);

        result.IsError.Should().BeTrue();
        result.FirstError.Should().BeEquivalentTo(EventErrors.NotFound(input.EventId));
    }

    [Fact]
    public async Task GetConversationAsync_WhenEventHasNoConversation_ShouldReturnNoConversationError()
    {
        var input = new GetConversationInput(Guid.NewGuid(), Guid.NewGuid());
        var @event = Event.Reconstitute(
            input.EventId, input.OrganisationId, "", null, null, "", "", DateTime.UtcNow,
            EventStatus.Draft, null, null, null, 0);
        _eventRepositoryMock.Setup(x => x.GetByIdAsync(input.EventId, input.OrganisationId)).ReturnsAsync(@event);

        var result = await _service.GetConversationAsync(input);

        result.IsError.Should().BeTrue();
        result.FirstError.Should().BeEquivalentTo(EventErrors.NoConversation(input.EventId));
    }

    [Fact]
    public async Task GetConversationAsync_WhenEventExists_ShouldReturnConversationWithMessages()
    {
        var conversationId = Guid.NewGuid();
        var input = new GetConversationInput(Guid.NewGuid(), Guid.NewGuid());
        var @event = Event.Reconstitute(
            input.EventId, input.OrganisationId, "Title",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2),
            "Address", "Description", DateTime.UtcNow,
            EventStatus.Draft, null, null, conversationId, 0);
        var messages = new[]
        {
            Message.Create(conversationId, MessageRole.User, "Hello"),
            Message.Create(conversationId, MessageRole.Assistant, "Hi there")
        };

        _eventRepositoryMock.Setup(x => x.GetByIdAsync(input.EventId, input.OrganisationId)).ReturnsAsync(@event);
        _messageRepositoryMock.Setup(x => x.GetByConversationIdAsync(conversationId)).ReturnsAsync(messages);

        var result = await _service.GetConversationAsync(input);

        result.IsError.Should().BeFalse();
        result.Value.ConversationId.Should().Be(conversationId);
        result.Value.Messages.Should().HaveCount(2);
    }
}