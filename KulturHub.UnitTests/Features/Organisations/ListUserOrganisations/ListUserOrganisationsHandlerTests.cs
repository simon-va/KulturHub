using ErrorOr;
using FluentAssertions;
using KulturHub.Application.Features.Organisations.ListUserOrganisations;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using Moq;

namespace KulturHub.UnitTests.Features.Organisations.ListUserOrganisations;

public class ListUserOrganisationsHandlerTests
{
    // Rules:
    // - ExecuteAsync forwards the userId to the repository.
    // - ExecuteAsync maps the returned entities to summaries containing only Id and Name.
    // - If the userId is Guid.Empty, a Validation error is returned and the repository is not called.
    // - The repository's CancellationToken is forwarded to the repository call.

    private readonly Mock<IOrganisationRepository> _organisationRepositoryMock = new();
    private readonly ListUserOrganisationsHandler _sut;

    public ListUserOrganisationsHandlerTests()
    {
        _sut = new ListUserOrganisationsHandler(_organisationRepositoryMock.Object);
    }

    private static Organisation ValidOrganisation(string name) =>
        Organisation.Reconstitute(Guid.NewGuid(), name, DateTime.UtcNow);

    [Fact]
    public async Task ExecuteAsync_ShouldPassUserIdToRepository()
    {
        var userId = Guid.NewGuid();
        _organisationRepositoryMock
            .Setup(r => r.ListByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Organisation>());

        await _sut.ExecuteAsync(userId);

        _organisationRepositoryMock.Verify(
            r => r.ListByUserIdAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEmptyList_WhenUserHasNoMemberships()
    {
        var userId = Guid.NewGuid();
        _organisationRepositoryMock
            .Setup(r => r.ListByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Organisation>());

        var result = await _sut.ExecuteAsync(userId);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapEntitiesToSummaries()
    {
        var org1 = ValidOrganisation("Acme Inc.");
        var org2 = ValidOrganisation("Beta GmbH");
        _organisationRepositoryMock
            .Setup(r => r.ListByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { org1, org2 });

        var result = await _sut.ExecuteAsync(Guid.NewGuid());

        result.IsError.Should().BeFalse();
        var summaries = result.Value;
        summaries.Should().HaveCount(2);
        summaries[0].Id.Should().Be(org1.Id);
        summaries[0].Name.Should().Be("Acme Inc.");
        summaries[1].Id.Should().Be(org2.Id);
        summaries[1].Name.Should().Be("Beta GmbH");
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserIdIsEmpty_ShouldReturnValidationError_AndNotCallRepository()
    {
        var result = await _sut.ExecuteAsync(Guid.Empty);

        result.IsError.Should().BeTrue();
        result.Errors.Should().AllSatisfy(e => e.Type.Should().Be(ErrorType.Validation));
        result.Errors.Should().ContainSingle(e => e.Code == "UserId");

        _organisationRepositoryMock.Verify(
            r => r.ListByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldForwardCancellationToken()
    {
        var userId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        _organisationRepositoryMock
            .Setup(r => r.ListByUserIdAsync(userId, cts.Token))
            .ReturnsAsync(Array.Empty<Organisation>());

        await _sut.ExecuteAsync(userId, cts.Token);

        _organisationRepositoryMock.Verify(
            r => r.ListByUserIdAsync(userId, cts.Token),
            Times.Once);
    }
}
