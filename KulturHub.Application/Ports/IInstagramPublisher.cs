namespace KulturHub.Application.Ports;

public interface IInstagramPublisher
{
    Task<string> PublishCarouselAsync(List<string> imageUrls, string caption);
}
