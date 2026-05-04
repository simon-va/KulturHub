namespace KulturHub.Domain.Interfaces;

public interface IStorageService
{
    Task<string> UploadImageAsync(byte[] imageData, string fileName);
}
