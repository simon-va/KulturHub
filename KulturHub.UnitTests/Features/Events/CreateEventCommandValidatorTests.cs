using FluentAssertions;
using KulturHub.Application.Features.Events.CreateEvent;

namespace KulturHub.UnitTests.Features.Events;

public class CreateEventCommandValidatorTests
{
    private readonly CreateEventCommandValidator _validator = new();

    private static CreateEventCommand ValidCommand() => new(
        Title: "Konzert im Park",
        StartTime: DateTime.UtcNow.AddDays(1),
        EndTime: DateTime.UtcNow.AddDays(1).AddHours(2),
        Address: "Musterstraße 1, 12345 Musterstadt",
        Description: "Ein großartiges Konzert.");

    [Fact]
    public void Validate_WhenTitleIsEmpty_ShouldHaveValidationError()
    {
        var cmd = ValidCommand() with { Title = "" };
        _validator.Validate(cmd).Errors
            .Should().ContainSingle(e => e.PropertyName == nameof(cmd.Title));
    }

    [Fact]
    public void Validate_WhenTitleExceeds200Characters_ShouldHaveValidationError()
    {
        var cmd = ValidCommand() with { Title = new string('x', 201) };
        _validator.Validate(cmd).Errors
            .Should().ContainSingle(e => e.PropertyName == nameof(cmd.Title));
    }

    [Fact]
    public void Validate_WhenStartTimeIsDefault_ShouldHaveValidationError()
    {
        var cmd = ValidCommand() with { StartTime = default };
        _validator.Validate(cmd).Errors
            .Should().Contain(e => e.PropertyName == nameof(cmd.StartTime));
    }

    [Fact]
    public void Validate_WhenStartTimeIsInThePast_ShouldHaveValidationError()
    {
        var cmd = ValidCommand() with { StartTime = DateTime.UtcNow.AddDays(-1) };
        _validator.Validate(cmd).Errors
            .Should().ContainSingle(e => e.PropertyName == nameof(cmd.StartTime));
    }

    [Fact]
    public void Validate_WhenEndTimeIsBeforeStartTime_ShouldHaveValidationError()
    {
        var cmd = ValidCommand() with { EndTime = DateTime.UtcNow.AddHours(1) };
        _validator.Validate(cmd).Errors
            .Should().ContainSingle(e => e.PropertyName == nameof(cmd.EndTime));
    }

    [Fact]
    public void Validate_WhenEndTimeEqualsStartTime_ShouldHaveValidationError()
    {
        var time = DateTime.UtcNow.AddDays(1);
        var cmd = ValidCommand() with { StartTime = time, EndTime = time };
        _validator.Validate(cmd).Errors
            .Should().ContainSingle(e => e.PropertyName == nameof(cmd.EndTime));
    }

    [Fact]
    public void Validate_WhenAddressIsEmpty_ShouldHaveValidationError()
    {
        var cmd = ValidCommand() with { Address = string.Empty };
        _validator.Validate(cmd).Errors
            .Should().ContainSingle(e => e.PropertyName == nameof(cmd.Address));
    }

    [Fact]
    public void Validate_WhenAddressExceeds500Characters_ShouldHaveValidationError()
    {
        var cmd = ValidCommand() with { Address = new string('a', 501) };
        _validator.Validate(cmd).Errors
            .Should().ContainSingle(e => e.PropertyName == nameof(cmd.Address));
    }

    [Fact]
    public void Validate_WhenDescriptionIsEmpty_ShouldHaveValidationError()
    {
        var cmd = ValidCommand() with { Description = string.Empty };
        _validator.Validate(cmd).Errors
            .Should().ContainSingle(e => e.PropertyName == nameof(cmd.Description));
    }

    [Fact]
    public void Validate_WhenDescriptionExceeds2000Characters_ShouldHaveValidationError()
    {
        var cmd = ValidCommand() with { Description = new string('d', 2001) };
        _validator.Validate(cmd).Errors
            .Should().ContainSingle(e => e.PropertyName == nameof(cmd.Description));
    }

    [Fact]
    public void Validate_WhenCommandIsValid_ShouldHaveNoValidationErrors()
    {
        _validator.Validate(ValidCommand()).IsValid.Should().BeTrue();
    }
}
