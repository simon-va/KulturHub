namespace KulturHub.Infrastructure.Auth;

public class SupabaseOptions
{
    public const string SectionName = "Supabase";

    public string Url { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string DiscoveryUrl { get; set; } = string.Empty;
}
