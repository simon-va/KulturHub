namespace KulturHub.Application.Ports;

public interface IStorageService
{
    Task<string> UploadImageAsync(byte[] imageData, string fileName);
}
