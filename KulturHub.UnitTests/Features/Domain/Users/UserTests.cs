using ErrorOr;
using FluentAssertions;
using KulturHub.Domain.Users;
using Microsoft.Extensions.Time.Testing;

namespace KulturHub.UnitTests.Features.Domain.Users;

public class UserTests
{
    private static readonly DateTime NowUtc = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_WhenAllFieldsValid_ShouldReturnUser()
    {
        var clock = new FakeTimeProvider(NowUtc);
        var id = UserId.New();

        var result = User.Create(
            id,
            "max@example.com",
            "Max",
            "Mustermann",
            clock,
            isAdmin: false);

        result.IsError.Should().BeFalse();
        result.Value.Id.Should().Be(id);
        result.Value.Email.Should().Be("max@example.com");
        result.Value.FirstName.Should().Be("Max");
        result.Value.LastName.Should().Be("Mustermann");
        result.Value.IsAdmin.Should().BeFalse();
        result.Value.CreatedAt.Should().Be(NowUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WhenEmailIsEmpty_ShouldReturnValidationError(string? email)
    {
        var clock = new FakeTimeProvider(NowUtc);

        var result = User.Create(UserId.New(), email!, "Max", "Mustermann", clock);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("User.EmailRequired");
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@tld")]
    [InlineData("@no-local.com")]
    public void Create_WhenEmailIsInvalid_ShouldReturnValidationError(string email)
    {
        var clock = new FakeTimeProvider(NowUtc);

        var result = User.Create(UserId.New(), email, "Max", "Mustermann", clock);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("User.EmailInvalid");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WhenFirstNameIsEmpty_ShouldReturnValidationError(string? firstName)
    {
        var clock = new FakeTimeProvider(NowUtc);

        var result = User.Create(UserId.New(), "max@example.com", firstName!, "Mustermann", clock);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("User.FirstNameRequired");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WhenLastNameIsEmpty_ShouldReturnValidationError(string? lastName)
    {
        var clock = new FakeTimeProvider(NowUtc);

        var result = User.Create(UserId.New(), "max@example.com", "Max", lastName!, clock);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("User.LastNameRequired");
    }

    [Fact]
    public void Create_ShouldSetIsAdminFalseByDefault()
    {
        var clock = new FakeTimeProvider(NowUtc);

        var result = User.Create(UserId.New(), "max@example.com", "Max", "Mustermann", clock);

        result.IsError.Should().BeFalse();
        result.Value.IsAdmin.Should().BeFalse();
    }

    [Fact]
    public void Create_WithIsAdminTrue_ShouldSetIsAdminTrue()
    {
        var clock = new FakeTimeProvider(NowUtc);

        var result = User.Create(
            UserId.New(), "max@example.com", "Max", "Mustermann", clock, isAdmin: true);

        result.IsError.Should().BeFalse();
        result.Value.IsAdmin.Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldTrimEmailAndNames()
    {
        var clock = new FakeTimeProvider(NowUtc);

        var result = User.Create(
            UserId.New(),
            "  max@example.com  ",
            "  Max  ",
            "  Mustermann  ",
            clock);

        result.IsError.Should().BeFalse();
        result.Value.Email.Should().Be("max@example.com");
        result.Value.FirstName.Should().Be("Max");
        result.Value.LastName.Should().Be("Mustermann");
    }
}