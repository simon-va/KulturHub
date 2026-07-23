using ErrorOr;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using KulturHub.Application.Errors;
using KulturHub.Application.Features.Organisations.CreateOrganisation;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using KulturHub.UnitTests.Common;
using Microsoft.Extensions.Logging;
using Moq;

namespace KulturHub.UnitTests.Features.Organisations.CreateOrganisation;

public class CreateOrganisationHandlerTests
{
    // Rules:
    // - Validation runs first; invalid input short-circuits before any dependency is touched.
    // - If an organisation with the same name already exists, OrganisationErrors.NameTaken is returned
    //   and no transaction is opened.
    // - On success, a transaction is opened, organisation, membership and change log are inserted
    //   through it, and the transaction is committed. The change log entry has message
    //   "Organisation erstellt" and data { "name": "<organisation name>" }.
    // - If any repository throws, the transaction is rolled back (via IAsyncDisposable) and
    //   OrganisationErrors.CreateFailed is returned.

    private readonly Mock<IOrganisationRepository> _organisationRepositoryMock = new();
    private readonly Mock<IMembershipRepository> _membershipRepositoryMock = new();
    private readonly Mock<IChangeLogRepository> _changeLogRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IValidator<CreateOrganisationInput>> _validatorMock = new();
    private readonly Mock<ILogger<CreateOrganisationHandler>> _loggerMock = new();

    private readonly CreateOrganisationHandler _sut;
    private readonly FakeUnitOfWorkTransaction _fakeTransaction = new();

    public CreateOrganisationHandlerTests()
    {
        _sut = new CreateOrganisationHandler(
            _organisationRepositoryMock.Object,
            _membershipRepositoryMock.Object,
            _changeLogRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _validatorMock.Object,
            _loggerMock.Object);

        _unitOfWorkMock
            .Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_fakeTransaction);
    }

    private static CreateOrganisationInput ValidInput(Guid? userId = null) =>
        new("Acme Inc.", userId ?? Guid.NewGuid());

    [Fact]
    public async Task ExecuteAsync_WhenAllInputsAreValid_ShouldReturnCreateOrganisationResponse_AndCommitTransaction()
    {
        var userId = Guid.NewGuid();
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<CreateOrganisationInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _organisationRepositoryMock
            .Setup(r => r.ExistsByNameAsync("Acme Inc.", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _organisationRepositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<Organisation>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _membershipRepositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<Membership>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _changeLogRepositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.ExecuteAsync(ValidInput(userId));

        result.IsError.Should().BeFalse();
        result.Value.Name.Should().Be("Acme Inc.");
        result.Value.Id.Should().NotBe(Guid.Empty);
        result.Value.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _organisationRepositoryMock.Verify(
            r => r.InsertAsync(
                It.Is<Organisation>(o => o.Name == "Acme Inc."),
                _fakeTransaction,
                It.IsAny<CancellationToken>()),
            Times.Once);
        _membershipRepositoryMock.Verify(
            r => r.InsertAsync(
                It.Is<Membership>(m => m.UserId == userId && m.OrganisationId != Guid.Empty),
                _fakeTransaction,
                It.IsAny<CancellationToken>()),
            Times.Once);
        _changeLogRepositoryMock.Verify(
            r => r.InsertAsync(
                It.Is<ChangeLog>(c =>
                    c.Message == "Organisation erstellt"
                    && c.UserId == userId
                    && c.OrganisationId != Guid.Empty
                    && c.Data.ContainsKey("name")
                    && c.Data["name"] as string == "Acme Inc."),
                _fakeTransaction,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _fakeTransaction.CommitCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInputIsInvalid_ShouldReturnValidationErrors_AndNotTouchDependencies()
    {
        var failures = new[]
        {
            new ValidationFailure("Name", "Name is required."),
        };
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<CreateOrganisationInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(failures));

        var result = await _sut.ExecuteAsync(ValidInput());

        result.IsError.Should().BeTrue();
        result.Errors.Should().AllSatisfy(e => e.Type.Should().Be(ErrorType.Validation));
        result.Errors.Should().ContainSingle(e => e.Code == "Name");

        _organisationRepositoryMock.Verify(
            r => r.ExistsByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _organisationRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<Organisation>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _membershipRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<Membership>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _changeLogRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNameAlreadyExists_ShouldReturnNameTaken_AndNotOpenTransaction()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<CreateOrganisationInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _organisationRepositoryMock
            .Setup(r => r.ExistsByNameAsync("Acme Inc.", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.ExecuteAsync(ValidInput());

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == OrganisationErrors.NameTaken.Code);
        result.Errors[0].Type.Should().Be(ErrorType.Conflict);

        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _organisationRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<Organisation>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _membershipRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<Membership>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _changeLogRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOrganisationRepositoryThrows_ShouldReturnCreateFailed_AndTransactionIsDisposed()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<CreateOrganisationInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _organisationRepositoryMock
            .Setup(r => r.ExistsByNameAsync("Acme Inc.", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _organisationRepositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<Organisation>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("connection lost"));

        var result = await _sut.ExecuteAsync(ValidInput());

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Code.Should().StartWith("Organisation.CreateFailed");
        result.Errors[0].Description.Should().Contain("connection lost");

        _fakeTransaction.CommitCount.Should().Be(0);
        _fakeTransaction.DisposeCount.Should().Be(1);
        _membershipRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<Membership>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _changeLogRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMembershipRepositoryThrows_ShouldReturnCreateFailed_AndTransactionIsDisposed()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<CreateOrganisationInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _organisationRepositoryMock
            .Setup(r => r.ExistsByNameAsync("Acme Inc.", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _organisationRepositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<Organisation>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _membershipRepositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<Membership>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("fk violation"));

        var result = await _sut.ExecuteAsync(ValidInput());

        result.IsError.Should().BeTrue();
        result.Errors[0].Code.Should().StartWith("Organisation.CreateFailed");
        result.Errors[0].Description.Should().Contain("fk violation");

        _fakeTransaction.CommitCount.Should().Be(0);
        _fakeTransaction.DisposeCount.Should().Be(1);
        _changeLogRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenChangeLogRepositoryThrows_ShouldReturnCreateFailed_AndTransactionIsDisposed()
    {
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<CreateOrganisationInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _organisationRepositoryMock
            .Setup(r => r.ExistsByNameAsync("Acme Inc.", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _organisationRepositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<Organisation>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _membershipRepositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<Membership>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _changeLogRepositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("change log write failed"));

        var result = await _sut.ExecuteAsync(ValidInput());

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Code.Should().StartWith("Organisation.CreateFailed");
        result.Errors[0].Description.Should().Contain("change log write failed");

        _fakeTransaction.CommitCount.Should().Be(0);
        _fakeTransaction.DisposeCount.Should().Be(1);
    }
}
