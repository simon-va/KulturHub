using FluentAssertions;
using FluentValidation.TestHelper;
using KulturHub.Application.Features.Auth.SignUp;

namespace KulturHub.UnitTests.Features.Auth.SignUp;

public class SignUpInputValidatorTests
{
    // Rules:
    // - FirstName: required, max 100 chars
    // - LastName: required, max 100 chars
    // - Email: required, must be a valid email address
    // - Password: required, min 8 chars
    // - InvitationCode: required, must match XXX-XXX format (excludes 0, O, 1, I)

    private readonly SignUpInputValidator _sut = new();

    private static SignUpInput ValidInput() =>
        new("Max", "Mustermann", "max@example.com", "Secret123!", "K3P-R2A");

    [Fact]
    public void Validate_WhenAllInputsAreValid_ShouldHaveNoErrors()
    {
        var result = _sut.TestValidate(ValidInput());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenFirstNameIsBlank_ShouldHaveError(string firstName)
    {
        var input = ValidInput() with { FirstName = firstName };
        var result = _sut.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public void Validate_WhenFirstNameExceedsMaxLength_ShouldHaveError()
    {
        var input = ValidInput() with { FirstName = new string('a', 101) };
        var result = _sut.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenLastNameIsBlank_ShouldHaveError(string lastName)
    {
        var input = ValidInput() with { LastName = lastName };
        var result = _sut.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Fact]
    public void Validate_WhenLastNameExceedsMaxLength_ShouldHaveError()
    {
        var input = ValidInput() with { LastName = new string('a', 101) };
        var result = _sut.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("missing@")]
    [InlineData("@nolocal.com")]
    public void Validate_WhenEmailIsInvalid_ShouldHaveError(string email)
    {
        var input = ValidInput() with { Email = email };
        var result = _sut.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    public void Validate_WhenPasswordIsTooShort_ShouldHaveError(string password)
    {
        var input = ValidInput() with { Password = password };
        var result = _sut.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Validate_WhenPasswordMeetsMinimumLength_ShouldNotHaveError()
    {
        var input = ValidInput() with { Password = "12345678" };
        var result = _sut.TestValidate(input);
        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    [Theory]
    [InlineData("ABC-DEF")]
    [InlineData("234-567")]
    [InlineData("K3P-R2A")]
    [InlineData("ZZZ-222")]
    public void Validate_WhenInvitationCodeMatchesFormat_ShouldNotHaveError(string code)
    {
        var input = ValidInput() with { InvitationCode = code };
        var result = _sut.TestValidate(input);
        result.ShouldNotHaveValidationErrorFor(x => x.InvitationCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("K3PR2A")]
    [InlineData("K3P-R2A ")]
    [InlineData("K3P-R2")]
    [InlineData("K3P--R2A")]
    [InlineData("0KP-R2A")]
    [InlineData("K3P-R2O")]
    [InlineData("K3P-R21")]
    [InlineData("K3P-R2I")]
    [InlineData("k3p-r2a")]
    [InlineData("K3P_R2A")]
    public void Validate_WhenInvitationCodeIsBlank_ShouldHaveError(string code)
    {
        var input = ValidInput() with { InvitationCode = code };
        var result = _sut.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.InvitationCode);
    }
}
