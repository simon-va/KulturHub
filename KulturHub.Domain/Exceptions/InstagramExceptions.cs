namespace KulturHub.Domain.Exceptions;

public class InstagramTokenNotFoundException()
    : DomainException("No Instagram token found in database.");

public class InstagramTokenExpiredException()
    : DomainException("Instagram access token has expired.");

public class InstagramPublishingException(string details)
    : DomainException($"Instagram publishing failed: {details}");
