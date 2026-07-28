using ErrorOr;
using FluentAssertions;
using KulturHub.Application.Errors;
using KulturHub.Application.Features.Platform.Organisations.UpdateOrganisation;
using KulturHub.Domain.ChangeLogs;
using KulturHub.Domain.Organisations;
using KulturHub.Infrastructure.Persistence;
using KulturHub.UnitTests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace KulturHub.UnitTests.Features.Application.Platform.Organisations.UpdateOrganisation;

public class UpdateOrganisationHandlerTests
{
    private static readonly DateTime NowUtc = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static (UpdateOrganisationHandler Sut, AppDbContext Db) CreateSut(
        IEnumerable<Organisation> seed)
    {
        var db = TestDbContextFactory.CreateInMemory();
        db.Organisations.AddRange(seed);
        db.SaveChanges();

        var clock = new FakeTimeProvider(NowUtc);
        var handler = new UpdateOrganisationHandler(
            db, clock, NullLogger<UpdateOrganisationHandler>.Instance);

        return (handler, db);
    }

    private static Organisation CreateExisting(string name, DateTime createdAt) =>
        Organisation.Create(name, new FakeTimeProvider(createdAt)).Value;

    [Fact]
    public async Task Handle_WhenRequestIsValid_ShouldUpdateNameAndWriteChangeLog()
    {
        var existing = CreateExisting("Alter Name", NowUtc);
        var (sut, db) = CreateSut([existing]);

        var result = await sut.HandleAsync(
            UserId, existing.Id.Value, new UpdateOrganisationRequest("Neuer Name"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        db.Organisations.Single().Name.Should().Be("Neuer Name");

        var changeLog = db.ChangeLogs.Single();
        changeLog.Message.Should().Be("Organisation wurde aktualisiert");
        changeLog.OrganisationId.Value.Should().Be(existing.Id.Value);
        changeLog.CreatedBy.Value.Should().Be(UserId);
        changeLog.CreatedAt.Should().Be(NowUtc);
        changeLog.IsDeleted.Should().BeFalse();
        changeLog.Data.Should().ContainKey("name");
        changeLog.Data["name"].Should().Be("Neuer Name");
    }

    [Fact]
    public async Task Handle_WhenOrganisationDoesNotExist_ShouldReturnNotFound()
    {
        var (sut, db) = CreateSut([]);

        var result = await sut.HandleAsync(
            UserId, Guid.NewGuid(), new UpdateOrganisationRequest("Neuer Name"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
        result.FirstError.Code.Should().Be("Organisation.NotFound");
        db.Organisations.Count().Should().Be(0);
        db.ChangeLogs.Count().Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenNameIsEmpty_ShouldReturnNameRequired()
    {
        var existing = CreateExisting("Alter Name", NowUtc);
        var (sut, db) = CreateSut([existing]);

        var result = await sut.HandleAsync(
            UserId, existing.Id.Value, new UpdateOrganisationRequest("  "), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Organisation.NameRequired");
        db.Organisations.Single().Name.Should().Be("Alter Name");
        db.ChangeLogs.Count().Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenNameExceeds200Characters_ShouldReturnNameTooLong()
    {
        var existing = CreateExisting("Alter Name", NowUtc);
        var (sut, db) = CreateSut([existing]);
        var tooLong = new string('a', 201);

        var result = await sut.HandleAsync(
            UserId, existing.Id.Value, new UpdateOrganisationRequest(tooLong), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Organisation.NameTooLong");
        db.Organisations.Single().Name.Should().Be("Alter Name");
        db.ChangeLogs.Count().Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenNameTakenByOtherOrganisation_ShouldReturnNameAlreadyExists()
    {
        var existing = CreateExisting("Alter Name", NowUtc);
        var other = CreateExisting("Schon Vergeben", NowUtc);
        var (sut, db) = CreateSut([existing, other]);

        var result = await sut.HandleAsync(
            UserId, existing.Id.Value, new UpdateOrganisationRequest("Schon Vergeben"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        result.FirstError.Code.Should().Be("Organisation.NameAlreadyExists");
        db.Organisations.AsNoTracking().Single(o => o.Id == existing.Id).Name.Should().Be("Alter Name");
        db.ChangeLogs.Count().Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenNameIsUnchanged_ShouldStillWriteChangeLog()
    {
        var existing = CreateExisting("Gleicher Name", NowUtc);
        var (sut, db) = CreateSut([existing]);

        var result = await sut.HandleAsync(
            UserId, existing.Id.Value, new UpdateOrganisationRequest("Gleicher Name"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        db.Organisations.Single().Name.Should().Be("Gleicher Name");
        db.ChangeLogs.Count().Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenOrganisationIsSoftDeleted_ShouldReturnNotFound()
    {
        var softDeleted = CreateExisting("Alter Name", NowUtc);
        var (sut, db) = CreateSut([softDeleted]);

        db.Entry(softDeleted).Property(nameof(Organisation.IsDeleted)).CurrentValue = true;
        db.SaveChanges();

        var result = await sut.HandleAsync(
            UserId, softDeleted.Id.Value, new UpdateOrganisationRequest("Neuer Name"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
        result.FirstError.Code.Should().Be("Organisation.NotFound");
        db.ChangeLogs.Count().Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenNameIsWhitespaceAround_ShouldBeTrimmed()
    {
        var existing = CreateExisting("Alter Name", NowUtc);
        var (sut, db) = CreateSut([existing]);

        var result = await sut.HandleAsync(
            UserId, existing.Id.Value, new UpdateOrganisationRequest("  Neuer Name  "), CancellationToken.None);

        result.IsError.Should().BeFalse();
        db.Organisations.Single().Name.Should().Be("Neuer Name");
        var changeLog = db.ChangeLogs.Single();
        changeLog.Data["name"].Should().Be("Neuer Name");
    }
}
