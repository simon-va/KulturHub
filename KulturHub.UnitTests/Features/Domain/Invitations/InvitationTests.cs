using ErrorOr;
using FluentAssertions;
using KulturHub.Domain.Invitations;

namespace KulturHub.UnitTests.Features.Domain.Invitations;

public class InvitationTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_WhenCodeIsValid_ShouldSetFields()
    {
        var result = Invitation.Create("ABC-234", Now, Now.AddDays(1));

        result.IsError.Should().BeFalse();
        result.Value.Code.Should().Be("ABC-234");
        result.Value.CreatedAt.Should().Be(Now);
        result.Value.ExpiresAt.Should().Be(Now.AddDays(1));
        result.Value.IsDeleted.Should().BeFalse();
        result.Value.DeletedAt.Should().BeNull();
        result.Value.Id.Value.Should().NotBe(Guid.Empty);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WhenCodeIsNullOrEmpty_ShouldReturnValidationError(string? code)
    {
        var result = Invitation.Create(code!, Now, Now.AddDays(1));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Invitation.CodeRequired");
    }

    [Theory]
    [InlineData("ABC-23")]        // too short
    [InlineData("ABC-2344")]      // too long
    [InlineData("ABC234")]        // missing dash
    [InlineData("ABC-234 ")]      // trailing space
    [InlineData("0BC-234")]       // contains 0
    [InlineData("OBC-234")]       // contains O
    [InlineData("IBC-234")]       // contains I
    [InlineData("1BC-234")]       // contains 1
    [InlineData("ABC-23O")]       // contains O in second group
    public void Create_WhenCodeFormatIsInvalid_ShouldReturnValidationError(string code)
    {
        var result = Invitation.Create(code, Now, Now.AddDays(1));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Invitation.InvalidFormat");
    }

    [Fact]
    public void Create_WhenCreatedAtIsNotUtc_ShouldReturnValidationError()
    {
        var localNow = DateTime.SpecifyKind(Now, DateTimeKind.Local);

        var result = Invitation.Create("ABC-234", localNow, localNow.AddDays(1));

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Invitation.CreatedAtMustBeUtc");
    }

    [Fact]
    public void Create_WhenExpiresAtIsNotUtc_ShouldReturnValidationError()
    {
        var localExpires = DateTime.SpecifyKind(Now.AddDays(1), DateTimeKind.Local);

        var result = Invitation.Create("ABC-234", Now, localExpires);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Invitation.ExpiresAtMustBeUtc");
    }

    [Fact]
    public void Create_WhenExpiresAtIsNotAfterCreatedAt_ShouldReturnValidationError()
    {
        var result = Invitation.Create("ABC-234", Now, Now);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Invitation.ExpiresAtMustBeAfterCreatedAt");
    }
}
