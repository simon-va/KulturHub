namespace KulturHub.Domain.Interfaces;

public interface IInstagramPublisher
{
    Task<string> PublishCarouselAsync(List<string> imageUrls, string caption);
}
