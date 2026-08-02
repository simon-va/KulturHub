using ErrorOr;
using FluentAssertions;
using KulturHub.Application.Errors;
using KulturHub.Application.Features.Platform.Organisations.CreateOrganisation;
using KulturHub.Domain.ChangeLogs;
using KulturHub.Domain.Organisations;
using KulturHub.Domain.Users;
using KulturHub.Infrastructure.Persistence;
using KulturHub.UnitTests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace KulturHub.UnitTests.Features.Application.Platform.Organisations.CreateOrganisation;

public class CreateOrganisationHandlerTests
{
    private static readonly DateTime NowUtc = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static (CreateOrganisationHandler Sut, AppDbContext Db) CreateSut(
        IEnumerable<Organisation> seed)
    {
        var db = TestDbContextFactory.CreateInMemory();
        db.Organisations.AddRange(seed);
        db.SaveChanges();

        var clock = new FakeTimeProvider(NowUtc);
        var handler = new CreateOrganisationHandler(
            db, clock, NullLogger<CreateOrganisationHandler>.Instance);

        return (handler, db);
    }

    private static CreateOrganisationCommand ValidCommand() => new(UserId, "Kulturverein Rügen");

    [Fact]
    public async Task Handle_WhenRequestIsValid_ShouldCreateOrganisationAndMembershipAndChangeLog()
    {
        var (sut, db) = CreateSut([]);

        var result = await sut.HandleAsync(ValidCommand(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Name.Should().Be("Kulturverein Rügen");
        result.Value.CreatedAt.Should().Be(NowUtc);

        db.Organisations.Count().Should().Be(1);
        db.Memberships.Count().Should().Be(1);
        db.ChangeLogs.Count().Should().Be(1);

        var membership = db.Memberships.Single();
        membership.UserId.Value.Should().Be(UserId);
        membership.OrganisationId.Value.Should().Be(result.Value.Id);
        membership.DecidedAt.Should().Be(NowUtc);
        membership.InvitedAt.Should().Be(NowUtc);
        membership.Status.Should().Be(KulturHub.Domain.Memberships.MembershipStatus.Accepted);

        var changeLog = db.ChangeLogs.Single();
        changeLog.Message.Should().Be("Organisation wurde erstellt");
        changeLog.OrganisationId.Value.Should().Be(result.Value.Id);
        changeLog.CreatedBy.Value.Should().Be(UserId);
        changeLog.CreatedAt.Should().Be(NowUtc);
        changeLog.Category.Should().Be(KulturHub.Domain.ChangeLogs.ChangeLogCategory.Organisation);
        changeLog.IsDeleted.Should().BeFalse();
        changeLog.Data.Should().ContainKey("name");
        changeLog.Data["name"].Should().Be("Kulturverein Rügen");
    }

    [Fact]
    public async Task Handle_WhenNameIsEmpty_ShouldReturnNameRequired()
    {
        var (sut, db) = CreateSut([]);

        var result = await sut.HandleAsync(ValidCommand() with { Name = "  " }, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Organisation.NameRequired");
        db.Organisations.Count().Should().Be(0);
        db.Memberships.Count().Should().Be(0);
        db.ChangeLogs.Count().Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenNameExceeds200Characters_ShouldReturnNameTooLong()
    {
        var (sut, db) = CreateSut([]);
        var tooLong = new string('a', 201);

        var result = await sut.HandleAsync(ValidCommand() with { Name = tooLong }, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Organisation.NameTooLong");
        db.Organisations.Count().Should().Be(0);
        db.Memberships.Count().Should().Be(0);
        db.ChangeLogs.Count().Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenNameAlreadyExists_ShouldReturnNameAlreadyExists()
    {
        var existing = Organisation.Create("Kulturverein Rügen", new FakeTimeProvider(NowUtc)).Value;
        var (sut, db) = CreateSut([existing]);

        var result = await sut.HandleAsync(ValidCommand(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        result.FirstError.Code.Should().Be("Organisation.NameAlreadyExists");
        db.Organisations.Count().Should().Be(1);
        db.Memberships.Count().Should().Be(0);
        db.ChangeLogs.Count().Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenNameBelongsToSoftDeletedOrganisation_ShouldCreateNewOrganisationAndChangeLog()
    {
        var softDeleted = Organisation.Create("Kulturverein Rügen", new FakeTimeProvider(NowUtc)).Value;
        var (sut, db) = CreateSut([softDeleted]);

        db.Entry(softDeleted).Property(nameof(Organisation.IsDeleted)).CurrentValue = true;
        db.SaveChanges();

        var result = await sut.HandleAsync(ValidCommand(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        db.Organisations.Count().Should().Be(1);
        db.Organisations.IgnoreQueryFilters().Count().Should().Be(2);
        db.Memberships.Count().Should().Be(1);
        db.ChangeLogs.Count().Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenNameIsWhitespaceAround_ShouldBeTrimmed()
    {
        var (sut, db) = CreateSut([]);

        var result = await sut.HandleAsync(ValidCommand() with { Name = "  Kulturverein Rügen  " }, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Name.Should().Be("Kulturverein Rügen");
        var changeLog = db.ChangeLogs.Single();
        changeLog.Data.Should().ContainKey("name");
        changeLog.Data["name"].Should().Be("Kulturverein Rügen");
    }
}
