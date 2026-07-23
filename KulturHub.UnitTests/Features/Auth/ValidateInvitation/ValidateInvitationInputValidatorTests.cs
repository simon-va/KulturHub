using FluentAssertions;
using FluentValidation.TestHelper;
using KulturHub.Application.Features.Auth.ValidateInvitation;

namespace KulturHub.UnitTests.Features.Auth.ValidateInvitation;

public class ValidateInvitationInputValidatorTests
{
    // Rules:
    // - InvitationCode must not be empty.
    // - InvitationCode must match the InvitationCodeRules.CodePattern (XXX-XXX from the visible alphabet).

    private readonly ValidateInvitationInputValidator _sut = new();

    [Theory]
    [InlineData("ABC-DEF")]
    [InlineData("234-567")]
    [InlineData("K3P-R2A")]
    [InlineData("ZZZ-222")]
    [InlineData("HJK-LMN")]
    public void Validate_WhenInvitationCodeMatchesPattern_ShouldNotHaveError(string code)
    {
        var result = _sut.TestValidate(new ValidateInvitationInput(code));
        result.ShouldNotHaveValidationErrorFor(x => x.InvitationCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ABCDEF")]
    [InlineData("K3P-R2A ")]
    [InlineData("K3P-R2")]
    [InlineData("ABCD-EF")]
    [InlineData("0KP-R2A")]
    [InlineData("K3P-R2O")]
    [InlineData("K3P-R21")]
    [InlineData("K3P-R2I")]
    [InlineData("k3p-r2a")]
    [InlineData("K3P_R2A")]
    public void Validate_WhenInvitationCodeIsInvalid_ShouldHaveError(string code)
    {
        var result = _sut.TestValidate(new ValidateInvitationInput(code));
        result.ShouldHaveValidationErrorFor(x => x.InvitationCode);
    }
}
