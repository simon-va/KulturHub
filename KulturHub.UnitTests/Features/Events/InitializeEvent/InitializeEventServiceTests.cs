using FluentAssertions;
using KulturHub.Application.Features.Events.InitializeEvent;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Interfaces;
using Moq;

namespace KulturHub.UnitTests.Features.Events.InitializeEvent;

public class InitializeEventServiceTests
{
    // Rules:
    // - Creates a conversation, a system message, and a draft event
    // - Returns the event id
    // - All three entities are persisted via their repositories

    private readonly Mock<IConversationRepository> _conversationRepositoryMock = new();
    private readonly Mock<IMessageRepository> _messageRepositoryMock = new();
    private readonly Mock<IEventRepository> _eventRepositoryMock = new();
    private readonly InitializeEventService _service;

    public InitializeEventServiceTests()
    {
        _service = new InitializeEventService(
            _conversationRepositoryMock.Object,
            _messageRepositoryMock.Object,
            _eventRepositoryMock.Object);
    }

    [Fact]
    public async Task InitializeEventAsync_ShouldReturnEventId()
    {
        var input = new InitializeEventInput(Guid.NewGuid());

        var result = await _service.InitializeEventAsync(input);

        result.IsError.Should().BeFalse();
        result.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task InitializeEventAsync_ShouldCreateConversation()
    {
        var input = new InitializeEventInput(Guid.NewGuid());

        await _service.InitializeEventAsync(input);

        _conversationRepositoryMock.Verify(x => x.CreateAsync(It.Is<Conversation>(c => c.OrganisationId == input.OrganisationId)), Times.Once);
    }

    [Fact]
    public async Task InitializeEventAsync_ShouldCreateSystemMessage()
    {
        var input = new InitializeEventInput(Guid.NewGuid());

        await _service.InitializeEventAsync(input);

        _messageRepositoryMock.Verify(x => x.CreateAsync(It.Is<Message>(m => m.Role == KulturHub.Domain.Enums.MessageRole.Assistant)), Times.Once);
    }

    [Fact]
    public async Task InitializeEventAsync_ShouldCreateDraftEvent()
    {
        var input = new InitializeEventInput(Guid.NewGuid());

        await _service.InitializeEventAsync(input);

        _eventRepositoryMock.Verify(x => x.CreateAsync(It.Is<Event>(e => e.Status == KulturHub.Domain.Enums.EventStatus.Draft && e.OrganisationId == input.OrganisationId)), Times.Once);
    }
}