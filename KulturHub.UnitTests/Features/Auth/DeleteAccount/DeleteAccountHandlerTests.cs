using ErrorOr;
using FluentAssertions;
using KulturHub.Application.Errors;
using KulturHub.Application.Features.Auth.DeleteAccount;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace KulturHub.UnitTests.Features.Auth.DeleteAccount;

public class DeleteAccountHandlerTests
{
    // Rules:
    // - ExecuteAsync returns NotFound when the user does not exist in the repository.
    // - ExecuteAsync does not call Supabase when the user is not found.
    // - ExecuteAsync returns DeleteProviderFailed (Failure) when Supabase rejects the delete.
    // - ExecuteAsync does not call the repository's DeleteAsync when Supabase rejects the delete.
    // - ExecuteAsync returns Result.Deleted and calls both ports on success.
    // - ExecuteAsync returns Result.Deleted (and logs a warning) when Supabase succeeds but the repository reports 0 affected rows.
    // - ExecuteAsync marks the domain entity as deleted (shadow delete) before calling the repository.
    // - ExecuteAsync returns Conflict (SoleMemberOfOrganisations) when the user is the sole active member of
    //   one or more organisations; in that case Supabase and the repository's DeleteAsync are not called.
    // - The Conflict error's description contains all organisation ids for which the user is the sole active member.

    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IMembershipRepository> _membershipRepositoryMock = new();
    private readonly Mock<IUserAdminClient> _userAdminClientMock = new();
    private readonly ILogger<DeleteAccountHandler> _logger = NullLogger<DeleteAccountHandler>.Instance;
    private readonly DeleteAccountHandler _sut;

    public DeleteAccountHandlerTests()
    {
        _sut = new DeleteAccountHandler(
            _userRepositoryMock.Object,
            _membershipRepositoryMock.Object,
            _userAdminClientMock.Object,
            _logger);
    }

    private static User ValidUser() =>
        User.Reconstitute(Guid.NewGuid(), "ada@example.com", "Ada", "Lovelace");

    private void SetupNoSoleMemberOrganisations(Guid userId)
    {
        _membershipRepositoryMock
            .Setup(r => r.GetActiveOrganisationIdsWhereUserIsSoleMemberAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserDoesNotExist_ShouldReturnNotFound_AndNotCallSupabase()
    {
        var userId = Guid.NewGuid();
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _sut.ExecuteAsync(userId);

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == AuthErrors.NotFound.Code);
        result.Errors[0].Type.Should().Be(ErrorType.NotFound);
        _membershipRepositoryMock.Verify(
            r => r.GetActiveOrganisationIdsWhereUserIsSoleMemberAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _userAdminClientMock.Verify(
            c => c.DeleteUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _userRepositoryMock.Verify(
            r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSupabaseDeleteFails_ShouldReturnDeleteProviderFailed_AndNotDeleteFromRepository()
    {
        var user = ValidUser();
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(user.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        SetupNoSoleMemberOrganisations(user.UserId);
        _userAdminClientMock
            .Setup(c => c.DeleteUserAsync(user.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.ExecuteAsync(user.UserId);

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == AuthErrors.DeleteProviderFailed.Code);
        result.Errors[0].Type.Should().Be(ErrorType.Failure);
        _userRepositoryMock.Verify(
            r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenBothSucceed_ShouldReturnDeleted_AndCallBothPorts()
    {
        var user = ValidUser();
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(user.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        SetupNoSoleMemberOrganisations(user.UserId);
        _userAdminClientMock
            .Setup(c => c.DeleteUserAsync(user.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _userRepositoryMock
            .Setup(r => r.DeleteAsync(user.UserId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _sut.ExecuteAsync(user.UserId);

        result.IsError.Should().BeFalse();
        result.Value.Should().Be(Result.Deleted);
        _userAdminClientMock.Verify(
            c => c.DeleteUserAsync(user.UserId, It.IsAny<CancellationToken>()),
            Times.Once);
        _userRepositoryMock.Verify(
            r => r.DeleteAsync(user.UserId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryReturnsZeroAffectedRows_ShouldReturnDeleted()
    {
        var user = ValidUser();
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(user.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        SetupNoSoleMemberOrganisations(user.UserId);
        _userAdminClientMock
            .Setup(c => c.DeleteUserAsync(user.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _userRepositoryMock
            .Setup(r => r.DeleteAsync(user.UserId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _sut.ExecuteAsync(user.UserId);

        result.IsError.Should().BeFalse();
        result.Value.Should().Be(Result.Deleted);
    }

    [Fact]
    public async Task ExecuteAsync_OnSuccess_ShouldShadowDeleteTheDomainEntity()
    {
        var user = ValidUser();
        user.IsDeleted.Should().BeFalse();
        user.DeletedAt.Should().BeNull();

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(user.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        SetupNoSoleMemberOrganisations(user.UserId);
        _userAdminClientMock
            .Setup(c => c.DeleteUserAsync(user.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _userRepositoryMock
            .Setup(r => r.DeleteAsync(user.UserId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        await _sut.ExecuteAsync(user.UserId);

        user.IsDeleted.Should().BeTrue();
        user.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserIsSoleActiveMemberOfOneOrganisation_ShouldReturnConflict_AndNotCallSupabase()
    {
        var user = ValidUser();
        var soleOrganisationId = Guid.NewGuid();
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(user.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _membershipRepositoryMock
            .Setup(r => r.GetActiveOrganisationIdsWhereUserIsSoleMemberAsync(user.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { soleOrganisationId });

        var result = await _sut.ExecuteAsync(user.UserId);

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == AuthErrors.SoleMemberOfOrganisations(Array.Empty<Guid>()).Code);
        result.Errors[0].Type.Should().Be(ErrorType.Conflict);
        result.Errors[0].Description.Should().Contain(soleOrganisationId.ToString());
        _userAdminClientMock.Verify(
            c => c.DeleteUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _userRepositoryMock.Verify(
            r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserIsSoleActiveMemberOfMultipleOrganisations_ShouldListAllOrganisationIdsInDescription()
    {
        var user = ValidUser();
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(user.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _membershipRepositoryMock
            .Setup(r => r.GetActiveOrganisationIdsWhereUserIsSoleMemberAsync(user.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { orgA, orgB });

        var result = await _sut.ExecuteAsync(user.UserId);

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Type.Should().Be(ErrorType.Conflict);
        result.Errors[0].Description.Should().Contain(orgA.ToString());
        result.Errors[0].Description.Should().Contain(orgB.ToString());
        _userAdminClientMock.Verify(
            c => c.DeleteUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _userRepositoryMock.Verify(
            r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserIsNotSoleMemberAnywhere_ShouldProceedNormally()
    {
        var user = ValidUser();
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(user.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        SetupNoSoleMemberOrganisations(user.UserId);
        _userAdminClientMock
            .Setup(c => c.DeleteUserAsync(user.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _userRepositoryMock
            .Setup(r => r.DeleteAsync(user.UserId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _sut.ExecuteAsync(user.UserId);

        result.IsError.Should().BeFalse();
        result.Value.Should().Be(Result.Deleted);
        _membershipRepositoryMock.Verify(
            r => r.GetActiveOrganisationIdsWhereUserIsSoleMemberAsync(user.UserId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
