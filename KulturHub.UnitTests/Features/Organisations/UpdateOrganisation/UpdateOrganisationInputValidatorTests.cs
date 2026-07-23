using FluentAssertions;
using FluentValidation.TestHelper;
using KulturHub.Application.Features.Organisations.UpdateOrganisation;

namespace KulturHub.UnitTests.Features.Organisations.UpdateOrganisation;

public class UpdateOrganisationInputValidatorTests
{
    // Rules:
    // - Name: required, max 200 chars
    // - UserId: required (non-empty Guid)
    // - OrganisationId: required (non-empty Guid)

    private readonly UpdateOrganisationInputValidator _sut = new();

    private static UpdateOrganisationInput ValidInput() =>
        new("Acme Inc.", Guid.NewGuid(), Guid.NewGuid());

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

    [Fact]
    public void Validate_WhenOrganisationIdIsEmpty_ShouldHaveError()
    {
        var input = ValidInput() with { OrganisationId = Guid.Empty };
        var result = _sut.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.OrganisationId);
    }
}
