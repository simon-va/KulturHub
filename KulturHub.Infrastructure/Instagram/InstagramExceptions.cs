namespace KulturHub.Infrastructure.Instagram;

public class InstagramTokenNotFoundException()
    : Exception("No Instagram token found in database.");

public class InstagramTokenExpiredException()
    : Exception("Instagram access token has expired.");

public class InstagramPublishingException(string details)
    : Exception($"Instagram publishing failed: {details}");
