using System.Text.RegularExpressions;
using FluentAssertions;
using KulturHub.Domain.Invitations;

namespace KulturHub.UnitTests.Features.Domain.Invitations;

public class InvitationCodeGeneratorTests
{
    private static readonly Regex FormatRegex = new(
        "^[A-HJ-NP-Z2-9]{3}-[A-HJ-NP-Z2-9]{3}$",
        RegexOptions.Compiled);

    [Fact]
    public void Generate_ShouldReturnValidFormat()
    {
        var code = InvitationCodeGenerator.Generate();

        code.Should().HaveLength(7);
        code[3].Should().Be('-');
        FormatRegex.IsMatch(code).Should().BeTrue();
    }

    [Fact]
    public void Generate_ShouldNotContainForbiddenCharacters()
    {
        for (var i = 0; i < 1000; i++)
        {
            var code = InvitationCodeGenerator.Generate();

            code.Should().NotContain("0");
            code.Should().NotContain("O");
            code.Should().NotContain("I");
            code.Should().NotContain("1");
        }
    }

    [Theory]
    [InlineData("ABC-234", true)]
    [InlineData("XYZ-987", true)]
    [InlineData("HJK-LMN", true)]
    [InlineData("AAA-AAA", true)]
    [InlineData("abc-234", false)]   // lowercase
    [InlineData("ABCD-234", false)]  // too long first group
    [InlineData("AB-2345", false)]   // too long second group
    [InlineData("ABC-23", false)]    // too short
    [InlineData("ABC234", false)]    // missing dash
    [InlineData("0BC-234", false)]   // 0
    [InlineData("OBC-234", false)]   // O
    [InlineData("IBC-234", false)]   // I
    [InlineData("1BC-234", false)]   // 1
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValid_ShouldMatchExpectedResult(string? code, bool expected)
    {
        InvitationCodeGenerator.IsValid(code!).Should().Be(expected);
    }
}
