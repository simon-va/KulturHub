namespace KulturHub.Application.Rules;

public static class InvitationCodeRules
{
    public const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public const string CodePattern = "^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{3}-[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{3}$";

    public const int SegmentLength = 3;
}
