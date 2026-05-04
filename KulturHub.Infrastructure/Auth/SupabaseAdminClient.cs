using System.Net.Http.Headers;
using KulturHub.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

namespace KulturHub.Infrastructure.Auth;

public class SupabaseAdminClient(HttpClient httpClient, IConfiguration configuration) : ISupabaseAdminClient
{
    public async Task<bool> DeleteUserAsync(Guid userId)
    {
        string supabaseUrl = configuration["Supabase:Url"]
            ?? throw new InvalidOperationException("Supabase:Url is not configured.");

        string serviceRoleKey = configuration["Supabase:Key"]
            ?? throw new InvalidOperationException("Supabase:Key is not configured.");

        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"{supabaseUrl}/auth/v1/admin/users/{userId}");

        request.Headers.Add("apikey", serviceRoleKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceRoleKey);

        var response = await httpClient.SendAsync(request);
        return response.IsSuccessStatusCode;
    }
}
