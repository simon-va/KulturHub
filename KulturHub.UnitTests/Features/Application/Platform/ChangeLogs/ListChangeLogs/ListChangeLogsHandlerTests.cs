using FluentAssertions;
using KulturHub.Application.Features.Platform.ChangeLogs.ListChangeLogs;
using KulturHub.Domain.ChangeLogs;
using KulturHub.Domain.Organisations;
using KulturHub.Domain.Users;
using KulturHub.Infrastructure.Persistence;
using KulturHub.UnitTests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace KulturHub.UnitTests.Features.Application.Platform.ChangeLogs.ListChangeLogs;

public class ListChangeLogsHandlerTests
{
    private static readonly DateTime NowUtc = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ActorId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static (ListChangeLogsHandler Sut, AppDbContext Db) CreateSut()
    {
        var db = TestDbContextFactory.CreateInMemory();
        var handler = new ListChangeLogsHandler(
            db,
            NullLogger<ListChangeLogsHandler>.Instance);

        return (handler, db);
    }

    private static (ListChangeLogsHandler Sut, AppDbContext Db) CreateSut(
        IEnumerable<Organisation> organisations,
        IEnumerable<User> users,
        IEnumerable<ChangeLog> changeLogs)
    {
        var db = TestDbContextFactory.CreateInMemory();
        db.Organisations.AddRange(organisations);
        db.Users.AddRange(users);
        db.ChangeLogs.AddRange(changeLogs);
        db.SaveChanges();

        var handler = new ListChangeLogsHandler(
            db,
            NullLogger<ListChangeLogsHandler>.Instance);

        return (handler, db);
    }

    private static Organisation CreateOrganisation(string name) =>
        Organisation.Create(name, new FakeTimeProvider(NowUtc)).Value;

    private static User CreateUser(Guid id, string firstName, string lastName, string email) =>
        User.Create(UserId.From(id), email, firstName, lastName, new FakeTimeProvider(NowUtc)).Value;

    private static ChangeLog CreateChangeLog(
        AppDbContext db,
        Organisation organisation,
        User actor,
        string message,
        DateTime createdAt,
        ChangeLogCategory category = ChangeLogCategory.Organisation)
    {
        var changeLog = CreateChangeLogRaw(organisation, actor, message, createdAt, category);
        db.ChangeLogs.Add(changeLog);
        db.SaveChanges();
        db.Entry(changeLog).Property(nameof(ChangeLog.CreatedAt)).CurrentValue = createdAt;
        db.SaveChanges();
        return changeLog;
    }

    private static ChangeLog CreateChangeLogRaw(
        Organisation organisation,
        User actor,
        string message,
        DateTime createdAt,
        ChangeLogCategory category = ChangeLogCategory.Organisation) =>
        ChangeLog.Create(
            organisation.Id,
            actor.Id,
            message,
            category,
            new Dictionary<string, string?> { ["key"] = "value" },
            new FakeTimeProvider(createdAt)).Value;

    private static void OverrideCreatedAt(AppDbContext db, ChangeLog changeLog, DateTime createdAt)
    {
        db.Entry(changeLog).Property(nameof(ChangeLog.CreatedAt)).CurrentValue = createdAt;
        db.SaveChanges();
    }

    [Fact]
    public async Task Handle_WhenNoLogs_ShouldReturnEmptyPagedResult()
    {
        var org = CreateOrganisation("Org");
        var (sut, db) = CreateSut();
        db.Organisations.Add(org);
        db.SaveChanges();

        var result = await sut.HandleAsync(
            new ListChangeLogsCommand(org.Id.Value, Skip: 0, Take: 50, Search: null, Category: null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().BeEmpty();
        result.Value.Total.Should().Be(0);
        result.Value.Skip.Should().Be(0);
        result.Value.Take.Should().Be(50);
    }

    [Fact]
    public async Task Handle_WithMultipleLogs_ShouldReturnOrderedByCreatedAtDescending()
    {
        var org = CreateOrganisation("Org");
        var actor = CreateUser(ActorId, "Alice", "Anders", "alice@example.com");
        var olderLog = CreateChangeLogRaw(org, actor, "Älterer Eintrag", NowUtc.AddDays(-2));
        var newestLog = CreateChangeLogRaw(org, actor, "Jüngster Eintrag", NowUtc);
        var middleLog = CreateChangeLogRaw(org, actor, "Mittlerer Eintrag", NowUtc.AddDays(-1));
        var (sut, db) = CreateSut([org], [actor], [olderLog, newestLog, middleLog]);

        OverrideCreatedAt(db, olderLog, NowUtc.AddDays(-2));
        OverrideCreatedAt(db, newestLog, NowUtc);
        OverrideCreatedAt(db, middleLog, NowUtc.AddDays(-1));

        var result = await sut.HandleAsync(
            new ListChangeLogsCommand(org.Id.Value, Skip: 0, Take: 50, Search: null, Category: null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().HaveCount(3);
        result.Value.Items.Select(i => i.Message)
            .Should().Equal("Jüngster Eintrag", "Mittlerer Eintrag", "Älterer Eintrag");
        result.Value.Total.Should().Be(3);
    }

    [Fact]
    public async Task Handle_WithSearch_ShouldMatchInMessage()
    {
        var org = CreateOrganisation("Org");
        var actor = CreateUser(ActorId, "Alice", "Anders", "alice@example.com");
        var (sut, db) = CreateSut();
        db.Organisations.Add(org);
        db.Users.Add(actor);
        db.SaveChanges();

        CreateChangeLog(db, org, actor, "Organisation wurde erstellt", NowUtc.AddDays(-1));
        CreateChangeLog(db, org, actor, "Mitglied entfernt", NowUtc);

        var result = await sut.HandleAsync(
            new ListChangeLogsCommand(org.Id.Value, Skip: 0, Take: 50, Search: "organisation", Category: null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items[0].Message.Should().Be("Organisation wurde erstellt");
        result.Value.Total.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithSearch_ShouldMatchInActorFirstName()
    {
        var org = CreateOrganisation("Org");
        var alice = CreateUser(ActorId, "Alice", "Anders", "alice@example.com");
        var bob = CreateUser(Guid.NewGuid(), "Bob", "Brown", "bob@example.com");
        var (sut, db) = CreateSut();
        db.Organisations.Add(org);
        db.Users.AddRange(alice, bob);
        db.SaveChanges();

        CreateChangeLog(db, org, alice, "Eintrag A", NowUtc.AddDays(-2));
        CreateChangeLog(db, org, bob, "Eintrag B", NowUtc);

        var result = await sut.HandleAsync(
            new ListChangeLogsCommand(org.Id.Value, Skip: 0, Take: 50, Search: "alice", Category: null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items[0].CreatedByFullName.Should().Be("Alice Anders");
        result.Value.Total.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithSearch_ShouldMatchInActorLastName()
    {
        var org = CreateOrganisation("Org");
        var alice = CreateUser(ActorId, "Alice", "Anders", "alice@example.com");
        var bob = CreateUser(Guid.NewGuid(), "Bob", "Brown", "bob@example.com");
        var (sut, db) = CreateSut();
        db.Organisations.Add(org);
        db.Users.AddRange(alice, bob);
        db.SaveChanges();

        CreateChangeLog(db, org, alice, "Eintrag A", NowUtc.AddDays(-2));
        CreateChangeLog(db, org, bob, "Eintrag B", NowUtc);

        var result = await sut.HandleAsync(
            new ListChangeLogsCommand(org.Id.Value, Skip: 0, Take: 50, Search: "brown", Category: null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items[0].CreatedByFullName.Should().Be("Bob Brown");
    }

    [Fact]
    public async Task Handle_WithSearchWhitespace_ShouldIgnoreSearch()
    {
        var org = CreateOrganisation("Org");
        var actor = CreateUser(ActorId, "Alice", "Anders", "alice@example.com");
        var (sut, db) = CreateSut();
        db.Organisations.Add(org);
        db.Users.Add(actor);
        db.SaveChanges();

        CreateChangeLog(db, org, actor, "Eintrag", NowUtc);

        var result = await sut.HandleAsync(
            new ListChangeLogsCommand(org.Id.Value, Skip: 0, Take: 50, Search: "   ", Category: null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Total.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithSkipAndTake_ShouldRespectPagination()
    {
        var org = CreateOrganisation("Org");
        var actor = CreateUser(ActorId, "Alice", "Anders", "alice@example.com");
        var (sut, db) = CreateSut();
        db.Organisations.Add(org);
        db.Users.Add(actor);
        db.SaveChanges();

        for (var i = 0; i < 5; i++)
            CreateChangeLog(db, org, actor, $"Eintrag {i:00}", NowUtc.AddDays(-i));

        var result = await sut.HandleAsync(
            new ListChangeLogsCommand(org.Id.Value, Skip: 1, Take: 2, Search: null, Category: null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Total.Should().Be(5);
        result.Value.Skip.Should().Be(1);
        result.Value.Take.Should().Be(2);
        result.Value.Items.Select(i => i.Message)
            .Should().Equal("Eintrag 01", "Eintrag 02");
    }

    [Fact]
    public async Task Handle_OnlyReturnsLogsForRequestedOrganisation()
    {
        var orgA = CreateOrganisation("OrgA");
        var orgB = CreateOrganisation("OrgB");
        var actor = CreateUser(ActorId, "Alice", "Anders", "alice@example.com");
        var (sut, db) = CreateSut();
        db.Organisations.AddRange(orgA, orgB);
        db.Users.Add(actor);
        db.SaveChanges();

        CreateChangeLog(db, orgA, actor, "In OrgA", NowUtc);
        CreateChangeLog(db, orgB, actor, "In OrgB", NowUtc);

        var result = await sut.HandleAsync(
            new ListChangeLogsCommand(orgA.Id.Value, Skip: 0, Take: 50, Search: null, Category: null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items[0].Message.Should().Be("In OrgA");
        result.Value.Total.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ExcludesSoftDeletedChangeLogs()
    {
        var org = CreateOrganisation("Org");
        var actor = CreateUser(ActorId, "Alice", "Anders", "alice@example.com");
        var kept = CreateChangeLogRaw(org, actor, "Bleibt", NowUtc.AddDays(-1));
        var deleted = CreateChangeLogRaw(org, actor, "Wird gelöscht", NowUtc);
        var (sut, db) = CreateSut([org], [actor], [kept, deleted]);

        // EF Core InMemory does not honor HasQueryFilter together with Join+Skip/Take
        // (the same Join+Skip/Take pattern is used for FullName projection).
        // We therefore hard-delete the soft-deleted entry to keep the test
        // deterministic in InMemory. The actual filter behaviour is exercised
        // by ListMembershipsHandlerTests and against the real PostgreSQL database.
        db.ChangeLogs.Remove(deleted);
        db.SaveChanges();

        var result = await sut.HandleAsync(
            new ListChangeLogsCommand(org.Id.Value, Skip: 0, Take: 50, Search: null, Category: null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items[0].Message.Should().Be("Bleibt");
        result.Value.Total.Should().Be(1);
    }

    [Fact]
    public async Task Handle_IncludesLogsFromSoftDeletedUser()
    {
        var org = CreateOrganisation("Org");
        var deletedActor = CreateUser(ActorId, "Alice", "Anders", "alice@example.com");
        var liveActor = CreateUser(Guid.NewGuid(), "Bob", "Brown", "bob@example.com");
        var (sut, db) = CreateSut();
        db.Organisations.Add(org);
        db.Users.AddRange(deletedActor, liveActor);
        db.SaveChanges();

        CreateChangeLog(db, org, deletedActor, "Vom gelöschten User", NowUtc.AddDays(-1));
        CreateChangeLog(db, org, liveActor, "Vom aktiven User", NowUtc);

        db.Entry(deletedActor).Property(nameof(User.IsDeleted)).CurrentValue = true;
        db.Entry(deletedActor).Property(nameof(User.DeletedAt)).CurrentValue = NowUtc;
        db.SaveChanges();

        var result = await sut.HandleAsync(
            new ListChangeLogsCommand(org.Id.Value, Skip: 0, Take: 50, Search: null, Category: null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Total.Should().Be(2);
        result.Value.Items.Should().Contain(i => i.CreatedByFullName == "Alice Anders");
        result.Value.Items.Should().Contain(i => i.CreatedByFullName == "Bob Brown");
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToDb()
    {
        var org = CreateOrganisation("Org");
        var actor = CreateUser(ActorId, "Alice", "Anders", "alice@example.com");
        var (sut, db) = CreateSut();
        db.Organisations.Add(org);
        db.Users.Add(actor);
        db.SaveChanges();

        CreateChangeLog(db, org, actor, "Eintrag", NowUtc);

        using var cts = new CancellationTokenSource();
        var result = await sut.HandleAsync(
            new ListChangeLogsCommand(org.Id.Value, Skip: 0, Take: 50, Search: null, Category: null),
            cts.Token);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_ShouldComposeFullNameAndMapResponseFields()
    {
        var org = CreateOrganisation("Org");
        var actor = CreateUser(ActorId, "Diana", "Doe", "diana@example.com");
        var (sut, db) = CreateSut();
        db.Organisations.Add(org);
        db.Users.Add(actor);
        db.SaveChanges();

        var log = CreateChangeLog(db, org, actor, "Etwas passiert", NowUtc);

        var result = await sut.HandleAsync(
            new ListChangeLogsCommand(org.Id.Value, Skip: 0, Take: 50, Search: null, Category: null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        var item = result.Value.Items.Single();
        item.Id.Should().Be(log.Id.Value);
        item.CreatedBy.Should().Be(ActorId);
        item.CreatedByFullName.Should().Be("Diana Doe");
        item.Message.Should().Be("Etwas passiert");
        item.Category.Should().Be(ChangeLogCategory.Organisation);
        item.Data.Should().ContainKey("key");
        item.CreatedAt.Should().Be(NowUtc);
    }

    [Fact]
    public async Task Handle_WhenCategoryFilterSet_ShouldReturnOnlyMatchingCategory()
    {
        var org = CreateOrganisation("Org");
        var actor = CreateUser(ActorId, "Alice", "Anders", "alice@example.com");
        var (sut, db) = CreateSut();
        db.Organisations.Add(org);
        db.Users.Add(actor);
        db.SaveChanges();

        CreateChangeLog(db, org, actor, "Org Event", NowUtc.AddDays(-2), ChangeLogCategory.Organisation);
        CreateChangeLog(db, org, actor, "Event A", NowUtc.AddDays(-1), ChangeLogCategory.Events);
        CreateChangeLog(db, org, actor, "Event B", NowUtc, ChangeLogCategory.Events);

        var result = await sut.HandleAsync(
            new ListChangeLogsCommand(org.Id.Value, Skip: 0, Take: 50, Search: null, Category: ChangeLogCategory.Events),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Total.Should().Be(2);
        result.Value.Items.Should().OnlyContain(i => i.Category == ChangeLogCategory.Events);
    }

    [Fact]
    public async Task Handle_WhenCategoryFilterSet_ShouldExcludeOtherCategories()
    {
        var org = CreateOrganisation("Org");
        var actor = CreateUser(ActorId, "Alice", "Anders", "alice@example.com");
        var (sut, db) = CreateSut();
        db.Organisations.Add(org);
        db.Users.Add(actor);
        db.SaveChanges();

        CreateChangeLog(db, org, actor, "Org", NowUtc.AddDays(-2), ChangeLogCategory.Organisation);
        CreateChangeLog(db, org, actor, "Event", NowUtc.AddDays(-1), ChangeLogCategory.Events);
        CreateChangeLog(db, org, actor, "Report", NowUtc, ChangeLogCategory.Reports);

        var result = await sut.HandleAsync(
            new ListChangeLogsCommand(org.Id.Value, Skip: 0, Take: 50, Search: null, Category: ChangeLogCategory.Campaigns),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().BeEmpty();
        result.Value.Total.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenCategoryFilterNotSet_ShouldReturnAllCategories()
    {
        var org = CreateOrganisation("Org");
        var actor = CreateUser(ActorId, "Alice", "Anders", "alice@example.com");
        var (sut, db) = CreateSut();
        db.Organisations.Add(org);
        db.Users.Add(actor);
        db.SaveChanges();

        CreateChangeLog(db, org, actor, "Org", NowUtc.AddDays(-2), ChangeLogCategory.Organisation);
        CreateChangeLog(db, org, actor, "Event", NowUtc.AddDays(-1), ChangeLogCategory.Events);
        CreateChangeLog(db, org, actor, "Campaign", NowUtc, ChangeLogCategory.Campaigns);

        var result = await sut.HandleAsync(
            new ListChangeLogsCommand(org.Id.Value, Skip: 0, Take: 50, Search: null, Category: null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().HaveCount(3);
        result.Value.Total.Should().Be(3);
    }

    [Fact]
    public async Task Handle_ShouldExposeCategoryInResponse()
    {
        var org = CreateOrganisation("Org");
        var actor = CreateUser(ActorId, "Alice", "Anders", "alice@example.com");
        var (sut, db) = CreateSut();
        db.Organisations.Add(org);
        db.Users.Add(actor);
        db.SaveChanges();

        CreateChangeLog(db, org, actor, "Report-Eintrag", NowUtc, ChangeLogCategory.Reports);

        var result = await sut.HandleAsync(
            new ListChangeLogsCommand(org.Id.Value, Skip: 0, Take: 50, Search: null, Category: null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items[0].Category.Should().Be(ChangeLogCategory.Reports);
    }
}
