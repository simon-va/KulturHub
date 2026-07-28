using KulturHub.Application.Ports;
using KulturHub.Domain.Invitations;

namespace KulturHub.Infrastructure.Invitations;

public sealed class InvitationCodeGeneratorAdapter : IInvitationCodeGenerator
{
    public string Generate() => InvitationCodeGenerator.Generate();
}
