using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using KulturHub.Application.Features.WeeklyPost;
using KulturHub.Application.Ports;
using Microsoft.Extensions.Configuration;

namespace KulturHub.Infrastructure.ExternalApis;

public class ChaynsApiClient(HttpClient httpClient, IConfiguration config) : IChaynsApiClient
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<List<ChaynsEvent>> GetEventsAsync(DateTime startDate, DateTime endDate)
    {
        string siteId = config["Chayns:SiteId"]
            ?? throw new InvalidOperationException("Chayns:SiteId fehlt in appsettings.json");
        string pageId = config["Chayns:PageId"]
            ?? throw new InvalidOperationException("Chayns:PageId fehlt in appsettings.json");

        string baseUrl = $"https://mashup.tobit.com/api/events/v2.0/v2.0/sites/{siteId}/pages/{pageId}/events";
        string minStart = ToUtcParam(startDate.Date);
        string maxStart = ToUtcParam(endDate.Date.AddDays(1).AddTicks(-1));

        var all = new List<TobitEventDto>();
        int skip = 0;
        const int take = 20;

        while (true)
        {
            string url = $"{baseUrl}?minStartTime={minStart}&maxStartTime={maxStart}&isHidden=false&sortOrder=Ascending&skip={skip}&take={take}";
            var batch = await httpClient.GetFromJsonAsync<List<TobitEventDto>>(url, JsonOptions) ?? [];
            all.AddRange(batch);
            if (batch.Count < take) break;
            skip += take;
        }

        return all.Select(Map).ToList();
    }

    private static string ToUtcParam(DateTime localDate)
    {
        DateTime utc = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(localDate, DateTimeKind.Unspecified), Berlin);
        return Uri.EscapeDataString(utc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
    }

    private static ChaynsEvent Map(TobitEventDto dto)
    {
        DateTime utc = DateTime.Parse(dto.StartTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        DateTime local = TimeZoneInfo.ConvertTimeFromUtc(utc, Berlin);

        return new ChaynsEvent
        {
            Title     = dto.Name,
            Location  = string.IsNullOrWhiteSpace(dto.LocationName)
                            ? dto.Address?.Name ?? string.Empty
                            : dto.LocationName,
            StartDate = local,
        };
    }

    public async Task<int> CreateEventAsync(string title, DateTime startTime, DateTime endTime,
        string address, string description)
    {
        string eventSiteId = config["Chayns:EventSiteId"]
            ?? throw new InvalidOperationException("Chayns:EventSiteId fehlt in appsettings.json");
        string eventPageId = config["Chayns:EventPageId"]
            ?? throw new InvalidOperationException("Chayns:EventPageId fehlt in appsettings.json");
        string bearerToken = await FetchUserTokenAsync();

        string url = $"https://mashup.tobit.com/api/events/v2.0/v2.0/sites/{eventSiteId}/pages/{eventPageId}/events";

        var body = new
        {
            name = title,
            startTime = ToChaynsDateParam(startTime),
            endTime = ToChaynsDateParam(endTime),
            locationName = address,
            description,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        request.Content = JsonContent.Create(body);

        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CreateEventResponse>(JsonOptions)
            ?? throw new InvalidOperationException("Ungültige Antwort vom Chayns Event-Endpoint");
        return result.Id;
    }

    private async Task<string> FetchUserTokenAsync()
    {
        string username = config["Chayns:Username"]
            ?? throw new InvalidOperationException("Chayns:Username fehlt in appsettings.json");
        string password = config["Chayns:Password"]
            ?? throw new InvalidOperationException("Chayns:Password fehlt in appsettings.json");
        string locationId = config["Chayns:EventLocationId"]
            ?? throw new InvalidOperationException("Chayns:EventLocationId fehlt in appsettings.json");

        string credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://auth.tobit.com/v2/token");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Content = JsonContent.Create(new { tokenType = 1, locationId = locationId });

        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions)
            ?? throw new InvalidOperationException("Ungültige Antwort vom Chayns Auth-Endpoint");
        return result.Token;
    }

    private static string ToChaynsDateParam(DateTime dateTime) =>
        dateTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) + "Z";

    private record TobitEventAddress(string Name);

    private record TobitEventDto(
        string Name,
        string LocationName,
        TobitEventAddress? Address,
        string StartTime);

    private record TokenResponse(string Token);

    private record CreateEventResponse(int Id);
}
