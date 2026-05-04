using ErrorOr;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KulturHub.Application.Features.WeeklyPost.GenerateWeeklyPost;

public class GenerateWeeklyPostHandler(
    IChaynsApiClient chaynsApiClient,
    IImageGenerator imageGenerator,
    IStorageService storageService,
    IPostRepository postRepository,
    IInstagramPublisher instagramPublisher,
    ILogger<GenerateWeeklyPostHandler> logger) : IRequestHandler<GenerateWeeklyPostCommand, ErrorOr<Guid>>
{
    public async Task<ErrorOr<Guid>> Handle(GenerateWeeklyPostCommand request, CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        int daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0) daysUntilMonday = 7;
        var weekStart = today.AddDays(daysUntilMonday);
        var weekEnd = weekStart.AddDays(6);

        var events = await chaynsApiClient.GetEventsAsync(weekStart, weekEnd);

        if (events.Count == 0)
        {
            logger.LogInformation("No events found for {WeekStart:d} – {WeekEnd:d}, skipping post creation.", weekStart, weekEnd);
            return Guid.Empty;
        }

        var post = Post.CreateWeeklyPost(weekStart, weekEnd);

        try
        {
            var imageBytes = imageGenerator.GenerateWeeklyImages(events, weekStart, weekEnd);

            post.MarkAsUploading();

            for (int i = 0; i < imageBytes.Count; i++)
            {
                var fileName = $"weekly_{weekStart:yyyyMMdd}_{i}.png";
                var url = await storageService.UploadImageAsync(imageBytes[i], fileName);

                post.AddImage(new PostImage
                {
                    Id = Guid.NewGuid(),
                    PostId = post.Id,
                    StorageUrl = url,
                    SortOrder = i,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await postRepository.CreateAsync(post);

            logger.LogInformation("Weekly post {PostId} saved with {ImageCount} images, starting Instagram publish.", post.Id, post.Images.Count);
        }
        catch (Exception ex)
        {
            post.MarkAsFailed(ex.Message);
            await postRepository.CreateAsync(post);

            logger.LogError(ex, "Failed to generate weekly post for {WeekStart:d}.", weekStart);
            return Error.Failure("WeeklyPost.GenerationFailed", ex.Message);
        }

        try
        {
            var imageUrls = post.Images
                .OrderBy(x => x.SortOrder)
                .Select(x => x.StorageUrl)
                .ToList();

            var mediaId = await instagramPublisher.PublishCarouselAsync(imageUrls, post.Caption);

            post.Publish(mediaId);

            logger.LogInformation("Weekly post {PostId} published to Instagram as {MediaId}.", post.Id, mediaId);
        }
        catch (Exception ex)
        {
            post.MarkAsFailed($"Instagram publishing failed: {ex.Message}");

            logger.LogError(ex, "Failed to publish weekly post {PostId} to Instagram.", post.Id);
        }

        await postRepository.UpdateAsync(post);
        return post.Id;
    }
}
