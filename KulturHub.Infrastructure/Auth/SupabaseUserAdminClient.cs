/*using System.Net.Http.Headers;
using KulturHub.Application.Ports;
using Microsoft.Extensions.Options;

namespace KulturHub.Infrastructure.Auth;

public class SupabaseUserAdminClient(HttpClient httpClient, IOptions<SupabaseAuthOptions> options) : IUserAdminClient
{
    private readonly SupabaseAuthOptions _options = options.Value;

    public async Task<bool> DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Url))
            throw new InvalidOperationException("Supabase:Url is not configured.");
        if (string.IsNullOrWhiteSpace(_options.Key))
            throw new InvalidOperationException("Supabase:Key is not configured.");

        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            new Uri(new Uri(_options.Url), $"/auth/v1/admin/users/{userId}"));

        request.Headers.Add("apikey", _options.Key);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Key);

        var response = await httpClient.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }
}*/
