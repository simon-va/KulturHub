using ErrorOr;
using FluentAssertions;
using FluentValidation;
using KulturHub.Application.Features.ChangeLogs.ListOrganisationChangeLogs;
using KulturHub.Application.Ports;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace KulturHub.UnitTests.Features.ChangeLogs.ListOrganisationChangeLogs;

public class ListOrganisationChangeLogsHandlerTests
{
    // Rules:
    // - ExecuteAsync forwards organisationId, skip and take from the query to the repository.
    // - ExecuteAsync returns the list items from the repository as-is.
    // - If organisationId is Guid.Empty, a Validation error is returned and the
    //   repository is not called.
    // - Default skip=0 and take=50 when query values are null.
    // - skip < 0 → Validation error, repository is not called.
    // - take outside [1, 200] → Validation error, repository is not called.
    // - The repository's CancellationToken is forwarded to the repository call.

    private readonly Mock<IChangeLogRepository> _changeLogRepositoryMock = new();
    private readonly ListOrganisationChangeLogsHandler _sut;

    public ListOrganisationChangeLogsHandlerTests()
    {
        _sut = new ListOrganisationChangeLogsHandler(
            _changeLogRepositoryMock.Object,
            new ListOrganisationChangeLogsQueryValidator(),
            NullLogger<ListOrganisationChangeLogsHandler>.Instance);
    }

    private static ChangeLogListItem ValidItem(Guid organisationId) =>
        new(
            Guid.NewGuid(),
            organisationId,
            Guid.NewGuid(),
            "Anna Müller",
            "Organisation aktualisiert",
            new Dictionary<string, object?> { ["name"] = "Neuer Name" },
            DateTime.UtcNow.AddMinutes(-1));

    [Fact]
    public async Task ExecuteAsync_ShouldPassOrganisationIdSkipAndTakeToRepository()
    {
        var organisationId = Guid.NewGuid();
        _changeLogRepositoryMock
            .Setup(r => r.ListByOrganisationAsync(organisationId, 10, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChangeLogListItem>());

        var result = await _sut.ExecuteAsync(
            new ListOrganisationChangeLogsQuery(organisationId, 10, 25));

        result.IsError.Should().BeFalse();
        _changeLogRepositoryMock.Verify(
            r => r.ListByOrganisationAsync(organisationId, 10, 25, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldApplyDefaults_WhenQueryValuesAreNull()
    {
        var organisationId = Guid.NewGuid();
        _changeLogRepositoryMock
            .Setup(r => r.ListByOrganisationAsync(organisationId, 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChangeLogListItem>());

        var result = await _sut.ExecuteAsync(
            new ListOrganisationChangeLogsQuery(organisationId, null, null));

        result.IsError.Should().BeFalse();
        _changeLogRepositoryMock.Verify(
            r => r.ListByOrganisationAsync(organisationId, 0, 50, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(200)]
    public async Task ExecuteAsync_WhenTakeIsAtBoundary_ShouldPassThroughToRepository(int take)
    {
        var organisationId = Guid.NewGuid();
        _changeLogRepositoryMock
            .Setup(r => r.ListByOrganisationAsync(organisationId, 0, take, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChangeLogListItem>());

        var result = await _sut.ExecuteAsync(
            new ListOrganisationChangeLogsQuery(organisationId, null, take));

        result.IsError.Should().BeFalse();
        _changeLogRepositoryMock.Verify(
            r => r.ListByOrganisationAsync(organisationId, 0, take, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(201)]
    [InlineData(2000)]
    public async Task ExecuteAsync_WhenTakeIsOutOfRange_ShouldReturnValidationError_AndNotCallRepository(int take)
    {
        var organisationId = Guid.NewGuid();

        var result = await _sut.ExecuteAsync(
            new ListOrganisationChangeLogsQuery(organisationId, null, take));

        result.IsError.Should().BeTrue();
        result.Errors.Should().AllSatisfy(e => e.Type.Should().Be(ErrorType.Validation));
        result.Errors.Should().ContainSingle(e => e.Code == "Take");

        _changeLogRepositoryMock.Verify(
            r => r.ListByOrganisationAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSkipIsNegative_ShouldReturnValidationError_AndNotCallRepository()
    {
        var organisationId = Guid.NewGuid();

        var result = await _sut.ExecuteAsync(
            new ListOrganisationChangeLogsQuery(organisationId, -3, 50));

        result.IsError.Should().BeTrue();
        result.Errors.Should().AllSatisfy(e => e.Type.Should().Be(ErrorType.Validation));
        result.Errors.Should().ContainSingle(e => e.Code == "Skip");

        _changeLogRepositoryMock.Verify(
            r => r.ListByOrganisationAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEmptyList_WhenRepositoryReturnsNoItems()
    {
        var organisationId = Guid.NewGuid();
        _changeLogRepositoryMock
            .Setup(r => r.ListByOrganisationAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChangeLogListItem>());

        var result = await _sut.ExecuteAsync(
            new ListOrganisationChangeLogsQuery(organisationId, null, null));

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPassThroughItems_FromRepository()
    {
        var organisationId = Guid.NewGuid();
        var item = ValidItem(organisationId);
        _changeLogRepositoryMock
            .Setup(r => r.ListByOrganisationAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { item });

        var result = await _sut.ExecuteAsync(
            new ListOrganisationChangeLogsQuery(organisationId, null, null));

        result.IsError.Should().BeFalse();
        var items = result.Value;
        items.Should().HaveCount(1);
        items[0].Should().BeSameAs(item);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOrganisationIdIsEmpty_ShouldReturnValidationError_AndNotCallRepository()
    {
        var result = await _sut.ExecuteAsync(
            new ListOrganisationChangeLogsQuery(Guid.Empty, null, null));

        result.IsError.Should().BeTrue();
        result.Errors.Should().AllSatisfy(e => e.Type.Should().Be(ErrorType.Validation));
        result.Errors.Should().ContainSingle(e => e.Code == "OrganisationId");

        _changeLogRepositoryMock.Verify(
            r => r.ListByOrganisationAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldForwardCancellationToken()
    {
        var organisationId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        _changeLogRepositoryMock
            .Setup(r => r.ListByOrganisationAsync(organisationId, 0, 50, cts.Token))
            .ReturnsAsync(Array.Empty<ChangeLogListItem>());

        await _sut.ExecuteAsync(
            new ListOrganisationChangeLogsQuery(organisationId, null, null),
            cts.Token);

        _changeLogRepositoryMock.Verify(
            r => r.ListByOrganisationAsync(organisationId, 0, 50, cts.Token),
            Times.Once);
    }
}
