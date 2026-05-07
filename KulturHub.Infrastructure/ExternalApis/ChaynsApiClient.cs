using System.Globalization;
using System.Net.Http.Json;
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

    private record TobitEventAddress(string Name);

    private record TobitEventDto(
        string Name,
        string LocationName,
        TobitEventAddress? Address,
        string StartTime);
}
