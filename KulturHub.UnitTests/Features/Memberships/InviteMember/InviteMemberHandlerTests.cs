using ErrorOr;
using FluentAssertions;
using FluentValidation;
using KulturHub.Application.Errors;
using KulturHub.Application.Features.Memberships.InviteMember;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using KulturHub.UnitTests.Common;
using Microsoft.Extensions.Logging;
using Moq;

namespace KulturHub.UnitTests.Features.Memberships.InviteMember;

public class InviteMemberHandlerTests
{
    // Rules:
    // - FluentValidation: OrganisationId, ActingUserId, Email (NotEmpty + EmailAddress).
    // - If the user does not exist, UserNotFound is returned and no transaction is opened.
    // - If the acting user invites themselves, SelfInvite (Validation) is returned and no transaction is opened.
    // - If an active membership for (invitedUser, organisation) already exists, AlreadyInvited (Conflict)
    //   is returned; the transaction is disposed and no insert / change log is written.
    // - On success a transaction is opened, a Pending membership is inserted, a ChangeLog with message
    //   "Member eingeladen" and data { userId, membershipId, email } is written, and the transaction is
    //   committed. The response carries the new membership id, userId, email and Pending status.
    // - If any repository call throws, InviteFailed is returned, the transaction is disposed, and no
    //   change log (or insert) is left half-written.
    // - The CancellationToken is forwarded to the unit of work and all repository calls.

    private readonly Mock<IMembershipRepository> _membershipRepositoryMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IChangeLogRepository> _changeLogRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILogger<InviteMemberHandler>> _loggerMock = new();
    private readonly IValidator<InviteMemberInput> _validator = new InviteMemberInputValidator();

    private readonly InviteMemberHandler _sut;
    private readonly FakeUnitOfWorkTransaction _fakeTransaction = new();

    public InviteMemberHandlerTests()
    {
        _sut = new InviteMemberHandler(
            _membershipRepositoryMock.Object,
            _userRepositoryMock.Object,
            _changeLogRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _validator,
            _loggerMock.Object);

        _unitOfWorkMock
            .Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_fakeTransaction);
    }

    private static InviteMemberInput ValidInput(
        Guid? organisationId = null,
        string email = "anna@example.com",
        Guid? actingUserId = null) =>
        new(
            organisationId ?? Guid.NewGuid(),
            email,
            actingUserId ?? Guid.NewGuid());

    private static User ValidUser(Guid? userId = null, string email = "anna@example.com") =>
        User.Reconstitute(userId ?? Guid.NewGuid(), email, "Anna", "Müller");

    [Fact]
    public async Task ExecuteAsync_WhenOrganisationIdIsEmpty_ShouldReturnValidationError_AndNotOpenTransaction()
    {
        var input = new InviteMemberInput(Guid.Empty, "anna@example.com", Guid.NewGuid());

        var result = await _sut.ExecuteAsync(input);

        result.IsError.Should().BeTrue();
        result.Errors.Should().AllSatisfy(e => e.Type.Should().Be(ErrorType.Validation));
        result.Errors.Should().ContainSingle(e => e.Code == "OrganisationId");
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _userRepositoryMock.Verify(
            r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActingUserIdIsEmpty_ShouldReturnValidationError_AndNotOpenTransaction()
    {
        var input = new InviteMemberInput(Guid.NewGuid(), "anna@example.com", Guid.Empty);

        var result = await _sut.ExecuteAsync(input);

        result.IsError.Should().BeTrue();
        result.Errors.Should().AllSatisfy(e => e.Type.Should().Be(ErrorType.Validation));
        result.Errors.Should().ContainSingle(e => e.Code == "ActingUserId");
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenEmailIsEmpty_ShouldReturnValidationError_AndNotOpenTransaction()
    {
        var input = new InviteMemberInput(Guid.NewGuid(), string.Empty, Guid.NewGuid());

        var result = await _sut.ExecuteAsync(input);

        result.IsError.Should().BeTrue();
        result.Errors.Should().AllSatisfy(e => e.Type.Should().Be(ErrorType.Validation));
        result.Errors.Should().Contain(e => e.Code == "Email");
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenEmailIsInvalid_ShouldReturnValidationError_AndNotOpenTransaction()
    {
        var input = new InviteMemberInput(Guid.NewGuid(), "not-an-email", Guid.NewGuid());

        var result = await _sut.ExecuteAsync(input);

        result.IsError.Should().BeTrue();
        result.Errors.Should().AllSatisfy(e => e.Type.Should().Be(ErrorType.Validation));
        result.Errors.Should().Contain(e => e.Code == "Email");
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserDoesNotExist_ShouldReturnUserNotFound_AndNotOpenTransaction()
    {
        var input = ValidInput();
        _userRepositoryMock
            .Setup(r => r.GetByEmailAsync(input.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _sut.ExecuteAsync(input);

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == MembershipErrors.UserNotFound.Code);
        result.Errors[0].Type.Should().Be(ErrorType.NotFound);
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActingUserInvitesThemselves_ShouldReturnSelfInvite_AndNotOpenTransaction()
    {
        var userId = Guid.NewGuid();
        var input = ValidInput(actingUserId: userId);
        _userRepositoryMock
            .Setup(r => r.GetByEmailAsync(input.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidUser(userId, input.Email));

        var result = await _sut.ExecuteAsync(input);

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == MembershipErrors.SelfInvite.Code);
        result.Errors[0].Type.Should().Be(ErrorType.Validation);
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActiveMembershipAlreadyExists_ShouldReturnAlreadyInvited_AndDisposeTransaction()
    {
        var input = ValidInput();
        var user = ValidUser(email: input.Email);
        var existing = Membership.Reconstitute(
            Guid.NewGuid(),
            user.UserId,
            input.OrganisationId,
            DateTime.UtcNow.AddDays(-1),
            status: MembershipStatus.Pending);

        _userRepositoryMock
            .Setup(r => r.GetByEmailAsync(input.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _membershipRepositoryMock
            .Setup(r => r.GetActiveByUserAndOrganisationAsync(
                user.UserId, input.OrganisationId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await _sut.ExecuteAsync(input);

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == MembershipErrors.AlreadyInvited.Code);
        result.Errors[0].Type.Should().Be(ErrorType.Conflict);

        _membershipRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<Membership>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _changeLogRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _fakeTransaction.CommitCount.Should().Be(0);
        _fakeTransaction.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAllInputsAreValid_ShouldInsertPendingMembership_AndWriteChangeLog_AndCommit()
    {
        var input = ValidInput();
        var user = ValidUser(email: input.Email);

        _userRepositoryMock
            .Setup(r => r.GetByEmailAsync(input.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _membershipRepositoryMock
            .Setup(r => r.GetActiveByUserAndOrganisationAsync(
                user.UserId, input.OrganisationId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Membership?)null);
        _membershipRepositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<Membership>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _changeLogRepositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.ExecuteAsync(input);

        result.IsError.Should().BeFalse();
        result.Value.MembershipId.Should().NotBe(Guid.Empty);
        result.Value.UserId.Should().Be(user.UserId);
        result.Value.Email.Should().Be(user.Email);
        result.Value.Status.Should().Be(MembershipStatus.Pending);

        _membershipRepositoryMock.Verify(
            r => r.InsertAsync(
                It.Is<Membership>(m =>
                    m.UserId == user.UserId
                    && m.OrganisationId == input.OrganisationId
                    && m.Status == MembershipStatus.Pending),
                _fakeTransaction,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _changeLogRepositoryMock.Verify(
            r => r.InsertAsync(
                It.Is<ChangeLog>(c =>
                    c.OrganisationId == input.OrganisationId
                    && c.UserId == input.ActingUserId
                    && c.Message == "Member eingeladen"
                    && c.Data.ContainsKey("userId")
                    && (Guid)c.Data["userId"]! == user.UserId
                    && c.Data.ContainsKey("email")
                    && (string)c.Data["email"]! == user.Email
                    && c.Data.ContainsKey("membershipId")),
                _fakeTransaction,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _fakeTransaction.CommitCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInsertThrows_ShouldReturnInviteFailed_AndNotWriteChangeLog()
    {
        var input = ValidInput();
        var user = ValidUser(email: input.Email);

        _userRepositoryMock
            .Setup(r => r.GetByEmailAsync(input.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _membershipRepositoryMock
            .Setup(r => r.GetActiveByUserAndOrganisationAsync(
                user.UserId, input.OrganisationId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Membership?)null);
        _membershipRepositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<Membership>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("connection lost"));

        var result = await _sut.ExecuteAsync(input);

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Code.Should().StartWith("Membership.InviteFailed");
        result.Errors[0].Description.Should().Contain("connection lost");

        _changeLogRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _fakeTransaction.CommitCount.Should().Be(0);
        _fakeTransaction.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenChangeLogThrows_ShouldReturnInviteFailed_AndDisposeTransaction()
    {
        var input = ValidInput();
        var user = ValidUser(email: input.Email);

        _userRepositoryMock
            .Setup(r => r.GetByEmailAsync(input.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _membershipRepositoryMock
            .Setup(r => r.GetActiveByUserAndOrganisationAsync(
                user.UserId, input.OrganisationId, It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Membership?)null);
        _membershipRepositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<Membership>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _changeLogRepositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("change log write failed"));

        var result = await _sut.ExecuteAsync(input);

        result.IsError.Should().BeTrue();
        result.Errors[0].Code.Should().StartWith("Membership.InviteFailed");
        result.Errors[0].Description.Should().Contain("change log write failed");

        _fakeTransaction.CommitCount.Should().Be(0);
        _fakeTransaction.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldForwardCancellationToken()
    {
        var input = ValidInput();
        var user = ValidUser(email: input.Email);
        using var cts = new CancellationTokenSource();

        _userRepositoryMock
            .Setup(r => r.GetByEmailAsync(input.Email, cts.Token))
            .ReturnsAsync(user);
        _membershipRepositoryMock
            .Setup(r => r.GetActiveByUserAndOrganisationAsync(
                user.UserId, input.OrganisationId, It.IsAny<IUnitOfWorkTransaction?>(), cts.Token))
            .ReturnsAsync((Membership?)null);
        _membershipRepositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<Membership>(), It.IsAny<IUnitOfWorkTransaction?>(), cts.Token))
            .Returns(Task.CompletedTask);
        _changeLogRepositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), cts.Token))
            .Returns(Task.CompletedTask);

        await _sut.ExecuteAsync(input, cts.Token);

        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(cts.Token), Times.Once);
        _userRepositoryMock.Verify(r => r.GetByEmailAsync(input.Email, cts.Token), Times.Once);
        _membershipRepositoryMock.Verify(
            r => r.GetActiveByUserAndOrganisationAsync(user.UserId, input.OrganisationId, _fakeTransaction, cts.Token),
            Times.Once);
        _membershipRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<Membership>(), _fakeTransaction, cts.Token),
            Times.Once);
        _changeLogRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<ChangeLog>(), _fakeTransaction, cts.Token),
            Times.Once);
    }
}
