using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using KulturHub.Application.Errors;
using KulturHub.Application.Features.Events.SendMessage;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Enums;
using KulturHub.Domain.Exceptions;
using KulturHub.Domain.Interfaces;
using Moq;

namespace KulturHub.UnitTests.Features.Events.SendMessage;

public class SendMessageServiceTests
{
    // Rules:
    // - When event not found: returns NotFound error
    // - When event has no conversation: returns NoConversation error
    // - When AI returns valid ready response with category: updates event to ReadyToPublish, saves messages
    // - When AI returns incomplete response: updates event to Draft, saves messages
    // - When AI returns unparseable JSON: returns AiParseError
    // - When AI returns invalid date format: returns AiParseError
    // - When AI returns empty reply: returns AiParseError
    // - When AI returns ready without category: returns IncompleteAiResponse
    // - When AI returns invalid category_id: returns AiParseError
    // - When concurrency conflict occurs: returns ConcurrencyConflict error
    // - Bot message is saved with Assistant role
    // - AI receives event context in system prompt (times shown in Europe/Berlin)
    // - AI receives available categories in system prompt
    // - Status transition requires category_id when AI signals ready
    // - Bare ISO datetimes (no offset) are interpreted as Europe/Berlin local time and converted to UTC
    // - Datetimes with explicit UTC offset (Z or +xx:xx) are parsed directly as UTC

    private readonly Mock<IEventRepository> _eventRepositoryMock = new();
    private readonly Mock<IMessageRepository> _messageRepositoryMock = new();
    private readonly Mock<IEventCategoryRepository> _eventCategoryRepositoryMock = new();
    private readonly Mock<IAiChatService> _aiChatServiceMock = new();
    private readonly SendMessageService _service;

    private static readonly List<EventCategory> DefaultCategories =
    [
        EventCategory.Reconstitute(1, "Konzert", "#FF0000"),
        EventCategory.Reconstitute(2, "Theater", "#00FF00"),
        EventCategory.Reconstitute(3, "Kunst", "#0000FF")
    ];

    public SendMessageServiceTests()
    {
        _eventCategoryRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(DefaultCategories);

        _service = new SendMessageService(
            _eventRepositoryMock.Object,
            _messageRepositoryMock.Object,
            _eventCategoryRepositoryMock.Object,
            _aiChatServiceMock.Object);
    }

    [Fact]
    public async Task SendMessageAsync_WhenEventNotFound_ShouldReturnNotFoundError()
    {
        var input = new SendMessageInput(Guid.NewGuid(), Guid.NewGuid(), "Hello");
        _eventRepositoryMock.Setup(x => x.GetByIdAsync(input.EventId, input.OrganisationId)).ReturnsAsync((Event?)null);

        var result = await _service.SendMessageAsync(input);

        result.IsError.Should().BeTrue();
        result.FirstError.Should().BeEquivalentTo(EventErrors.NotFound(input.EventId));
    }

    [Fact]
    public async Task SendMessageAsync_WhenEventHasNoConversation_ShouldReturnNoConversationError()
    {
        var input = new SendMessageInput(Guid.NewGuid(), Guid.NewGuid(), "Hello");
        var @event = Event.Reconstitute(
            input.EventId, input.OrganisationId, "", null, null, "", "", DateTime.UtcNow,
            EventStatus.Draft, null, null, null, 0);
        _eventRepositoryMock.Setup(x => x.GetByIdAsync(input.EventId, input.OrganisationId)).ReturnsAsync(@event);

        var result = await _service.SendMessageAsync(input);

        result.IsError.Should().BeTrue();
        result.FirstError.Should().BeEquivalentTo(EventErrors.NoConversation(input.EventId));
    }

    [Fact]
    public async Task SendMessageAsync_WhenAiReturnsReadyWithAllFields_ShouldUpdateEventToReadyToPublish()
    {
        var conversationId = Guid.NewGuid();
        var input = new SendMessageInput(Guid.NewGuid(), Guid.NewGuid(), "Hello");
        var @event = Event.Reconstitute(
            input.EventId, input.OrganisationId, "",
            null, null, "", "", DateTime.UtcNow,
            EventStatus.Draft, null, null, conversationId, 0);
        var start = DateTime.UtcNow.AddDays(1);
        var end = DateTime.UtcNow.AddDays(2);
        var aiJson = JsonSerializer.Serialize(new
        {
            title = "Title",
            address = "Address",
            description = "Description",
            start_time = start.ToString("O"),
            end_time = end.ToString("O"),
            category_id = "1",
            status = "ready",
            reply = "All set!"
        });

        _eventRepositoryMock.Setup(x => x.GetByIdAsync(input.EventId, input.OrganisationId)).ReturnsAsync(@event);
        _messageRepositoryMock.Setup(x => x.GetByConversationIdAsync(conversationId)).ReturnsAsync([]);
        _aiChatServiceMock.Setup(x => x.GetStructuredReplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<AiMessage>>(), It.IsAny<CancellationToken>())).ReturnsAsync(aiJson);

        var result = await _service.SendMessageAsync(input);

        result.IsError.Should().BeFalse();
        @event.Status.Should().Be(EventStatus.ReadyToPublish);
        @event.Title.Should().Be("Title");
        @event.Address.Should().Be("Address");
        @event.Description.Should().Be("Description");
    }

    [Fact]
    public async Task SendMessageAsync_WhenAiReturnsIncomplete_ShouldUpdateEventToDraft()
    {
        var conversationId = Guid.NewGuid();
        var input = new SendMessageInput(Guid.NewGuid(), Guid.NewGuid(), "Hello");
        var @event = Event.Reconstitute(
            input.EventId, input.OrganisationId, "Title",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2),
            "Address", "Description", DateTime.UtcNow,
            EventStatus.ReadyToPublish, null, null, conversationId, 0);
        var aiJson = JsonSerializer.Serialize(new
        {
            status = "incomplete",
            reply = "Need more info"
        });

        _eventRepositoryMock.Setup(x => x.GetByIdAsync(input.EventId, input.OrganisationId)).ReturnsAsync(@event);
        _messageRepositoryMock.Setup(x => x.GetByConversationIdAsync(conversationId)).ReturnsAsync([]);
        _aiChatServiceMock.Setup(x => x.GetStructuredReplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<AiMessage>>(), It.IsAny<CancellationToken>())).ReturnsAsync(aiJson);

        var result = await _service.SendMessageAsync(input);

        result.IsError.Should().BeFalse();
        @event.Status.Should().Be(EventStatus.Draft);
    }

    [Fact]
    public async Task SendMessageAsync_WhenAiReturnsUnparseableJson_ShouldReturnAiParseError()
    {
        var conversationId = Guid.NewGuid();
        var input = new SendMessageInput(Guid.NewGuid(), Guid.NewGuid(), "Hello");
        var @event = Event.Reconstitute(
            input.EventId, input.OrganisationId, "", null, null, "", "", DateTime.UtcNow,
            EventStatus.Draft, null, null, conversationId, 0);

        _eventRepositoryMock.Setup(x => x.GetByIdAsync(input.EventId, input.OrganisationId)).ReturnsAsync(@event);
        _messageRepositoryMock.Setup(x => x.GetByConversationIdAsync(conversationId)).ReturnsAsync([]);
        _aiChatServiceMock.Setup(x => x.GetStructuredReplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<AiMessage>>(), It.IsAny<CancellationToken>())).ReturnsAsync("not json");

        var result = await _service.SendMessageAsync(input);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Event.AiParseError");
    }

    [Fact]
    public async Task SendMessageAsync_WhenAiReturnsInvalidDate_ShouldReturnAiParseError()
    {
        var conversationId = Guid.NewGuid();
        var input = new SendMessageInput(Guid.NewGuid(), Guid.NewGuid(), "Hello");
        var @event = Event.Reconstitute(
            input.EventId, input.OrganisationId, "", null, null, "", "", DateTime.UtcNow,
            EventStatus.Draft, null, null, conversationId, 0);
        var aiJson = JsonSerializer.Serialize(new
        {
            start_time = "invalid-date",
            status = "incomplete",
            reply = "Need date"
        });

        _eventRepositoryMock.Setup(x => x.GetByIdAsync(input.EventId, input.OrganisationId)).ReturnsAsync(@event);
        _messageRepositoryMock.Setup(x => x.GetByConversationIdAsync(conversationId)).ReturnsAsync([]);
        _aiChatServiceMock.Setup(x => x.GetStructuredReplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<AiMessage>>(), It.IsAny<CancellationToken>())).ReturnsAsync(aiJson);

        var result = await _service.SendMessageAsync(input);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Event.AiParseError");
    }

    [Fact]
    public async Task SendMessageAsync_WhenAiReturnsEmptyReply_ShouldReturnAiParseError()
    {
        var conversationId = Guid.NewGuid();
        var input = new SendMessageInput(Guid.NewGuid(), Guid.NewGuid(), "Hello");
        var @event = Event.Reconstitute(
            input.EventId, input.OrganisationId, "", null, null, "", "", DateTime.UtcNow,
            EventStatus.Draft, null, null, conversationId, 0);
        var aiJson = JsonSerializer.Serialize(new
        {
            status = "incomplete",
            reply = ""
        });

        _eventRepositoryMock.Setup(x => x.GetByIdAsync(input.EventId, input.OrganisationId)).ReturnsAsync(@event);
        _messageRepositoryMock.Setup(x => x.GetByConversationIdAsync(conversationId)).ReturnsAsync([]);
        _aiChatServiceMock.Setup(x => x.GetStructuredReplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<AiMessage>>(), It.IsAny<CancellationToken>())).ReturnsAsync(aiJson);

        var result = await _service.SendMessageAsync(input);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Event.AiParseError");
    }

    [Fact]
    public async Task SendMessageAsync_WhenAiReturnsIsoDateWithoutFractionalSeconds_ShouldParseAsBerlinSummerTime()
    {
        var conversationId = Guid.NewGuid();
        var input = new SendMessageInput(Guid.NewGuid(), Guid.NewGuid(), "Hello");
        var @event = Event.Reconstitute(
            input.EventId, input.OrganisationId, "Title",
            null, null, "Address", "", DateTime.UtcNow,
            EventStatus.Draft, null, null, conversationId, 0);
        var aiJson = JsonSerializer.Serialize(new
        {
            description = "Description",
            start_time = "2026-07-20T13:00:00",
            end_time = "2026-07-20T15:00:00",
            category_id = "1",
            status = "ready",
            reply = "All set!"
        });

        _eventRepositoryMock.Setup(x => x.GetByIdAsync(input.EventId, input.OrganisationId)).ReturnsAsync(@event);
        _messageRepositoryMock.Setup(x => x.GetByConversationIdAsync(conversationId)).ReturnsAsync([]);
        _aiChatServiceMock.Setup(x => x.GetStructuredReplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<AiMessage>>(), It.IsAny<CancellationToken>())).ReturnsAsync(aiJson);

        var result = await _service.SendMessageAsync(input);

        result.IsError.Should().BeFalse();
        @event.Status.Should().Be(EventStatus.ReadyToPublish);
        @event.StartTime.Should().Be(new DateTime(2026, 7, 20, 11, 0, 0, DateTimeKind.Utc));
        @event.EndTime.Should().Be(new DateTime(2026, 7, 20, 13, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task SendMessageAsync_WhenAiReturnsIsoDateInWinter_ShouldParseAsBerlinWinterTime()
    {
        var conversationId = Guid.NewGuid();
        var input = new SendMessageInput(Guid.NewGuid(), Guid.NewGuid(), "Hello");
        var @event = Event.Reconstitute(
            input.EventId, input.OrganisationId, "Title",
            null, null, "Address", "", DateTime.UtcNow,
            EventStatus.Draft, null, null, conversationId, 0);
        var aiJson = JsonSerializer.Serialize(new
        {
            description = "Description",
            start_time = "2027-01-15T13:00:00",
            end_time = "2027-01-15T15:00:00",
            category_id = "1",
            status = "ready",
            reply = "All set!"
        });

        _eventRepositoryMock.Setup(x => x.GetByIdAsync(input.EventId, input.OrganisationId)).ReturnsAsync(@event);
        _messageRepositoryMock.Setup(x => x.GetByConversationIdAsync(conversationId)).ReturnsAsync([]);
        _aiChatServiceMock.Setup(x => x.GetStructuredReplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<AiMessage>>(), It.IsAny<CancellationToken>())).ReturnsAsync(aiJson);

        var result = await _service.SendMessageAsync(input);

        result.IsError.Should().BeFalse();
        @event.Status.Should().Be(EventStatus.ReadyToPublish);
        @event.StartTime.Should().Be(new DateTime(2027, 1, 15, 12, 0, 0, DateTimeKind.Utc));
        @event.EndTime.Should().Be(new DateTime(2027, 1, 15, 14, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task SendMessageAsync_WhenAiReturnsIsoDateWithExplicitUtcOffset_ShouldParseAsUtc()
    {
        var conversationId = Guid.NewGuid();
        var input = new SendMessageInput(Guid.NewGuid(), Guid.NewGuid(), "Hello");
        var @event = Event.Reconstitute(
            input.EventId, input.OrganisationId, "Title",
            null, null, "Address", "", DateTime.UtcNow,
            EventStatus.Draft, null, null, conversationId, 0);
        var aiJson = JsonSerializer.Serialize(new
        {
            description = "Description",
            start_time = "2026-07-20T11:00:00Z",
            end_time = "2026-07-20T13:00:00Z",
            category_id = "1",
            status = "ready",
            reply = "All set!"
        });

        _eventRepositoryMock.Setup(x => x.GetByIdAsync(input.EventId, input.OrganisationId)).ReturnsAsync(@event);
        _messageRepositoryMock.Setup(x => x.GetByConversationIdAsync(conversationId)).ReturnsAsync([]);
        _aiChatServiceMock.Setup(x => x.GetStructuredReplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<AiMessage>>(), It.IsAny<CancellationToken>())).ReturnsAsync(aiJson);

        var result = await _service.SendMessageAsync(input);

        result.IsError.Should().BeFalse();
        @event.Status.Should().Be(EventStatus.ReadyToPublish);
        @event.StartTime.Should().Be(new DateTime(2026, 7, 20, 11, 0, 0, DateTimeKind.Utc));
        @event.EndTime.Should().Be(new DateTime(2026, 7, 20, 13, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task SendMessageAsync_WhenAiReturnsReadyWithPartialFields_ShouldUpdateEventToReadyToPublish()
    {
        var conversationId = Guid.NewGuid();
        var input = new SendMessageInput(Guid.NewGuid(), Guid.NewGuid(), "Hello");
        var start = DateTime.UtcNow.AddDays(1);
        var end = DateTime.UtcNow.AddDays(2);
        var @event = Event.Reconstitute(
            input.EventId, input.OrganisationId, "Title",
            start, end,
            "Address", "", DateTime.UtcNow,
            EventStatus.Draft, null, null, conversationId, 0);
        var aiJson = JsonSerializer.Serialize(new
        {
            description = "Description",
            category_id = "1",
            status = "ready",
            reply = "All set!"
        });

        _eventRepositoryMock.Setup(x => x.GetByIdAsync(input.EventId, input.OrganisationId)).ReturnsAsync(@event);
        _messageRepositoryMock.Setup(x => x.GetByConversationIdAsync(conversationId)).ReturnsAsync([]);
        _aiChatServiceMock.Setup(x => x.GetStructuredReplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<AiMessage>>(), It.IsAny<CancellationToken>())).ReturnsAsync(aiJson);

        var result = await _service.SendMessageAsync(input);

        result.IsError.Should().BeFalse();
        @event.Status.Should().Be(EventStatus.ReadyToPublish);
        @event.Title.Should().Be("Title");
        @event.Address.Should().Be("Address");
        @event.Description.Should().Be("Description");
    }

    [Fact]
    public async Task SendMessageAsync_WhenAiReturnsReadyWithoutCategory_ShouldReturnIncompleteAiResponse()
    {
        var conversationId = Guid.NewGuid();
        var input = new SendMessageInput(Guid.NewGuid(), Guid.NewGuid(), "Hello");
        var @event = Event.Reconstitute(
            input.EventId, input.OrganisationId, "Title",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2),
            "Address", "Description", DateTime.UtcNow,
            EventStatus.Draft, null, null, conversationId, 0);
        var aiJson = JsonSerializer.Serialize(new
        {
            status = "ready",
            reply = "Ready!"
        });

        _eventRepositoryMock.Setup(x => x.GetByIdAsync(input.EventId, input.OrganisationId)).ReturnsAsync(@event);
        _messageRepositoryMock.Setup(x => x.GetByConversationIdAsync(conversationId)).ReturnsAsync([]);
        _aiChatServiceMock.Setup(x => x.GetStructuredReplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<AiMessage>>(), It.IsAny<CancellationToken>())).ReturnsAsync(aiJson);

        var result = await _service.SendMessageAsync(input);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Event.IncompleteAiResponse");
    }

    [Fact]
    public async Task SendMessageAsync_ShouldPassEventContextToAi()
    {
        var conversationId = Guid.NewGuid();
        var input = new SendMessageInput(Guid.NewGuid(), Guid.NewGuid(), "Hello");
        var @event = Event.Reconstitute(
            input.EventId, input.OrganisationId, "Existing Title",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2),
            "Existing Address", "Existing Description", DateTime.UtcNow,
            EventStatus.Draft, null, null, conversationId, 0);
        var aiJson = JsonSerializer.Serialize(new
        {
            status = "incomplete",
            reply = "Got it!"
        });

        _eventRepositoryMock.Setup(x => x.GetByIdAsync(input.EventId, input.OrganisationId)).ReturnsAsync(@event);
        _messageRepositoryMock.Setup(x => x.GetByConversationIdAsync(conversationId)).ReturnsAsync([]);

        string? capturedPrompt = null;
        _aiChatServiceMock.Setup(x => x.GetStructuredReplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<AiMessage>>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, IEnumerable<AiMessage>, CancellationToken>((prompt, schema, history, ct) => capturedPrompt = prompt)
            .ReturnsAsync(aiJson);

        await _service.SendMessageAsync(input);

        capturedPrompt.Should().NotBeNull();
        capturedPrompt.Should().Contain("Existing Title");
        capturedPrompt.Should().Contain("Existing Address");
        capturedPrompt.Should().Contain("Existing Description");
        capturedPrompt.Should().Contain("Konzert");
        capturedPrompt.Should().Contain("Theater");
        capturedPrompt.Should().Contain("Kunst");
    }

    [Fact]
    public async Task SendMessageAsync_WhenConcurrencyConflict_ShouldReturnConcurrencyConflictError()
    {
        var conversationId = Guid.NewGuid();
        var input = new SendMessageInput(Guid.NewGuid(), Guid.NewGuid(), "Hello");
        var @event = Event.Reconstitute(
            input.EventId, input.OrganisationId, "", null, null, "", "", DateTime.UtcNow,
            EventStatus.Draft, null, null, conversationId, 0);
        var aiJson = JsonSerializer.Serialize(new
        {
            status = "incomplete",
            reply = "Need more info"
        });

        _eventRepositoryMock.Setup(x => x.GetByIdAsync(input.EventId, input.OrganisationId)).ReturnsAsync(@event);
        _messageRepositoryMock.Setup(x => x.GetByConversationIdAsync(conversationId)).ReturnsAsync([]);
        _aiChatServiceMock.Setup(x => x.GetStructuredReplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<AiMessage>>(), It.IsAny<CancellationToken>())).ReturnsAsync(aiJson);
        _eventRepositoryMock.Setup(x => x.UpdateDraftAsync(@event)).ThrowsAsync(new ConcurrencyException("Conflict"));

        var result = await _service.SendMessageAsync(input);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Event.ConcurrencyConflict");
    }

    [Fact]
    public async Task SendMessageAsync_ShouldSaveBotMessageAsAssistant()
    {
        var conversationId = Guid.NewGuid();
        var input = new SendMessageInput(Guid.NewGuid(), Guid.NewGuid(), "Hello");
        var @event = Event.Reconstitute(
            input.EventId, input.OrganisationId, "", null, null, "", "", DateTime.UtcNow,
            EventStatus.Draft, null, null, conversationId, 0);
        var aiJson = JsonSerializer.Serialize(new
        {
            status = "incomplete",
            reply = "Need more info"
        });

        _eventRepositoryMock.Setup(x => x.GetByIdAsync(input.EventId, input.OrganisationId)).ReturnsAsync(@event);
        _messageRepositoryMock.Setup(x => x.GetByConversationIdAsync(conversationId)).ReturnsAsync([]);
        _aiChatServiceMock.Setup(x => x.GetStructuredReplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<AiMessage>>(), It.IsAny<CancellationToken>())).ReturnsAsync(aiJson);

        await _service.SendMessageAsync(input);

        _messageRepositoryMock.Verify(x => x.CreateAsync(It.Is<Message>(m => m.Role == MessageRole.Assistant)), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_WhenAiReturnsValidCategoryId_ShouldUpdateEventCategory()
    {
        var conversationId = Guid.NewGuid();
        var input = new SendMessageInput(Guid.NewGuid(), Guid.NewGuid(), "Hello");
        var @event = Event.Reconstitute(
            input.EventId, input.OrganisationId, "Title",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2),
            "Address", "Description", DateTime.UtcNow,
            EventStatus.Draft, null, null, conversationId, 0);
        var aiJson = JsonSerializer.Serialize(new
        {
            category_id = "2",
            status = "ready",
            reply = "Ready!"
        });

        _eventRepositoryMock.Setup(x => x.GetByIdAsync(input.EventId, input.OrganisationId)).ReturnsAsync(@event);
        _messageRepositoryMock.Setup(x => x.GetByConversationIdAsync(conversationId)).ReturnsAsync([]);
        _aiChatServiceMock.Setup(x => x.GetStructuredReplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<AiMessage>>(), It.IsAny<CancellationToken>())).ReturnsAsync(aiJson);

        var result = await _service.SendMessageAsync(input);

        result.IsError.Should().BeFalse();
        @event.Status.Should().Be(EventStatus.ReadyToPublish);
        @event.EventCategoryId.Should().Be(2);
    }

    [Fact]
    public async Task SendMessageAsync_WhenAiReturnsInvalidCategoryId_ShouldReturnAiParseError()
    {
        var conversationId = Guid.NewGuid();
        var input = new SendMessageInput(Guid.NewGuid(), Guid.NewGuid(), "Hello");
        var @event = Event.Reconstitute(
            input.EventId, input.OrganisationId, "Title",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2),
            "Address", "Description", DateTime.UtcNow,
            EventStatus.Draft, null, null, conversationId, 0);
        var aiJson = JsonSerializer.Serialize(new
        {
            category_id = "999",
            status = "ready",
            reply = "Ready!"
        });

        _eventRepositoryMock.Setup(x => x.GetByIdAsync(input.EventId, input.OrganisationId)).ReturnsAsync(@event);
        _messageRepositoryMock.Setup(x => x.GetByConversationIdAsync(conversationId)).ReturnsAsync([]);
        _aiChatServiceMock.Setup(x => x.GetStructuredReplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<AiMessage>>(), It.IsAny<CancellationToken>())).ReturnsAsync(aiJson);

        var result = await _service.SendMessageAsync(input);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Event.AiParseError");
    }

    [Fact]
    public async Task SendMessageAsync_WhenAiReturnsIncompleteWithCategory_ShouldUpdateCategoryButKeepDraft()
    {
        var conversationId = Guid.NewGuid();
        var input = new SendMessageInput(Guid.NewGuid(), Guid.NewGuid(), "Hello");
        var @event = Event.Reconstitute(
            input.EventId, input.OrganisationId, "Title",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2),
            "Address", "", DateTime.UtcNow,
            EventStatus.Draft, null, null, conversationId, 0);
        var aiJson = JsonSerializer.Serialize(new
        {
            category_id = "1",
            status = "incomplete",
            reply = "Got the category!"
        });

        _eventRepositoryMock.Setup(x => x.GetByIdAsync(input.EventId, input.OrganisationId)).ReturnsAsync(@event);
        _messageRepositoryMock.Setup(x => x.GetByConversationIdAsync(conversationId)).ReturnsAsync([]);
        _aiChatServiceMock.Setup(x => x.GetStructuredReplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<AiMessage>>(), It.IsAny<CancellationToken>())).ReturnsAsync(aiJson);

        var result = await _service.SendMessageAsync(input);

        result.IsError.Should().BeFalse();
        @event.Status.Should().Be(EventStatus.Draft);
        @event.EventCategoryId.Should().Be(1);
    }
}