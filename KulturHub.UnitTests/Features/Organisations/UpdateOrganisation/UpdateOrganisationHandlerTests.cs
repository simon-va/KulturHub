using ErrorOr;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using KulturHub.Application.Errors;
using KulturHub.Application.Features.Organisations.UpdateOrganisation;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using KulturHub.UnitTests.Common;
using Microsoft.Extensions.Logging;
using Moq;

namespace KulturHub.UnitTests.Features.Organisations.UpdateOrganisation;

public class UpdateOrganisationHandlerTests
{
    // Rules:
    // - Validation runs first; invalid input short-circuits before any dependency is touched.
    // - If the organisation does not exist, OrganisationErrors.NotFound is returned and no transaction is opened.
    // - The name uniqueness check is only run when the new name differs from the current name.
    // - If the new name is already taken by another organisation, OrganisationErrors.NameTaken is returned
    //   and no transaction is opened.
    // - On success, a transaction is opened, the renamed entity is updated and a change log entry with
    //   message "Organisation aktualisiert" and data { "name": "<new name>" } is inserted through it,
    //   and the transaction is committed.
    // - If any repository throws, the transaction is rolled back (via IAsyncDisposable) and
    //   OrganisationErrors.UpdateFailed is returned.

    private readonly Mock<IOrganisationRepository> _organisationRepositoryMock = new();
    private readonly Mock<IChangeLogRepository> _changeLogRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IValidator<UpdateOrganisationInput>> _validatorMock = new();
    private readonly Mock<ILogger<UpdateOrganisationHandler>> _loggerMock = new();

    private readonly UpdateOrganisationHandler _sut;
    private readonly FakeUnitOfWorkTransaction _fakeTransaction = new();

    public UpdateOrganisationHandlerTests()
    {
        _sut = new UpdateOrganisationHandler(
            _organisationRepositoryMock.Object,
            _changeLogRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _validatorMock.Object,
            _loggerMock.Object);

        _unitOfWorkMock
            .Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_fakeTransaction);
    }

    private static UpdateOrganisationInput ValidInput(string name = "Acme Inc.") =>
        new(name, Guid.NewGuid(), Guid.NewGuid());

    private static Organisation ExistingOrganisation(Guid id, string name) =>
        Organisation.Reconstitute(id, name, DateTime.UtcNow);

    [Fact]
    public async Task ExecuteAsync_WhenAllInputsAreValid_ShouldReturnUpdateOrganisationResponse_AndCommitTransaction()
    {
        var organisationId = Guid.NewGuid();
        var existing = ExistingOrganisation(organisationId, "Acme Inc.");
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<UpdateOrganisationInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _organisationRepositoryMock
            .Setup(r => r.GetByIdAsync(organisationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _organisationRepositoryMock
            .Setup(r => r.ExistsByNameAsync("Acme Inc. (updated)", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _organisationRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Organisation>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _changeLogRepositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.ExecuteAsync(ValidInput("Acme Inc. (updated)") with { OrganisationId = organisationId });

        result.IsError.Should().BeFalse();
        result.Value.Name.Should().Be("Acme Inc. (updated)");
        result.Value.Id.Should().Be(organisationId);
        result.Value.CreatedAt.Should().BeCloseTo(existing.CreatedAt, TimeSpan.FromSeconds(1));

        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _organisationRepositoryMock.Verify(
            r => r.UpdateAsync(
                It.Is<Organisation>(o => o.Id == organisationId && o.Name == "Acme Inc. (updated)"),
                _fakeTransaction,
                It.IsAny<CancellationToken>()),
            Times.Once);
        _changeLogRepositoryMock.Verify(
            r => r.InsertAsync(
                It.Is<ChangeLog>(c =>
                    c.Message == "Organisation aktualisiert"
                    && c.OrganisationId == organisationId
                    && c.Data.ContainsKey("name")
                    && c.Data["name"] as string == "Acme Inc. (updated)"),
                _fakeTransaction,
                It.IsAny<CancellationToken>()),
            Times.Once);
        _fakeTransaction.CommitCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNameIsUnchanged_ShouldSkipUniquenessCheck_AndStillUpdate_AndSkipChangeLog()
    {
        var organisationId = Guid.NewGuid();
        var existing = ExistingOrganisation(organisationId, "Acme Inc.");
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<UpdateOrganisationInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _organisationRepositoryMock
            .Setup(r => r.GetByIdAsync(organisationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _organisationRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Organisation>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.ExecuteAsync(ValidInput("Acme Inc.") with { OrganisationId = organisationId });

        result.IsError.Should().BeFalse();
        result.Value.Name.Should().Be("Acme Inc.");

        _organisationRepositoryMock.Verify(
            r => r.ExistsByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _organisationRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<Organisation>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _changeLogRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _fakeTransaction.CommitCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNameHasOnlyWhitespaceDifference_ShouldTreatAsUnchanged_AndSkipChangeLog()
    {
        var organisationId = Guid.NewGuid();
        var existing = ExistingOrganisation(organisationId, "Acme Inc.");
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<UpdateOrganisationInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _organisationRepositoryMock
            .Setup(r => r.GetByIdAsync(organisationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _organisationRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Organisation>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.ExecuteAsync(ValidInput("  Acme Inc.  ") with { OrganisationId = organisationId });

        result.IsError.Should().BeFalse();
        result.Value.Name.Should().Be("Acme Inc.");

        _organisationRepositoryMock.Verify(
            r => r.ExistsByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _changeLogRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInputIsInvalid_ShouldReturnValidationErrors_AndNotTouchDependencies()
    {
        var failures = new[]
        {
            new ValidationFailure("Name", "Name is required."),
        };
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<UpdateOrganisationInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(failures));

        var result = await _sut.ExecuteAsync(ValidInput());

        result.IsError.Should().BeTrue();
        result.Errors.Should().AllSatisfy(e => e.Type.Should().Be(ErrorType.Validation));
        result.Errors.Should().ContainSingle(e => e.Code == "Name");

        _organisationRepositoryMock.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _organisationRepositoryMock.Verify(
            r => r.ExistsByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _organisationRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<Organisation>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _changeLogRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOrganisationDoesNotExist_ShouldReturnNotFound_AndNotOpenTransaction()
    {
        var organisationId = Guid.NewGuid();
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<UpdateOrganisationInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _organisationRepositoryMock
            .Setup(r => r.GetByIdAsync(organisationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Organisation?)null);

        var result = await _sut.ExecuteAsync(ValidInput() with { OrganisationId = organisationId });

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == OrganisationErrors.NotFound.Code);
        result.Errors[0].Type.Should().Be(ErrorType.NotFound);

        _organisationRepositoryMock.Verify(
            r => r.ExistsByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _organisationRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<Organisation>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _changeLogRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNewNameAlreadyTaken_ShouldReturnNameTaken_AndNotOpenTransaction()
    {
        var organisationId = Guid.NewGuid();
        var existing = ExistingOrganisation(organisationId, "Acme Inc.");
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<UpdateOrganisationInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _organisationRepositoryMock
            .Setup(r => r.GetByIdAsync(organisationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _organisationRepositoryMock
            .Setup(r => r.ExistsByNameAsync("Acme Inc. (renamed)", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.ExecuteAsync(ValidInput("Acme Inc. (renamed)") with { OrganisationId = organisationId });

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == OrganisationErrors.NameTaken.Code);
        result.Errors[0].Type.Should().Be(ErrorType.Conflict);

        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _organisationRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<Organisation>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _changeLogRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryThrows_ShouldReturnUpdateFailed_AndTransactionIsDisposed()
    {
        var organisationId = Guid.NewGuid();
        var existing = ExistingOrganisation(organisationId, "Acme Inc.");
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<UpdateOrganisationInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _organisationRepositoryMock
            .Setup(r => r.GetByIdAsync(organisationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _organisationRepositoryMock
            .Setup(r => r.ExistsByNameAsync("Acme Inc. (updated)", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _organisationRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Organisation>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("connection lost"));

        var result = await _sut.ExecuteAsync(ValidInput("Acme Inc. (updated)") with { OrganisationId = organisationId });

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Code.Should().StartWith("Organisation.UpdateFailed");
        result.Errors[0].Description.Should().Contain("connection lost");

        _fakeTransaction.CommitCount.Should().Be(0);
        _fakeTransaction.DisposeCount.Should().Be(1);
        _changeLogRepositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenChangeLogRepositoryThrows_ShouldReturnUpdateFailed_AndTransactionIsDisposed()
    {
        var organisationId = Guid.NewGuid();
        var existing = ExistingOrganisation(organisationId, "Acme Inc.");
        _validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<UpdateOrganisationInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _organisationRepositoryMock
            .Setup(r => r.GetByIdAsync(organisationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _organisationRepositoryMock
            .Setup(r => r.ExistsByNameAsync("Acme Inc. (updated)", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _organisationRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Organisation>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _changeLogRepositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<ChangeLog>(), It.IsAny<IUnitOfWorkTransaction?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("change log write failed"));

        var result = await _sut.ExecuteAsync(ValidInput("Acme Inc. (updated)") with { OrganisationId = organisationId });

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Code.Should().StartWith("Organisation.UpdateFailed");
        result.Errors[0].Description.Should().Contain("change log write failed");

        _fakeTransaction.CommitCount.Should().Be(0);
        _fakeTransaction.DisposeCount.Should().Be(1);
    }
}
