using ErrorOr;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using KulturHub.Application.Errors;
using KulturHub.Application.Features.Memberships.DeleteOrganisationMembership;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using KulturHub.UnitTests.Common;
using Microsoft.Extensions.Logging;
using Moq;

namespace KulturHub.UnitTests.Features.Memberships.DeleteOrganisationMembership;

public class DeleteOrganisationMembershipHandlerTests
{
    // Rules:
    // - ExecuteAsync returns NotFound when the membership does not exist; no SoftDelete, no ChangeLog,
    //   transaction is disposed (rolled back).
    // - ExecuteAsync returns NotFound when the membership exists but belongs to a different organisation.
    // - ExecuteAsync returns AlreadyDeleted (Conflict) when the membership is already soft-deleted;
    //   no SoftDelete, no ChangeLog.
    // - ExecuteAsync returns LastMember (Conflict) when the membership is the only active member of the
    //   organisation; no SoftDelete, no ChangeLog, transaction is disposed.
    // - On success a transaction is opened, the membership is shadow-deleted (IsDeleted + DeletedAt set),
    //   SoftDeleteAsync is called with the transaction, a ChangeLog with message "Member entfernt" and
    //   data { userId, membershipId } is inserted, and the transaction is committed.
    // - If SoftDeleteAsync returns 0 affected rows, AlreadyDeleted is returned and no ChangeLog is written.
    // - If any repository throws, DeleteFailed is returned and the transaction is disposed (rolled back);
    //   no ChangeLog is written.
    // - The CancellationToken is forwarded to the unit of work and all repository calls.

    private readonly Mock<IMembershipRepository> _membershipRepositoryMock = new();
    private readonly Mock<IChangeLogRepository> _changeLogRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IValidator<DeleteOrganisationMembershipInput>> _validatorMock = new();
    private readonly Mock<ILogger<DeleteOrganisationMembershipHandler>> _loggerMock = new();

    private readonly DeleteOrganisationMembershipHandler _sut;
    private readonly FakeUnitOfWorkTransaction _fakeTransaction = new();

    public DeleteOrganisationMembershipHandlerTests()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<DeleteOrganisationMembershipInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _sut = new DeleteOrganisationMembershipHandler(
            _membershipRepositoryMock.Object,
            _changeLogRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _validatorMock.Object,
            _loggerMock.Object);

        _unitOfWorkMock
            .Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_fakeTransaction);
    }

    private static Membership ValidMembership(Guid organisationId, bool isDeleted = false) =>
        Membership.Reconstitute(
            Guid.NewGuid(),
            Guid.NewGuid(),
            organisationId,
            DateTime.UtcNow.AddDays(-1),
            isDeleted: isDeleted);

    private static DeleteOrganisationMembershipInput ValidInput(Guid? organisationId = null, Guid? membershipId = null) =>
        new(
            organisationId ?? Guid.NewGuid(),
            membershipId ?? Guid.NewGuid(),
            Guid.NewGuid());

    [Fact]
    public async Task ExecuteAsync_WhenOrganisationIdIsEmpty_ShouldReturnValidationError_AndNotOpenTransaction()
    {
        var input = new DeleteOrganisationMembershipInput(Guid.Empty, Guid.NewGuid(), Guid.NewGuid());
        _validatorMock
            .Setup(v => v.ValidateAsync(input, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[]
            {
                new ValidationFailure("OrganisationId", "OrganisationId is required."),
            }));

        var result = await _sut.ExecuteAsync(input);

        result.IsError.Should().BeTrue();
        result.Errors.Should().AllSatisfy(e => e.Type.Should().Be(ErrorType.Validation));
        result.Errors.Should().ContainSingle(e => e.Code == "OrganisationId");
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMembershipIdIsEmpty_ShouldReturnValidationError_AndNotOpenTransaction()
    {
        var input = new DeleteOrganisationMembershipInput(Guid.NewGuid(), Guid.Empty, Guid.NewGuid());
        _validatorMock
            .Setup(v => v.ValidateAsync(input, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[]
            {
                new ValidationFailure("MembershipId", "MembershipId is required."),
            }));

        var result = await _sut.ExecuteAsync(input);

        result.IsError.Should().BeTrue();
        result.Errors.Should().AllSatisfy(e => e.Type.Should().Be(ErrorType.Validation));
        result.Errors.Should().ContainSingle(e => e.Code == "MembershipId");
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActingUserIdIsEmpty_ShouldReturnValidationError_AndNotOpenTransaction()
    {
        var input = new DeleteOrganisationMembershipInput(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty);
        _validatorMock
            .Setup(v => v.ValidateAsync(input, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[]
            {
                new ValidationFailure("ActingUserId", "ActingUserId is required."),
            }));

        var result = await _sut.ExecuteAsync(input);

        result.IsError.Should().BeTrue();
        result.Errors.Should().AllSatisfy(e => e.Type.Should().Be(ErrorType.Validation));
        result.Errors.Should().ContainSingle(e => e.Code == "ActingUserId");
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMembershipDoesNotExist_ShouldReturnNotFound_AndDisposeTransaction()
    {
        var input = ValidInput();
        _membershipRepositoryMock
            .Setup(r => r.GetByIdAsync(input.MembershipId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Membership?)null);

        var result = await _sut.ExecuteAsync(input);

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == MembershipErrors.NotFound.Code);
        result.Errors[0].Type.Should().Be(ErrorType.NotFound);

        _membershipRepositoryMock.Verify(
            r => r.CountActiveByOrganisationAsync(It.IsAny<Guid>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _membershipRepositoryMock.Verify(
            r => r.SoftDeleteAsync(It.IsAny<Guid>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _changeLogRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _fakeTransaction.CommitCount.Should().Be(0);
        _fakeTransaction.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMembershipBelongsToDifferentOrganisation_ShouldReturnNotFound()
    {
        var input = ValidInput();
        var membership = ValidMembership(Guid.NewGuid());
        _membershipRepositoryMock
            .Setup(r => r.GetByIdAsync(input.MembershipId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        var result = await _sut.ExecuteAsync(input);

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == MembershipErrors.NotFound.Code);
        _membershipRepositoryMock.Verify(
            r => r.SoftDeleteAsync(It.IsAny<Guid>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMembershipAlreadyDeleted_ShouldReturnAlreadyDeleted_AndNotDeleteAgain()
    {
        var input = ValidInput();
        var membership = ValidMembership(input.OrganisationId, isDeleted: true);
        _membershipRepositoryMock
            .Setup(r => r.GetByIdAsync(input.MembershipId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        var result = await _sut.ExecuteAsync(input);

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == MembershipErrors.AlreadyDeleted.Code);
        result.Errors[0].Type.Should().Be(ErrorType.Conflict);

        _membershipRepositoryMock.Verify(
            r => r.CountActiveByOrganisationAsync(It.IsAny<Guid>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _membershipRepositoryMock.Verify(
            r => r.SoftDeleteAsync(It.IsAny<Guid>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _changeLogRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _fakeTransaction.CommitCount.Should().Be(0);
        _fakeTransaction.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMembershipIsTheOnlyActiveMember_ShouldReturnLastMember_AndNotDelete()
    {
        var input = ValidInput();
        var membership = ValidMembership(input.OrganisationId);
        _membershipRepositoryMock
            .Setup(r => r.GetByIdAsync(input.MembershipId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
        _membershipRepositoryMock
            .Setup(r => r.CountActiveByOrganisationAsync(input.OrganisationId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _sut.ExecuteAsync(input);

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == MembershipErrors.LastMember.Code);
        result.Errors[0].Type.Should().Be(ErrorType.Conflict);

        _membershipRepositoryMock.Verify(
            r => r.SoftDeleteAsync(It.IsAny<Guid>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _changeLogRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _fakeTransaction.CommitCount.Should().Be(0);
        _fakeTransaction.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOrganisationHasZeroActiveMembers_ShouldReturnLastMember_AndNotDelete()
    {
        var input = ValidInput();
        var membership = ValidMembership(input.OrganisationId);
        _membershipRepositoryMock
            .Setup(r => r.GetByIdAsync(input.MembershipId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
        _membershipRepositoryMock
            .Setup(r => r.CountActiveByOrganisationAsync(input.OrganisationId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _sut.ExecuteAsync(input);

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == MembershipErrors.LastMember.Code);
        _membershipRepositoryMock.Verify(
            r => r.SoftDeleteAsync(It.IsAny<Guid>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAllInputsAreValid_ShouldReturnDeleted_ShadowDeleteMembership_AndWriteChangeLog()
    {
        var input = ValidInput();
        var membership = ValidMembership(input.OrganisationId);
        membership.IsDeleted.Should().BeFalse();
        membership.DeletedAt.Should().BeNull();

        _membershipRepositoryMock
            .Setup(r => r.GetByIdAsync(input.MembershipId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
        _membershipRepositoryMock
            .Setup(r => r.CountActiveByOrganisationAsync(input.OrganisationId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _membershipRepositoryMock
            .Setup(r => r.SoftDeleteAsync(membership.Id, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _changeLogRepositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.ExecuteAsync(input);

        result.IsError.Should().BeFalse();
        result.Value.Should().Be(Result.Deleted);

        membership.IsDeleted.Should().BeTrue();
        membership.DeletedAt.Should().NotBeNull();

        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _membershipRepositoryMock.Verify(
            r => r.SoftDeleteAsync(membership.Id, _fakeTransaction, It.IsAny<CancellationToken>()),
            Times.Once);
        _changeLogRepositoryMock.Verify(
            r => r.InsertAsync(
                It.Is<ChangeLog>(c =>
                    c.OrganisationId == input.OrganisationId
                    && c.UserId == input.ActingUserId
                    && c.Message == "Member entfernt"
                    && c.Data.ContainsKey("userId")
                    && (Guid)c.Data["userId"]! == membership.UserId
                    && c.Data.ContainsKey("membershipId")
                    && (Guid)c.Data["membershipId"]! == membership.Id),
                _fakeTransaction,
                It.IsAny<CancellationToken>()),
            Times.Once);
        _fakeTransaction.CommitCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSoftDeleteReturnsZeroAffectedRows_ShouldReturnAlreadyDeleted_AndNotWriteChangeLog()
    {
        var input = ValidInput();
        var membership = ValidMembership(input.OrganisationId);
        _membershipRepositoryMock
            .Setup(r => r.GetByIdAsync(input.MembershipId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
        _membershipRepositoryMock
            .Setup(r => r.CountActiveByOrganisationAsync(input.OrganisationId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _membershipRepositoryMock
            .Setup(r => r.SoftDeleteAsync(membership.Id, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _sut.ExecuteAsync(input);

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == MembershipErrors.AlreadyDeleted.Code);
        _changeLogRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _fakeTransaction.CommitCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSoftDeleteThrows_ShouldReturnDeleteFailed_AndNotWriteChangeLog()
    {
        var input = ValidInput();
        var membership = ValidMembership(input.OrganisationId);
        _membershipRepositoryMock
            .Setup(r => r.GetByIdAsync(input.MembershipId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
        _membershipRepositoryMock
            .Setup(r => r.CountActiveByOrganisationAsync(input.OrganisationId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _membershipRepositoryMock
            .Setup(r => r.SoftDeleteAsync(membership.Id, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("connection lost"));

        var result = await _sut.ExecuteAsync(input);

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Code.Should().StartWith("Membership.DeleteFailed");
        result.Errors[0].Description.Should().Contain("connection lost");

        _changeLogRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _fakeTransaction.CommitCount.Should().Be(0);
        _fakeTransaction.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenChangeLogThrows_ShouldReturnDeleteFailed_AndDisposeTransaction()
    {
        var input = ValidInput();
        var membership = ValidMembership(input.OrganisationId);
        _membershipRepositoryMock
            .Setup(r => r.GetByIdAsync(input.MembershipId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
        _membershipRepositoryMock
            .Setup(r => r.CountActiveByOrganisationAsync(input.OrganisationId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _membershipRepositoryMock
            .Setup(r => r.SoftDeleteAsync(membership.Id, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _changeLogRepositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("change log write failed"));

        var result = await _sut.ExecuteAsync(input);

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Code.Should().StartWith("Membership.DeleteFailed");
        result.Errors[0].Description.Should().Contain("change log write failed");

        _fakeTransaction.CommitCount.Should().Be(0);
        _fakeTransaction.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldForwardCancellationToken()
    {
        var input = ValidInput();
        var membership = ValidMembership(input.OrganisationId);
        using var cts = new CancellationTokenSource();

        _membershipRepositoryMock
            .Setup(r => r.GetByIdAsync(input.MembershipId, It.IsAny<IUnitOfWorkTransaction?>(), cts.Token))
            .ReturnsAsync(membership);
        _membershipRepositoryMock
            .Setup(r => r.CountActiveByOrganisationAsync(input.OrganisationId, It.IsAny<IUnitOfWorkTransaction?>(), cts.Token))
            .ReturnsAsync(2);
        _membershipRepositoryMock
            .Setup(r => r.SoftDeleteAsync(membership.Id, It.IsAny<IUnitOfWorkTransaction?>(), cts.Token))
            .ReturnsAsync(1);
        _changeLogRepositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), cts.Token))
            .Returns(Task.CompletedTask);

        await _sut.ExecuteAsync(input, cts.Token);

        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(cts.Token), Times.Once);
        _membershipRepositoryMock.Verify(
            r => r.GetByIdAsync(input.MembershipId, It.IsAny<IUnitOfWorkTransaction?>(), cts.Token),
            Times.Once);
        _membershipRepositoryMock.Verify(
            r => r.CountActiveByOrganisationAsync(input.OrganisationId, It.IsAny<IUnitOfWorkTransaction?>(), cts.Token),
            Times.Once);
        _membershipRepositoryMock.Verify(
            r => r.SoftDeleteAsync(membership.Id, It.IsAny<IUnitOfWorkTransaction?>(), cts.Token),
            Times.Once);
        _changeLogRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), cts.Token),
            Times.Once);
    }
}
