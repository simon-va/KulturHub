using ErrorOr;
using FluentAssertions;
using KulturHub.Application.Errors;
using KulturHub.Application.Features.Admin.Invitations.CreateInvitation;
using KulturHub.Application.Ports;
using KulturHub.Domain.Invitations;
using KulturHub.Infrastructure.Persistence;
using KulturHub.UnitTests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace KulturHub.UnitTests.Features.Application.Invitations.CreateInvitation;

public class CreateInvitationHandlerTests
{
    private static readonly DateTime NowUtc = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static (CreateInvitationHandler Sut, AppDbContext Db, Mock<IInvitationCodeGenerator> Generator) CreateSut(
        IEnumerable<string> codesToReturn,
        IEnumerable<Invitation> seed)
    {
        var db = TestDbContextFactory.CreateInMemory();
        db.Invitations.AddRange(seed);
        db.SaveChanges();

        var queue = new Queue<string>(codesToReturn);
        var generator = new Mock<IInvitationCodeGenerator>();
        generator.Setup(g => g.Generate()).Returns(() =>
            queue.Count > 0 ? queue.Dequeue() : throw new InvalidOperationException("Generator exhausted."));

        var clock = new FakeTimeProvider(NowUtc);
        var handler = new CreateInvitationHandler(
            db, generator.Object, clock, NullLogger<CreateInvitationHandler>.Instance);

        return (handler, db, generator);
    }

    [Fact]
    public async Task Handle_ShouldCreateInvitationWith30DayExpiry()
    {
        var (sut, db, _) = CreateSut(["ABC-234"], []);

        var result = await sut.HandleAsync(CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.CreatedAt.Should().Be(NowUtc);
        result.Value.ExpiresAt.Should().Be(NowUtc.AddDays(30));
        result.Value.Code.Should().Be("ABC-234");
        db.Invitations.Count().Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenCodeAlreadyExists_ShouldGenerateDifferentCode()
    {
        var existing = Invitation.Create("AAA-BCD", NowUtc, NowUtc.AddDays(1)).Value;
        var (sut, db, _) = CreateSut(["AAA-BCD", "DEF-GHJ"], [existing]);

        var result = await sut.HandleAsync(CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Code.Should().Be("DEF-GHJ");
        db.Invitations.Count().Should().Be(2);
    }

    [Fact]
    public async Task Handle_WhenAllRetriesCollide_ShouldReturnCodeGenerationFailed()
    {
        var seed = new[]
        {
            Invitation.Create("AAA-BCD", NowUtc, NowUtc.AddDays(1)).Value,
            Invitation.Create("DEF-GHJ", NowUtc, NowUtc.AddDays(1)).Value,
            Invitation.Create("KLM-NPR", NowUtc, NowUtc.AddDays(1)).Value,
            Invitation.Create("STU-VWX", NowUtc, NowUtc.AddDays(1)).Value,
            Invitation.Create("YZA-BCD", NowUtc, NowUtc.AddDays(1)).Value,
        };

        var (sut, db, _) = CreateSut(
            ["AAA-BCD", "DEF-GHJ", "KLM-NPR", "STU-VWX", "YZA-BCD"],
            seed);

        var result = await sut.HandleAsync(CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        result.FirstError.Code.Should().Be("Invitation.CodeGenerationFailed");
        db.Invitations.Count().Should().Be(5);
    }
}
