using ErrorOr;
using FluentAssertions;
using KulturHub.Application.Abstractions.Persistence;
using KulturHub.Application.Errors;
using KulturHub.Application.Features.Public.Auth.ValidateInvitation;
using KulturHub.Domain.Invitations;
using KulturHub.Infrastructure.Persistence;
using KulturHub.UnitTests.TestHelpers;
using Microsoft.Extensions.Time.Testing;

namespace KulturHub.UnitTests.Features.Application.Public.Auth.ValidateInvitation;

public class ValidateInvitationHandlerTests
{
    private static readonly DateTime NowUtc = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static ValidateInvitationRequest ValidRequest(string code = "ABC-234") =>
        new(InvitationCode: code);

    private static (ValidateInvitationHandler Sut, AppDbContext Db) CreateSut(
        Invitation? seedInvitation = null)
    {
        var db = TestDbContextFactory.CreateInMemory();
        if (seedInvitation is not null)
        {
            db.Invitations.Add(seedInvitation);
            db.SaveChanges();
        }

        var clock = new FakeTimeProvider(NowUtc);
        var handler = new ValidateInvitationHandler(db, clock);

        return (handler, db);
    }

    private static Invitation CreateValidInvitation(string code = "ABC-234") =>
        Invitation.Create(code, NowUtc.AddDays(-1), NowUtc.AddDays(7)).Value;

    [Fact]
    public async Task Handle_WhenInvitationIsValidAndNotExpired_ShouldReturnSuccess_AndNotMutateInvitation()
    {
        var invitation = CreateValidInvitation();
        var (sut, db) = CreateSut(seedInvitation: invitation);

        var result = await sut.HandleAsync(ValidRequest(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        db.Invitations.Single().IsUsed.Should().BeFalse();
        db.Invitations.Single().UsedBy.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenInvitationNotFound_ShouldReturnNotFound()
    {
        var (sut, db) = CreateSut(seedInvitation: null);

        var result = await sut.HandleAsync(ValidRequest(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
        result.FirstError.Code.Should().Be("Invitation.NotFound");
        db.Invitations.Count().Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenInvitationAlreadyUsed_ShouldReturnConflict()
    {
        var invitation = CreateValidInvitation();
        invitation.MarkAsUsed(Guid.NewGuid());
        var (sut, db) = CreateSut(seedInvitation: invitation);

        var result = await sut.HandleAsync(ValidRequest(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        result.FirstError.Code.Should().Be("Invitation.AlreadyUsed");
        db.Invitations.Single().IsUsed.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenInvitationExpired_ShouldReturnConflict()
    {
        var expired = Invitation.Create("ABC-234", NowUtc.AddDays(-10), NowUtc.AddSeconds(-1)).Value;
        var (sut, db) = CreateSut(seedInvitation: expired);

        var result = await sut.HandleAsync(ValidRequest(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        result.FirstError.Code.Should().Be("Invitation.Expired");
        db.Invitations.Single().IsUsed.Should().BeFalse();
    }
}
