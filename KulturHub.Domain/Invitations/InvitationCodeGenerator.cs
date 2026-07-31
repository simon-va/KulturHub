using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace KulturHub.Domain.Invitations;

public static class InvitationCodeGenerator
{
    private const string AllowedChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int GroupLength = 4;

    private static readonly Regex FormatRegex = new(
        InvitationCodeSpecs.Pattern,
        RegexOptions.Compiled);

    public static string Generate() => RandomChars(GroupLength);

    private static string RandomChars(int length)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++)
            chars[i] = AllowedChars[RandomNumberGenerator.GetInt32(AllowedChars.Length)];
        return new string(chars);
    }

    public static bool IsValid(string code) =>
        !string.IsNullOrEmpty(code) && FormatRegex.IsMatch(code);
}
