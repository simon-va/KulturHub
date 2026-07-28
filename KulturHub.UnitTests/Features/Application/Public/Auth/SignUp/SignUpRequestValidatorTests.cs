using FluentAssertions;
using FluentValidation.TestHelper;
using KulturHub.Application.Features.Public.Auth.SignUp;

namespace KulturHub.UnitTests.Features.Application.Public.Auth.SignUp;

public class SignUpRequestValidatorTests
{
    private readonly SignUpRequestValidator _sut = new();

    private static SignUpRequest ValidRequest() => new(
        Email: "max@example.com",
        Password: "Sicher123!",
        FirstName: "Max",
        LastName: "Mustermann",
        InvitationCode: "ABC-234");

    [Fact]
    public void Validate_WhenAllFieldsAreValid_ShouldNotHaveErrors()
    {
        var result = _sut.TestValidate(ValidRequest());

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenEmailIsEmpty_ShouldHaveError(string email)
    {
        var request = ValidRequest() with { Email = email };

        var result = _sut.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("@no-local.com")]
    public void Validate_WhenEmailIsInvalid_ShouldHaveError(string email)
    {
        var request = ValidRequest() with { Email = email };

        var result = _sut.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    public void Validate_WhenPasswordIsTooShort_ShouldHaveError(string password)
    {
        var request = ValidRequest() with { Password = password };

        var result = _sut.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenFirstNameIsEmpty_ShouldHaveError(string firstName)
    {
        var request = ValidRequest() with { FirstName = firstName };

        var result = _sut.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenLastNameIsEmpty_ShouldHaveError(string lastName)
    {
        var request = ValidRequest() with { LastName = lastName };

        var result = _sut.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Theory]
    [InlineData("ABC-23")]
    [InlineData("ABC-2344")]
    [InlineData("0BC-234")]
    [InlineData("IBC-234")]
    [InlineData("ABC234")]
    public void Validate_WhenInvitationCodeFormatIsInvalid_ShouldHaveError(string code)
    {
        var request = ValidRequest() with { InvitationCode = code };

        var result = _sut.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.InvitationCode);
    }
}