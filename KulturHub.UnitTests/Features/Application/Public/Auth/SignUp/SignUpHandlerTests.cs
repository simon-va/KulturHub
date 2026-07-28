using ErrorOr;
using FluentAssertions;
using KulturHub.Application.Abstractions.Persistence;
using KulturHub.Application.Errors;
using KulturHub.Application.Features.Public.Auth.SignUp;
using KulturHub.Application.Ports;
using KulturHub.Domain.Invitations;
using KulturHub.Domain.Memberships;
using KulturHub.Domain.Organisations;
using KulturHub.Domain.Users;
using KulturHub.Infrastructure.Persistence;
using KulturHub.UnitTests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace KulturHub.UnitTests.Features.Application.Public.Auth.SignUp;

public class SignUpHandlerTests
{
    private static readonly DateTime NowUtc = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static SignUpRequest ValidRequest() => new(
        Email: "max@example.com",
        Password: "Sicher123!",
        FirstName: "Max",
        LastName: "Mustermann",
        InvitationCode: "ABC-234");

    private static (
        SignUpHandler Sut,
        AppDbContext Db,
        Mock<IAuthProvider> Auth,
        Mock<IUserAdminClient> Admin) CreateSut(
        AuthProviderSession? sessionToReturn = null,
        Error? authError = null,
        Invitation? seedInvitation = null)
    {
        var db = TestDbContextFactory.CreateInMemory();
        if (seedInvitation is not null)
        {
            db.Invitations.Add(seedInvitation);
            db.SaveChanges();
        }

        var auth = new Mock<IAuthProvider>();
        if (authError is not null)
        {
            var errorResult = ErrorOr<AuthProviderSession>.From(new List<Error> { authError.Value });
            auth.Setup(a => a.SignUpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(errorResult);
        }
        else
        {
            sessionToReturn ??= new AuthProviderSession("access", "refresh", Guid.NewGuid());
            ErrorOr<AuthProviderSession> success = sessionToReturn;
            auth.Setup(a => a.SignUpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(success);
        }

        var admin = new Mock<IUserAdminClient>();
        admin.Setup(a => a.DeleteUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(true);

        var clock = new FakeTimeProvider(NowUtc);
        var handler = new SignUpHandler(
            db, auth.Object, admin.Object, clock, NullLogger<SignUpHandler>.Instance);

        return (handler, db, auth, admin);
    }

    private static Invitation CreateValidInvitation(string code = "ABC-234") =>
        Invitation.Create(code, NowUtc.AddDays(-1), NowUtc.AddDays(7)).Value;

    [Fact]
    public async Task Handle_WhenRequestIsValid_ShouldCreateUserAndMarkInvitationAsUsed()
    {
        var invitation = CreateValidInvitation();
        var session = new AuthProviderSession("access-1", "refresh-1", Guid.NewGuid());
        var (sut, db, auth, _) = CreateSut(session, seedInvitation: invitation);

        var result = await sut.HandleAsync(ValidRequest(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.AccessToken.Should().Be("access-1");
        result.Value.RefreshToken.Should().Be("refresh-1");
        result.Value.UserId.Should().Be(session.UserId);
        result.Value.FirstName.Should().Be("Max");
        result.Value.LastName.Should().Be("Mustermann");

        db.Users.Count().Should().Be(1);
        var user = db.Users.Single();
        user.Id.Value.Should().Be(session.UserId);
        user.Email.Should().Be("max@example.com");
        user.IsAdmin.Should().BeFalse();

        var storedInvitation = db.Invitations.Single(i => i.Code == "ABC-234");
        storedInvitation.IsUsed.Should().BeTrue();
        storedInvitation.UsedBy.Should().Be(user.Id.Value);

        auth.Verify(a => a.SignUpAsync("max@example.com", "Sicher123!", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenInvitationNotFound_ShouldReturnNotFound_AndNotCallSupabase()
    {
        var (sut, db, auth, admin) = CreateSut(seedInvitation: null);

        var result = await sut.HandleAsync(ValidRequest(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
        result.FirstError.Code.Should().Be("Invitation.NotFound");
        db.Users.Count().Should().Be(0);
        auth.Verify(a => a.SignUpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        admin.Verify(a => a.DeleteUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenInvitationAlreadyUsed_ShouldReturnConflict_AndNotCallSupabase()
    {
        var invitation = CreateValidInvitation();
        invitation.MarkAsUsed(Guid.NewGuid());
        var (sut, db, auth, _) = CreateSut(seedInvitation: invitation);

        var result = await sut.HandleAsync(ValidRequest(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        result.FirstError.Code.Should().Be("Invitation.AlreadyUsed");
        db.Users.Count().Should().Be(0);
        auth.Verify(a => a.SignUpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenInvitationExpired_ShouldReturnConflict_AndNotCallSupabase()
    {
        var expired = Invitation.Create("ABC-234", NowUtc.AddDays(-10), NowUtc.AddSeconds(-1)).Value;
        var (sut, db, auth, _) = CreateSut(seedInvitation: expired);

        var result = await sut.HandleAsync(ValidRequest(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        result.FirstError.Code.Should().Be("Invitation.Expired");
        db.Users.Count().Should().Be(0);
        auth.Verify(a => a.SignUpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSupabaseSignUpFailsWithAlreadyRegistered_ShouldReturnConflict_AndNotSaveUser()
    {
        var invitation = CreateValidInvitation();
        var (sut, db, _, admin) = CreateSut(
            authError: AuthErrors.AlreadyRegistered,
            seedInvitation: invitation);

        var result = await sut.HandleAsync(ValidRequest(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        result.FirstError.Code.Should().Be("Auth.AlreadyRegistered");
        db.Users.Count().Should().Be(0);
        admin.Verify(a => a.DeleteUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSupabaseSignUpFailsWithSignUpFailed_ShouldReturnFailure_AndNotSaveUser()
    {
        var invitation = CreateValidInvitation();
        var (sut, db, _, admin) = CreateSut(
            authError: AuthErrors.SignUpFailed,
            seedInvitation: invitation);

        var result = await sut.HandleAsync(ValidRequest(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Failure);
        result.FirstError.Code.Should().Be("Auth.SignUpFailed");
        db.Users.Count().Should().Be(0);
        admin.Verify(a => a.DeleteUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDbInsertFails_ShouldRollBackSupabaseUser_AndReturnUserCreationRolledBack()
    {
        var invitation = CreateValidInvitation();
        var session = new AuthProviderSession("access", "refresh", Guid.NewGuid());
        var (sut, db, auth, admin) = CreateSut(session, seedInvitation: invitation);

        // Seed a user with the same id to provoke the unique constraint (in-memory throws differently).
        // Instead, force a SaveChanges failure by stubbing: replace SaveChangesAsync with a throwing action
        // via a wrapper context. We use a custom IAppDbContext that wraps the real one and overrides SaveChangesAsync.
        var throwingDb = new ThrowingAppDbContext(db);
        var sut2 = new SignUpHandler(
            throwingDb, auth.Object, admin.Object, new FakeTimeProvider(NowUtc), NullLogger<SignUpHandler>.Instance);

        var result = await sut2.HandleAsync(ValidRequest(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Auth.UserCreationRolledBack");
        admin.Verify(a => a.DeleteUserAsync(session.UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenDbInsertFails_AndRollbackAlsoFails_ShouldReturnCompensatingDeleteFailed()
    {
        var invitation = CreateValidInvitation();
        var session = new AuthProviderSession("access", "refresh", Guid.NewGuid());
        var db = TestDbContextFactory.CreateInMemory();
        db.Invitations.Add(invitation);
        db.SaveChanges();

        var auth = new Mock<IAuthProvider>();
        auth.Setup(a => a.SignUpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var admin = new Mock<IUserAdminClient>();
        admin.Setup(a => a.DeleteUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(false);

        var throwingDb = new ThrowingAppDbContext(db);
        var sut = new SignUpHandler(
            throwingDb, auth.Object, admin.Object, new FakeTimeProvider(NowUtc), NullLogger<SignUpHandler>.Instance);

        var result = await sut.HandleAsync(ValidRequest(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Auth.CompensatingDeleteFailed");
    }

    [Fact]
    public async Task Handle_WhenUserValidationFailsAfterSupabaseSignUp_ShouldRollBackAuthUser()
    {
        var invitation = CreateValidInvitation();
        var session = new AuthProviderSession("access", "refresh", Guid.NewGuid());
        var db = TestDbContextFactory.CreateInMemory();
        db.Invitations.Add(invitation);
        db.SaveChanges();

        var auth = new Mock<IAuthProvider>();
        auth.Setup(a => a.SignUpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var admin = new Mock<IUserAdminClient>();
        admin.Setup(a => a.DeleteUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(true);

        var clock = new FakeTimeProvider(NowUtc);
        var sut = new SignUpHandler(
            db, auth.Object, admin.Object, clock, NullLogger<SignUpHandler>.Instance);

        var invalidRequest = ValidRequest() with { Email = "not-an-email" };

        var result = await sut.HandleAsync(invalidRequest, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("User.EmailInvalid");
        admin.Verify(a => a.DeleteUserAsync(session.UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class ThrowingAppDbContext(AppDbContext inner) : IAppDbContext
    {
        public DbSet<Invitation> Invitations => inner.Invitations;
        public DbSet<User> Users => inner.Users;
        public DbSet<Organisation> Organisations => inner.Organisations;
        public DbSet<Membership> Memberships => inner.Memberships;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new DbUpdateException("Simulated database failure.");
    }
}