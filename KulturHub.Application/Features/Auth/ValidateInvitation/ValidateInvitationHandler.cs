using ErrorOr;
using FluentValidation;
using KulturHub.Application.Errors;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;

namespace KulturHub.Application.Features.Auth.ValidateInvitation;

public sealed class ValidateInvitationHandler(
    IInvitationRepository invitationRepository,
    IValidator<ValidateInvitationInput> validator)
{
    public async Task<ErrorOr<Success>> ExecuteAsync(
        ValidateInvitationInput input, CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(input, cancellationToken);
        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();

        var invitation = await invitationRepository.GetByCodeAsync(input.InvitationCode, cancellationToken);
        if (invitation is null)
            return InvitationErrors.NotFound;

        return invitation.EnsureCanBeUsed() switch
        {
            InvitationValidation.Ok          => Result.Success,
            InvitationValidation.Expired     => InvitationErrors.Expired,
            InvitationValidation.AlreadyUsed => InvitationErrors.AlreadyUsed,
            _ => throw new InvalidOperationException("Unhandled invitation validation result."),
        };
    }
}
