namespace KulturHub.Application.Ports;

public record InvitationFilter(bool IncludeUsed, bool IncludeExpired)
{
    public static readonly InvitationFilter OpenOnly = new(false, false);
    public static readonly InvitationFilter All = new(true, true);
}
