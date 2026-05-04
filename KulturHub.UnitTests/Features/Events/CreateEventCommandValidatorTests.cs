using FluentAssertions;
using KulturHub.Application.Features.Events.CreateEvent;

namespace KulturHub.UnitTests.Features.Events;

public class CreateEventInputValidatorTests
{
    private readonly CreateEventInputValidator _validator = new();

    private static CreateEventInput ValidInput() => new(
        OrganisationId: Guid.NewGuid(),
        UserId: Guid.NewGuid(),
        Title: "Konzert im Park",
        StartTime: DateTime.UtcNow.AddDays(1),
        EndTime: DateTime.UtcNow.AddDays(1).AddHours(2),
        Address: "Musterstraße 1, 12345 Musterstadt",
        Description: "Ein großartiges Konzert.");

    [Fact]
    public void Validate_WhenTitleIsEmpty_ShouldHaveValidationError()
    {
        var input = ValidInput() with { Title = "" };
        _validator.Validate(input).Errors
            .Should().ContainSingle(e => e.PropertyName == nameof(input.Title));
    }

    [Fact]
    public void Validate_WhenTitleExceeds200Characters_ShouldHaveValidationError()
    {
        var input = ValidInput() with { Title = new string('x', 201) };
        _validator.Validate(input).Errors
            .Should().ContainSingle(e => e.PropertyName == nameof(input.Title));
    }

    [Fact]
    public void Validate_WhenStartTimeIsDefault_ShouldHaveValidationError()
    {
        var input = ValidInput() with { StartTime = default };
        _validator.Validate(input).Errors
            .Should().Contain(e => e.PropertyName == nameof(input.StartTime));
    }

    [Fact]
    public void Validate_WhenStartTimeIsInThePast_ShouldHaveValidationError()
    {
        var input = ValidInput() with { StartTime = DateTime.UtcNow.AddDays(-1) };
        _validator.Validate(input).Errors
            .Should().ContainSingle(e => e.PropertyName == nameof(input.StartTime));
    }

    [Fact]
    public void Validate_WhenEndTimeIsBeforeStartTime_ShouldHaveValidationError()
    {
        var input = ValidInput() with { EndTime = DateTime.UtcNow.AddHours(1) };
        _validator.Validate(input).Errors
            .Should().ContainSingle(e => e.PropertyName == nameof(input.EndTime));
    }

    [Fact]
    public void Validate_WhenEndTimeEqualsStartTime_ShouldHaveValidationError()
    {
        var time = DateTime.UtcNow.AddDays(1);
        var input = ValidInput() with { StartTime = time, EndTime = time };
        _validator.Validate(input).Errors
            .Should().ContainSingle(e => e.PropertyName == nameof(input.EndTime));
    }

    [Fact]
    public void Validate_WhenAddressIsEmpty_ShouldHaveValidationError()
    {
        var input = ValidInput() with { Address = string.Empty };
        _validator.Validate(input).Errors
            .Should().ContainSingle(e => e.PropertyName == nameof(input.Address));
    }

    [Fact]
    public void Validate_WhenAddressExceeds500Characters_ShouldHaveValidationError()
    {
        var input = ValidInput() with { Address = new string('a', 501) };
        _validator.Validate(input).Errors
            .Should().ContainSingle(e => e.PropertyName == nameof(input.Address));
    }

    [Fact]
    public void Validate_WhenDescriptionIsEmpty_ShouldHaveValidationError()
    {
        var input = ValidInput() with { Description = string.Empty };
        _validator.Validate(input).Errors
            .Should().ContainSingle(e => e.PropertyName == nameof(input.Description));
    }

    [Fact]
    public void Validate_WhenDescriptionExceeds2000Characters_ShouldHaveValidationError()
    {
        var input = ValidInput() with { Description = new string('d', 2001) };
        _validator.Validate(input).Errors
            .Should().ContainSingle(e => e.PropertyName == nameof(input.Description));
    }

    [Fact]
    public void Validate_WhenInputIsValid_ShouldHaveNoValidationErrors()
    {
        _validator.Validate(ValidInput()).IsValid.Should().BeTrue();
    }
}
