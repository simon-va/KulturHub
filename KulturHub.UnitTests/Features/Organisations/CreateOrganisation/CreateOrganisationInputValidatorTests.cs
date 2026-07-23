using FluentAssertions;
using FluentValidation.TestHelper;
using KulturHub.Application.Features.Organisations.CreateOrganisation;

namespace KulturHub.UnitTests.Features.Organisations.CreateOrganisation;

public class CreateOrganisationInputValidatorTests
{
    // Rules:
    // - Name: required, max 200 chars
    // - UserId: required (non-empty Guid)

    private readonly CreateOrganisationInputValidator _sut = new();

    private static CreateOrganisationInput ValidInput() =>
        new("Acme Inc.", Guid.NewGuid());

    [Fact]
    public void Validate_WhenAllInputsAreValid_ShouldHaveNoErrors()
    {
        var result = _sut.TestValidate(ValidInput());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenNameIsBlank_ShouldHaveError(string name)
    {
        var input = ValidInput() with { Name = name };
        var result = _sut.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_WhenNameExceedsMaxLength_ShouldHaveError()
    {
        var input = ValidInput() with { Name = new string('a', 201) };
        var result = _sut.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_WhenNameMeetsMaxLength_ShouldNotHaveError()
    {
        var input = ValidInput() with { Name = new string('a', 200) };
        var result = _sut.TestValidate(input);
        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_WhenUserIdIsEmpty_ShouldHaveError()
    {
        var input = ValidInput() with { UserId = Guid.Empty };
        var result = _sut.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }
}
