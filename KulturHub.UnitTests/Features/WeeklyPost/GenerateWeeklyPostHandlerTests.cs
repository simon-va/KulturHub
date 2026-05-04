using FluentAssertions;
using KulturHub.Application.Features.WeeklyPost.GenerateWeeklyPost;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Enums;
using KulturHub.Application.Ports;
using KulturHub.Domain.Interfaces;
using KulturHub.Domain.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace KulturHub.UnitTests.Features.WeeklyPost;

public class WeeklyPostServiceTests
{
    // Rules:
    // - Returns Guid.Empty when no events are found for the week, without saving anything
    // - On image generation or upload failure: post is marked as Failed, saved to DB via CreateAsync, ErrorOr error is returned
    // - On Instagram publishing failure: post is marked as Failed, UpdateAsync is still called, post.Id is returned (not an error)
    // - Happy path: CreateAsync and UpdateAsync are each called exactly once

    private readonly Mock<IChaynsApiClient> _chaynsApiClientMock = new();
    private readonly Mock<IImageGenerator> _imageGeneratorMock = new();
    private readonly Mock<IStorageService> _storageServiceMock = new();
    private readonly Mock<IPostRepository> _postRepositoryMock = new();
    private readonly Mock<IInstagramPublisher> _instagramPublisherMock = new();
    private readonly Mock<ILogger<WeeklyPostService>> _loggerMock = new();
    private readonly WeeklyPostService _service;

    public WeeklyPostServiceTests()
    {
        _service = new WeeklyPostService(
            _chaynsApiClientMock.Object,
            _imageGeneratorMock.Object,
            _storageServiceMock.Object,
            _postRepositoryMock.Object,
            _instagramPublisherMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task GenerateWeeklyPostAsync_WhenNoEventsFound_ShouldReturnEmptyGuid()
    {
        _chaynsApiClientMock
            .Setup(x => x.GetEventsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([]);

        var result = await _service.GenerateWeeklyPostAsync(CancellationToken.None);

        result.Value.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task GenerateWeeklyPostAsync_WhenNoEventsFound_ShouldNotCreatePost()
    {
        _chaynsApiClientMock
            .Setup(x => x.GetEventsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([]);

        await _service.GenerateWeeklyPostAsync(CancellationToken.None);

        _postRepositoryMock.Verify(x => x.CreateAsync(It.IsAny<Post>()), Times.Never);
    }

    [Fact]
    public async Task GenerateWeeklyPostAsync_WhenEventsExist_ShouldReturnPostId()
    {
        SetupHappyPath(imageCount: 2);

        var result = await _service.GenerateWeeklyPostAsync(CancellationToken.None);

        result.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task GenerateWeeklyPostAsync_WhenEventsExist_ShouldCallCreateAsyncOnce()
    {
        SetupHappyPath(imageCount: 2);

        await _service.GenerateWeeklyPostAsync(CancellationToken.None);

        _postRepositoryMock.Verify(x => x.CreateAsync(It.IsAny<Post>()), Times.Once);
    }

    [Fact]
    public async Task GenerateWeeklyPostAsync_WhenEventsExist_ShouldCallUpdateAsyncOnce()
    {
        SetupHappyPath(imageCount: 2);

        await _service.GenerateWeeklyPostAsync(CancellationToken.None);

        _postRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Post>()), Times.Once);
    }

    [Fact]
    public async Task GenerateWeeklyPostAsync_WhenEventsExist_ShouldUploadImageForEachGeneratedImage()
    {
        SetupHappyPath(imageCount: 3);

        await _service.GenerateWeeklyPostAsync(CancellationToken.None);

        _storageServiceMock.Verify(
            x => x.UploadImageAsync(It.IsAny<byte[]>(), It.IsAny<string>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task GenerateWeeklyPostAsync_WhenImageGenerationFails_ShouldReturnError()
    {
        _chaynsApiClientMock
            .Setup(x => x.GetEventsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([new ChaynsEvent { Title = "Event", Location = "Ort", StartDate = DateTime.UtcNow }]);

        _imageGeneratorMock
            .Setup(x => x.GenerateWeeklyImages(It.IsAny<List<ChaynsEvent>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Throws(new InvalidOperationException("Render error"));

        var result = await _service.GenerateWeeklyPostAsync(CancellationToken.None);

        result.IsError.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateWeeklyPostAsync_WhenImageGenerationFails_ShouldCreateFailedPost()
    {
        _chaynsApiClientMock
            .Setup(x => x.GetEventsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([new ChaynsEvent { Title = "Event", Location = "Ort", StartDate = DateTime.UtcNow }]);

        _imageGeneratorMock
            .Setup(x => x.GenerateWeeklyImages(It.IsAny<List<ChaynsEvent>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Throws(new InvalidOperationException("Render error"));

        await _service.GenerateWeeklyPostAsync(CancellationToken.None);

        _postRepositoryMock.Verify(
            x => x.CreateAsync(It.Is<Post>(p => p.Status == PostStatus.Failed)),
            Times.Once);
    }

    [Fact]
    public async Task GenerateWeeklyPostAsync_WhenImageUploadFails_ShouldReturnError()
    {
        _chaynsApiClientMock
            .Setup(x => x.GetEventsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([new ChaynsEvent { Title = "Event", Location = "Ort", StartDate = DateTime.UtcNow }]);

        _imageGeneratorMock
            .Setup(x => x.GenerateWeeklyImages(It.IsAny<List<ChaynsEvent>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns([new byte[] { 1 }]);

        _storageServiceMock
            .Setup(x => x.UploadImageAsync(It.IsAny<byte[]>(), It.IsAny<string>()))
            .ThrowsAsync(new IOException("Storage unavailable"));

        var result = await _service.GenerateWeeklyPostAsync(CancellationToken.None);

        result.IsError.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateWeeklyPostAsync_WhenImageUploadFails_ShouldCreateFailedPost()
    {
        _chaynsApiClientMock
            .Setup(x => x.GetEventsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([new ChaynsEvent { Title = "Event", Location = "Ort", StartDate = DateTime.UtcNow }]);

        _imageGeneratorMock
            .Setup(x => x.GenerateWeeklyImages(It.IsAny<List<ChaynsEvent>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns([new byte[] { 1 }]);

        _storageServiceMock
            .Setup(x => x.UploadImageAsync(It.IsAny<byte[]>(), It.IsAny<string>()))
            .ThrowsAsync(new IOException("Storage unavailable"));

        await _service.GenerateWeeklyPostAsync(CancellationToken.None);

        _postRepositoryMock.Verify(
            x => x.CreateAsync(It.Is<Post>(p => p.Status == PostStatus.Failed)),
            Times.Once);
    }

    [Fact]
    public async Task GenerateWeeklyPostAsync_WhenInstagramPublishFails_ShouldNotReturnError()
    {
        SetupHappyPath(imageCount: 1);

        _instagramPublisherMock
            .Setup(x => x.PublishCarouselAsync(It.IsAny<List<string>>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Instagram error"));

        var result = await _service.GenerateWeeklyPostAsync(CancellationToken.None);

        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateWeeklyPostAsync_WhenInstagramPublishFails_ShouldCallUpdateAsync()
    {
        SetupHappyPath(imageCount: 1);

        _instagramPublisherMock
            .Setup(x => x.PublishCarouselAsync(It.IsAny<List<string>>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Instagram error"));

        await _service.GenerateWeeklyPostAsync(CancellationToken.None);

        _postRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Post>()), Times.Once);
    }

    [Fact]
    public async Task GenerateWeeklyPostAsync_WhenInstagramPublishFails_ShouldReturnPostId()
    {
        SetupHappyPath(imageCount: 1);

        _instagramPublisherMock
            .Setup(x => x.PublishCarouselAsync(It.IsAny<List<string>>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Instagram error"));

        var result = await _service.GenerateWeeklyPostAsync(CancellationToken.None);

        result.Value.Should().NotBe(Guid.Empty);
    }

    private void SetupHappyPath(int imageCount)
    {
        var events = Enumerable.Range(0, imageCount)
            .Select(i => new ChaynsEvent { Title = $"Event {i}", Location = "Ort", StartDate = DateTime.UtcNow })
            .ToList();

        var images = Enumerable.Range(0, imageCount)
            .Select(_ => new byte[] { 1, 2, 3 })
            .ToList();

        _chaynsApiClientMock
            .Setup(x => x.GetEventsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(events);

        _imageGeneratorMock
            .Setup(x => x.GenerateWeeklyImages(It.IsAny<List<ChaynsEvent>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(images);

        _storageServiceMock
            .Setup(x => x.UploadImageAsync(It.IsAny<byte[]>(), It.IsAny<string>()))
            .ReturnsAsync("https://storage.example.com/image.png");

        _postRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<Post>()))
            .ReturnsAsync((Post p) => p);

        _instagramPublisherMock
            .Setup(x => x.PublishCarouselAsync(It.IsAny<List<string>>(), It.IsAny<string>()))
            .ReturnsAsync("instagram-media-id-123");
    }
}
