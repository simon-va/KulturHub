using ErrorOr;
using FluentAssertions;
using FluentValidation;
using KulturHub.Application.Errors;
using KulturHub.Application.Features.Memberships.RespondToMembership;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using KulturHub.UnitTests.Common;
using Microsoft.Extensions.Logging;
using Moq;

namespace KulturHub.UnitTests.Features.Memberships.RespondToMembership;

public class RespondToMembershipHandlerTests
{
    // Rules:
    // - FluentValidation: MembershipId and ActingUserId must be non-empty; Decision must be a defined enum value.
    // - If the membership does not exist, NotFound is returned and no transaction is opened.
    // - If the membership exists but belongs to a different user, NotInvitee (Forbidden) is returned and
    //   no UpdateStatus, no ChangeLog is written.
    // - If the membership is soft-deleted, AlreadyDeleted (Conflict) is returned; no UpdateStatus, no ChangeLog.
    // - If the membership is already Accepted or Rejected, AlreadyDecided (Conflict) is returned; no
    //   UpdateStatus, no ChangeLog.
    // - On Accept: a transaction is opened, UpdateStatusAsync is called with MembershipStatus.Accepted,
    //   a ChangeLog with message "Einladung angenommen" and data { userId, membershipId, status: "Accepted" }
    //   is inserted, the transaction is committed, and Success is returned.
    // - On Reject: same as Accept but with MembershipStatus.Rejected, message "Einladung abgelehnt",
    //   status: "Rejected".
    // - If UpdateStatusAsync returns 0 affected rows, AlreadyDecided is returned and no ChangeLog is written.
    // - If any repository call throws, RespondFailed is returned, the transaction is disposed and no
    //   ChangeLog is written.
    // - The CancellationToken is forwarded to the unit of work and all repository calls.

    private readonly Mock<IMembershipRepository> _membershipRepositoryMock = new();
    private readonly Mock<IChangeLogRepository> _changeLogRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILogger<RespondToMembershipHandler>> _loggerMock = new();
    private readonly IValidator<RespondToMembershipInput> _validator = new RespondToMembershipInputValidator();

    private readonly RespondToMembershipHandler _sut;
    private readonly FakeUnitOfWorkTransaction _fakeTransaction = new();

    public RespondToMembershipHandlerTests()
    {
        _sut = new RespondToMembershipHandler(
            _membershipRepositoryMock.Object,
            _changeLogRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _validator,
            _loggerMock.Object);

        _unitOfWorkMock
            .Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_fakeTransaction);
    }

    private static Membership ValidMembership(
        Guid membershipId,
        Guid userId,
        Guid organisationId,
        MembershipStatus status = MembershipStatus.Pending,
        bool isDeleted = false) =>
        Membership.Reconstitute(
            membershipId,
            userId,
            organisationId,
            DateTime.UtcNow.AddDays(-1),
            isDeleted: isDeleted,
            status: status);

    private static RespondToMembershipInput ValidInput(
        Guid? membershipId = null,
        Guid? actingUserId = null,
        MembershipDecision decision = MembershipDecision.Accept) =>
        new(
            membershipId ?? Guid.NewGuid(),
            actingUserId ?? Guid.NewGuid(),
            decision);

    [Fact]
    public async Task ExecuteAsync_WhenMembershipIdIsEmpty_ShouldReturnValidationError_AndNotOpenTransaction()
    {
        var input = new RespondToMembershipInput(Guid.Empty, Guid.NewGuid(), MembershipDecision.Accept);

        var result = await _sut.ExecuteAsync(input);

        result.IsError.Should().BeTrue();
        result.Errors.Should().AllSatisfy(e => e.Type.Should().Be(ErrorType.Validation));
        result.Errors.Should().ContainSingle(e => e.Code == "MembershipId");
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActingUserIdIsEmpty_ShouldReturnValidationError_AndNotOpenTransaction()
    {
        var input = new RespondToMembershipInput(Guid.NewGuid(), Guid.Empty, MembershipDecision.Accept);

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
            r => r.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<MembershipStatus>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _changeLogRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _fakeTransaction.CommitCount.Should().Be(0);
        _fakeTransaction.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMembershipBelongsToDifferentUser_ShouldReturnNotInvitee_AndNotUpdateStatus()
    {
        var input = ValidInput();
        var membership = ValidMembership(input.MembershipId, Guid.NewGuid(), Guid.NewGuid());
        _membershipRepositoryMock
            .Setup(r => r.GetByIdAsync(input.MembershipId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        var result = await _sut.ExecuteAsync(input);

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == MembershipErrors.NotInvitee.Code);
        result.Errors[0].Type.Should().Be(ErrorType.Forbidden);

        _membershipRepositoryMock.Verify(
            r => r.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<MembershipStatus>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _changeLogRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _fakeTransaction.CommitCount.Should().Be(0);
        _fakeTransaction.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMembershipIsSoftDeleted_ShouldReturnAlreadyDeleted_AndNotUpdateStatus()
    {
        var input = ValidInput();
        var membership = ValidMembership(input.MembershipId, input.ActingUserId, Guid.NewGuid(), isDeleted: true);
        _membershipRepositoryMock
            .Setup(r => r.GetByIdAsync(input.MembershipId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        var result = await _sut.ExecuteAsync(input);

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == MembershipErrors.AlreadyDeleted.Code);
        result.Errors[0].Type.Should().Be(ErrorType.Conflict);

        _membershipRepositoryMock.Verify(
            r => r.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<MembershipStatus>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _changeLogRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(MembershipStatus.Accepted)]
    [InlineData(MembershipStatus.Rejected)]
    public async Task ExecuteAsync_WhenMembershipIsAlreadyDecided_ShouldReturnAlreadyDecided_AndNotUpdateStatus(MembershipStatus currentStatus)
    {
        var input = ValidInput();
        var membership = ValidMembership(input.MembershipId, input.ActingUserId, Guid.NewGuid(), status: currentStatus);
        _membershipRepositoryMock
            .Setup(r => r.GetByIdAsync(input.MembershipId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        var result = await _sut.ExecuteAsync(input);

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == MembershipErrors.AlreadyDecided.Code);
        result.Errors[0].Type.Should().Be(ErrorType.Conflict);

        _membershipRepositoryMock.Verify(
            r => r.UpdateStatusAsync(It.IsAny<Guid>(), It.IsAny<MembershipStatus>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _changeLogRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _fakeTransaction.CommitCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAcceptingPendingMembership_ShouldUpdateStatusToAccepted_AndWriteChangeLog_AndCommit()
    {
        var input = ValidInput(decision: MembershipDecision.Accept);
        var membership = ValidMembership(input.MembershipId, input.ActingUserId, Guid.NewGuid());
        membership.Status.Should().Be(MembershipStatus.Pending);

        _membershipRepositoryMock
            .Setup(r => r.GetByIdAsync(input.MembershipId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
        _membershipRepositoryMock
            .Setup(r => r.UpdateStatusAsync(membership.Id, MembershipStatus.Accepted, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _changeLogRepositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.ExecuteAsync(input);

        result.IsError.Should().BeFalse();
        result.Value.Should().Be(Result.Success);
        membership.Status.Should().Be(MembershipStatus.Accepted);

        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _membershipRepositoryMock.Verify(
            r => r.UpdateStatusAsync(membership.Id, MembershipStatus.Accepted, _fakeTransaction, It.IsAny<CancellationToken>()),
            Times.Once);
        _changeLogRepositoryMock.Verify(
            r => r.InsertAsync(
                It.Is<ChangeLog>(c =>
                    c.OrganisationId == membership.OrganisationId
                    && c.UserId == input.ActingUserId
                    && c.Message == "Einladung angenommen"
                    && c.Data.ContainsKey("userId")
                    && (Guid)c.Data["userId"]! == membership.UserId
                    && c.Data.ContainsKey("membershipId")
                    && (Guid)c.Data["membershipId"]! == membership.Id
                    && c.Data.ContainsKey("status")
                    && (string)c.Data["status"]! == "Accepted"),
                _fakeTransaction,
                It.IsAny<CancellationToken>()),
            Times.Once);
        _fakeTransaction.CommitCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRejectingPendingMembership_ShouldUpdateStatusToRejected_AndWriteChangeLog_AndCommit()
    {
        var input = ValidInput(decision: MembershipDecision.Reject);
        var membership = ValidMembership(input.MembershipId, input.ActingUserId, Guid.NewGuid());
        membership.Status.Should().Be(MembershipStatus.Pending);

        _membershipRepositoryMock
            .Setup(r => r.GetByIdAsync(input.MembershipId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
        _membershipRepositoryMock
            .Setup(r => r.UpdateStatusAsync(membership.Id, MembershipStatus.Rejected, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _changeLogRepositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.ExecuteAsync(input);

        result.IsError.Should().BeFalse();
        result.Value.Should().Be(Result.Success);
        membership.Status.Should().Be(MembershipStatus.Rejected);

        _membershipRepositoryMock.Verify(
            r => r.UpdateStatusAsync(membership.Id, MembershipStatus.Rejected, _fakeTransaction, It.IsAny<CancellationToken>()),
            Times.Once);
        _changeLogRepositoryMock.Verify(
            r => r.InsertAsync(
                It.Is<ChangeLog>(c =>
                    c.Message == "Einladung abgelehnt"
                    && c.Data.ContainsKey("status")
                    && (string)c.Data["status"]! == "Rejected"),
                _fakeTransaction,
                It.IsAny<CancellationToken>()),
            Times.Once);
        _fakeTransaction.CommitCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUpdateStatusReturnsZeroAffectedRows_ShouldReturnAlreadyDecided_AndNotWriteChangeLog()
    {
        var input = ValidInput();
        var membership = ValidMembership(input.MembershipId, input.ActingUserId, Guid.NewGuid());

        _membershipRepositoryMock
            .Setup(r => r.GetByIdAsync(input.MembershipId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
        _membershipRepositoryMock
            .Setup(r => r.UpdateStatusAsync(membership.Id, MembershipStatus.Accepted, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _sut.ExecuteAsync(input);

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == MembershipErrors.AlreadyDecided.Code);
        _changeLogRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _fakeTransaction.CommitCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUpdateStatusThrows_ShouldReturnRespondFailed_AndNotWriteChangeLog()
    {
        var input = ValidInput();
        var membership = ValidMembership(input.MembershipId, input.ActingUserId, Guid.NewGuid());

        _membershipRepositoryMock
            .Setup(r => r.GetByIdAsync(input.MembershipId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
        _membershipRepositoryMock
            .Setup(r => r.UpdateStatusAsync(membership.Id, MembershipStatus.Accepted, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("connection lost"));

        var result = await _sut.ExecuteAsync(input);

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Code.Should().StartWith("Membership.RespondFailed");
        result.Errors[0].Description.Should().Contain("connection lost");

        _changeLogRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _fakeTransaction.CommitCount.Should().Be(0);
        _fakeTransaction.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenChangeLogThrows_ShouldReturnRespondFailed_AndDisposeTransaction()
    {
        var input = ValidInput();
        var membership = ValidMembership(input.MembershipId, input.ActingUserId, Guid.NewGuid());

        _membershipRepositoryMock
            .Setup(r => r.GetByIdAsync(input.MembershipId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
        _membershipRepositoryMock
            .Setup(r => r.UpdateStatusAsync(membership.Id, MembershipStatus.Accepted, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _changeLogRepositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("change log write failed"));

        var result = await _sut.ExecuteAsync(input);

        result.IsError.Should().BeTrue();
        result.Errors[0].Code.Should().StartWith("Membership.RespondFailed");
        result.Errors[0].Description.Should().Contain("change log write failed");

        _fakeTransaction.CommitCount.Should().Be(0);
        _fakeTransaction.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldForwardCancellationToken()
    {
        var input = ValidInput(decision: MembershipDecision.Reject);
        var membership = ValidMembership(input.MembershipId, input.ActingUserId, Guid.NewGuid());
        using var cts = new CancellationTokenSource();

        _membershipRepositoryMock
            .Setup(r => r.GetByIdAsync(input.MembershipId, It.IsAny<IUnitOfWorkTransaction?>(), cts.Token))
            .ReturnsAsync(membership);
        _membershipRepositoryMock
            .Setup(r => r.UpdateStatusAsync(membership.Id, MembershipStatus.Rejected, It.IsAny<IUnitOfWorkTransaction?>(), cts.Token))
            .ReturnsAsync(1);
        _changeLogRepositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), cts.Token))
            .Returns(Task.CompletedTask);

        await _sut.ExecuteAsync(input, cts.Token);

        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(cts.Token), Times.Once);
        _membershipRepositoryMock.Verify(
            r => r.GetByIdAsync(input.MembershipId, _fakeTransaction, cts.Token),
            Times.Once);
        _membershipRepositoryMock.Verify(
            r => r.UpdateStatusAsync(membership.Id, MembershipStatus.Rejected, _fakeTransaction, cts.Token),
            Times.Once);
        _changeLogRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<ChangeLog>(), _fakeTransaction, cts.Token),
            Times.Once);
    }
}
