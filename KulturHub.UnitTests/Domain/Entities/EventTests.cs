using FluentAssertions;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Enums;
using KulturHub.Domain.Exceptions;

namespace KulturHub.UnitTests.Domain.Entities;

public class EventTests
{
    // Rules:
    // - CreateDraft initializes status to Draft and version to 0
    // - UpdateDetails sets all provided fields and status
    // - UpdateDetails with null parameters keeps existing values
    // - UpdateDetails throws when startTime is in the past
    // - UpdateDetails throws when endTime is before startTime
    // - UpdateDetails throws when event is Published
    // - Publish succeeds only when status is ReadyToPublish
    // - RevertToDraft succeeds only when status is Published
    // - IncrementVersion increases version by 1

    [Fact]
    public void CreateDraft_ShouldSetStatusToDraft()
    {
        var @event = Event.CreateDraft(Guid.NewGuid(), Guid.NewGuid());

        @event.Status.Should().Be(EventStatus.Draft);
    }

    [Fact]
    public void CreateDraft_ShouldSetVersionToZero()
    {
        var @event = Event.CreateDraft(Guid.NewGuid(), Guid.NewGuid());

        @event.Version.Should().Be(0);
    }

    [Fact]
    public void UpdateDetails_ShouldSetProvidedFields()
    {
        var @event = Event.CreateDraft(Guid.NewGuid(), Guid.NewGuid());
        var start = DateTime.UtcNow.AddDays(1);
        var end = DateTime.UtcNow.AddDays(2);

        @event.UpdateDetails("Title", "Address", "Description", start, end, EventStatus.ReadyToPublish);

        @event.Title.Should().Be("Title");
        @event.Address.Should().Be("Address");
        @event.Description.Should().Be("Description");
        @event.StartTime.Should().Be(start);
        @event.EndTime.Should().Be(end);
        @event.Status.Should().Be(EventStatus.ReadyToPublish);
    }

    [Fact]
    public void UpdateDetails_WithNullParameters_ShouldKeepExistingValues()
    {
        var start = DateTime.UtcNow.AddDays(1);
        var end = DateTime.UtcNow.AddDays(2);
        var @event = Event.CreateDraft(Guid.NewGuid(), Guid.NewGuid());
        @event.UpdateDetails("Title", "Address", "Description", start, end, EventStatus.ReadyToPublish);

        @event.UpdateDetails(newStatus: EventStatus.Draft);

        @event.Title.Should().Be("Title");
        @event.Address.Should().Be("Address");
        @event.Description.Should().Be("Description");
        @event.StartTime.Should().Be(start);
        @event.EndTime.Should().Be(end);
        @event.Status.Should().Be(EventStatus.Draft);
    }


    [Fact]
    public void UpdateDetails_WhenStartTimeIsInPast_ShouldThrowDomainException()
    {
        var @event = Event.CreateDraft(Guid.NewGuid(), Guid.NewGuid());

        Action act = () => @event.UpdateDetails(title: "Title", address: "Address", description: "Description",
            startTime: DateTime.UtcNow.AddHours(-1), endTime: DateTime.UtcNow.AddDays(2));

        act.Should().Throw<DomainException>().WithMessage("Start time must be in the future.");
    }

    [Fact]
    public void UpdateDetails_WhenEndTimeIsBeforeStartTime_ShouldThrowDomainException()
    {
        var @event = Event.CreateDraft(Guid.NewGuid(), Guid.NewGuid());
        var start = DateTime.UtcNow.AddDays(2);
        var end = DateTime.UtcNow.AddDays(1);

        Action act = () => @event.UpdateDetails(title: "Title", address: "Address", description: "Description",
            startTime: start, endTime: end);

        act.Should().Throw<DomainException>().WithMessage("End time must be after start time.");
    }

    [Fact]
    public void UpdateDetails_WhenEventIsPublished_ShouldThrowDomainException()
    {
        var @event = Event.Reconstitute(
            Guid.NewGuid(), Guid.NewGuid(), "Title",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2),
            "Address", "Description", DateTime.UtcNow,
            EventStatus.Published, null, null, null, 0);

        Action act = () => @event.UpdateDetails(title: "New Title");

        act.Should().Throw<DomainException>().WithMessage("Cannot modify a published event.");
    }

    [Fact]
    public void Publish_WhenReadyToPublish_ShouldSetStatusToPublished()
    {
        var @event = Event.Reconstitute(
            Guid.NewGuid(), Guid.NewGuid(), "Title",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2),
            "Address", "Description", DateTime.UtcNow,
            EventStatus.ReadyToPublish, null, null, null, 0);

        @event.Publish();

        @event.Status.Should().Be(EventStatus.Published);
    }

    [Fact]
    public void Publish_WhenDraft_ShouldThrowDomainException()
    {
        var @event = Event.CreateDraft(Guid.NewGuid(), Guid.NewGuid());

        Action act = () => @event.Publish();

        act.Should().Throw<DomainException>().WithMessage("Only ready events can be published.");
    }

    [Fact]
    public void RevertToDraft_WhenPublished_ShouldSetStatusToDraft()
    {
        var @event = Event.Reconstitute(
            Guid.NewGuid(), Guid.NewGuid(), "Title",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2),
            "Address", "Description", DateTime.UtcNow,
            EventStatus.Published, null, null, null, 0);

        @event.RevertToDraft();

        @event.Status.Should().Be(EventStatus.Draft);
    }

    [Fact]
    public void RevertToDraft_WhenDraft_ShouldThrowDomainException()
    {
        var @event = Event.CreateDraft(Guid.NewGuid(), Guid.NewGuid());

        Action act = () => @event.RevertToDraft();

        act.Should().Throw<DomainException>().WithMessage("Only published events can be reverted to draft.");
    }

    [Fact]
    public void IncrementVersion_ShouldIncreaseVersionByOne()
    {
        var @event = Event.CreateDraft(Guid.NewGuid(), Guid.NewGuid());

        @event.IncrementVersion();

        @event.Version.Should().Be(1);
    }
}