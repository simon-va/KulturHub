namespace KulturHub.Domain.Invitations;

public static class InvitationCodeSpecs
{
    public const string Pattern = @"^[A-HJ-NP-Z2-9]{3}-[A-HJ-NP-Z2-9]{3}$";
    public const string FormatHint = "XXX-XXX";
}
