using ErrorOr;
using MediatR;

namespace KulturHub.Application.Features.Events.CreateEvent;

public record CreateEventCommand(
    string Title,
    DateTime StartTime,
    DateTime EndTime,
    string Address,
    string Description) : IRequest<ErrorOr<Guid>>;
