using ErrorOr;
using FluentAssertions;
using KulturHub.Application.Errors;
using KulturHub.Application.Features.Platform.Users.GetCurrentUser;
using KulturHub.Domain.Users;
using KulturHub.Infrastructure.Persistence;
using KulturHub.UnitTests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace KulturHub.UnitTests.Features.Application.Platform.Users.GetCurrentUser;

public class GetCurrentUserHandlerTests
{
    private static readonly DateTime NowUtc = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserIdValue = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static (GetCurrentUserHandler Sut, AppDbContext Db) CreateSut(User? user = null)
    {
        var db = TestDbContextFactory.CreateInMemory();
        if (user is not null)
        {
            db.Users.Add(user);
            db.SaveChanges();
        }

        var handler = new GetCurrentUserHandler(
            db,
            NullLogger<GetCurrentUserHandler>.Instance);

        return (handler, db);
    }

    private static User CreateUser(string firstName, string lastName, string email, bool isAdmin = false) =>
        User.Create(UserId.From(UserIdValue), email, firstName, lastName, new FakeTimeProvider(NowUtc), isAdmin).Value;

    [Fact]
    public async Task Handle_WhenUserExists_ShouldReturnMeResponse()
    {
        var user = CreateUser("Max", "Mustermann", "max@example.com");
        var (sut, _) = CreateSut(user);

        var result = await sut.HandleAsync(UserIdValue, CancellationToken.None);

        result.IsError.Should().BeFalse();
        var response = result.Value;
        response.UserId.Should().Be(UserIdValue);
        response.FirstName.Should().Be("Max");
        response.LastName.Should().Be("Mustermann");
        response.Email.Should().Be("max@example.com");
        response.IsAdmin.Should().BeFalse();
        response.CreatedAt.Should().Be(NowUtc);
    }

    [Fact]
    public async Task Handle_WhenUserIsSoftDeleted_ShouldReturnNotFound()
    {
        var user = CreateUser("Max", "Mustermann", "max@example.com");
        var (sut, db) = CreateSut(user);

        db.Entry(user).Property(nameof(User.IsDeleted)).CurrentValue = true;
        db.Entry(user).Property(nameof(User.DeletedAt)).CurrentValue = NowUtc.AddDays(1);
        db.SaveChanges();

        var result = await sut.HandleAsync(UserIdValue, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
        result.FirstError.Code.Should().Be("User.NotFound");
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExistInDb_ShouldReturnNotFound()
    {
        var (sut, _) = CreateSut();

        var result = await sut.HandleAsync(UserIdValue, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
        result.FirstError.Code.Should().Be("User.NotFound");
    }

    [Fact]
    public async Task Handle_WhenUserIsAdmin_ShouldReturnIsAdminTrue()
    {
        var user = CreateUser("Max", "Mustermann", "max@example.com", isAdmin: true);
        var (sut, _) = CreateSut(user);

        var result = await sut.HandleAsync(UserIdValue, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.IsAdmin.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldBeIdempotent()
    {
        var user = CreateUser("Max", "Mustermann", "max@example.com");
        var (sut, _) = CreateSut(user);

        var first = await sut.HandleAsync(UserIdValue, CancellationToken.None);
        var second = await sut.HandleAsync(UserIdValue, CancellationToken.None);

        first.IsError.Should().BeFalse();
        second.IsError.Should().BeFalse();
        first.Value.UserId.Should().Be(second.Value.UserId);
    }
}
