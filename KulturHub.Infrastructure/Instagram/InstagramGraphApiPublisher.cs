using System.Text.Json.Serialization;
using KulturHub.Domain.Exceptions;
using KulturHub.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace KulturHub.Infrastructure.Instagram;

public class InstagramGraphApiPublisher(
    HttpClient httpClient,
    IInstagramTokenRepository tokenRepository,
    ILogger<InstagramGraphApiPublisher> logger) : IInstagramPublisher, IInstagramTokenRefresher
{
    private const string BaseUrl = "https://graph.instagram.com/v25.0";

    public async Task<string> PublishCarouselAsync(List<string> imageUrls, string caption)
    {
        var token = await tokenRepository.GetCurrentTokenAsync()
            ?? throw new InstagramTokenNotFoundException();

        if (token.ExpiresAt <= DateTime.UtcNow)
            throw new InstagramTokenExpiredException();

        var containerIds = new List<string>();
        foreach (var imageUrl in imageUrls)
        {
            var id = await CreateItemContainerAsync(token.InstagramUserId, token.AccessToken, imageUrl);
            containerIds.Add(id);
        }

        var carouselId = await CreateCarouselContainerAsync(
            token.InstagramUserId, token.AccessToken, containerIds, caption);

        await WaitForContainerReadyAsync(carouselId, token.AccessToken);

        return await PublishContainerAsync(token.InstagramUserId, token.AccessToken, carouselId);
    }

    public async Task<(string AccessToken, DateTime ExpiresAt)> RefreshAsync(
        string currentAccessToken,
        CancellationToken cancellationToken = default)
    {
        var url = "https://graph.instagram.com/refresh_access_token" +
                  $"?grant_type=ig_refresh_token" +
                  $"&access_token={currentAccessToken}";

        var response = await httpClient.GetAsync(url, cancellationToken);
        var result = await ReadResponseAsync<RefreshResponse>(response, "refresh token");

        return (result.AccessToken, DateTime.UtcNow.AddSeconds(result.ExpiresIn));
    }

    private async Task<string> CreateItemContainerAsync(string userId, string accessToken, string imageUrl)
    {
        var url = $"{BaseUrl}/{userId}/media" +
                  $"?image_url={Uri.EscapeDataString(imageUrl)}" +
                  $"&is_carousel_item=true" +
                  $"&access_token={accessToken}";

        var response = await httpClient.PostAsync(url, null);
        var result = await ReadResponseAsync<MediaIdResponse>(response, "create item container");
        return result.Id;
    }

    private async Task<string> CreateCarouselContainerAsync(
        string userId, string accessToken, List<string> childIds, string caption)
    {
        var children = Uri.EscapeDataString(string.Join(",", childIds));
        var url = $"{BaseUrl}/{userId}/media" +
                  $"?media_type=CAROUSEL" +
                  $"&caption={Uri.EscapeDataString(caption)}" +
                  $"&children={children}" +
                  $"&access_token={accessToken}";

        var response = await httpClient.PostAsync(url, null);
        var result = await ReadResponseAsync<MediaIdResponse>(response, "create carousel container");
        return result.Id;
    }

    private async Task WaitForContainerReadyAsync(string containerId, string accessToken)
    {
        for (int attempt = 1; attempt <= 5; attempt++)
        {
            await Task.Delay(TimeSpan.FromSeconds(3));

            var url = $"{BaseUrl}/{containerId}?fields=status_code&access_token={accessToken}";
            var response = await httpClient.GetAsync(url);
            var result = await ReadResponseAsync<StatusCodeResponse>(response, "poll container status");

            logger.LogDebug("Container {Id} status: {Status} (attempt {Attempt}/5)", containerId, result.StatusCode, attempt);

            if (result.StatusCode == "FINISHED")
                return;

            if (result.StatusCode == "ERROR")
                throw new InstagramPublishingException($"Container {containerId} processing failed with status ERROR.");
        }

        throw new InstagramPublishingException($"Container {containerId} did not reach FINISHED status after 5 attempts.");
    }

    private async Task<string> PublishContainerAsync(string userId, string accessToken, string carouselId)
    {
        var url = $"{BaseUrl}/{userId}/media_publish" +
                  $"?creation_id={carouselId}" +
                  $"&access_token={accessToken}";

        var response = await httpClient.PostAsync(url, null);
        var result = await ReadResponseAsync<MediaIdResponse>(response, "publish carousel");
        return result.Id;
    }

    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, string operation)
    {
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var error = TryParseErrorMessage(json);
            throw new HttpRequestException(
                $"Instagram API error during '{operation}' (HTTP {(int)response.StatusCode}): {error}");
        }

        return System.Text.Json.JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Empty response from Instagram API during '{operation}'.");
    }

    private static string TryParseErrorMessage(string json)
    {
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out var errorProp) &&
                errorProp.TryGetProperty("message", out var messageProp))
                return messageProp.GetString() ?? json;
        }
        catch { }
        return json;
    }

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed record MediaIdResponse([property: JsonPropertyName("id")] string Id);
    private sealed record StatusCodeResponse([property: JsonPropertyName("status_code")] string StatusCode);
    private sealed record RefreshResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] long ExpiresIn);
}
