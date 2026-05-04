using ErrorOr;

namespace KulturHub.Application.Errors;

public static class InstagramErrors
{
    public static readonly Error TokenNotFound =
        Error.NotFound("Instagram.TokenNotFound", "No Instagram token found in database.");

    public static readonly Error TokenExpired =
        Error.Failure("Instagram.TokenExpired", "Instagram access token has expired.");

    public static Error PublishingFailed(string details) =>
        Error.Failure("Instagram.PublishingFailed", $"Instagram publishing failed: {details}");

    public static Error RefreshFailed(string details) =>
        Error.Failure("Instagram.RefreshFailed", $"Failed to refresh Instagram token: {details}");
}
