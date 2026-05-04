using KulturHub.Domain.Interfaces;

namespace KulturHub.Infrastructure.Storage;

public class SupabaseStorageService(Supabase.Client supabaseClient) : IStorageService
{
    private const string BucketName = "kulturhub-images";
    private readonly Supabase.Client _supabaseClient = supabaseClient;

    public async Task<string> UploadImageAsync(byte[] imageData, string fileName)
    {
        var supabasePath = $"weekly/{fileName}";
        var bucket = _supabaseClient.Storage.From(BucketName);

        await bucket.Upload(
            imageData,
            supabasePath,
            new Supabase.Storage.FileOptions
            {
                ContentType  = "image/png",
                CacheControl = "3600",
                Upsert       = true
            });

        return bucket.GetPublicUrl(supabasePath);
    }
}
