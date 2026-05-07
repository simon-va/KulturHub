using Dapper;
using KulturHub.Application.Ports;
using KulturHub.Domain.Entities;
using KulturHub.Domain.Enums;
using KulturHub.Domain.Interfaces;

namespace KulturHub.Infrastructure.Persistence.Repositories;

public class PostRepository(IDbConnectionFactory connectionFactory) : IPostRepository
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public async Task<Post> CreateAsync(Post post)
    {
        const string insertPost = """
            INSERT INTO posts (id, type, status, caption, error_message, created_at, published_at)
            VALUES (@Id, @Type, @Status, @Caption, @ErrorMessage, @CreatedAt, @PublishedAt)
            """;

        const string insertImage = """
            INSERT INTO post_images (id, post_id, storage_url, sort_order, created_at)
            VALUES (@Id, @PostId, @StorageUrl, @SortOrder, @CreatedAt)
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();
        using var transaction = await connection.BeginTransactionAsync();

        await connection.ExecuteAsync(insertPost, new
        {
            post.Id,
            Type = (int)post.Type,
            Status = (int)post.Status,
            post.Caption,
            post.ErrorMessage,
            post.CreatedAt,
            post.PublishedAt
        }, transaction);

        foreach (var image in post.Images)
        {
            await connection.ExecuteAsync(insertImage, new
            {
                image.Id,
                image.PostId,
                image.StorageUrl,
                image.SortOrder,
                image.CreatedAt
            }, transaction);
        }

        await transaction.CommitAsync();
        return post;
    }

    public async Task UpdateAsync(Post post)
    {
        const string sql = """
            UPDATE posts
            SET type = @Type, status = @Status, caption = @Caption,
                error_message = @ErrorMessage, published_at = @PublishedAt,
                instagram_media_id = @InstagramMediaId
            WHERE id = @Id
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await connection.ExecuteAsync(sql, new
        {
            post.Id,
            Type = (int)post.Type,
            Status = (int)post.Status,
            post.Caption,
            post.ErrorMessage,
            post.PublishedAt,
            post.InstagramMediaId
        });
    }

    public async Task<Post?> GetByIdAsync(Guid id)
    {
        const string selectPost = """
            SELECT id, type, status, caption,
                   error_message      AS ErrorMessage,
                   created_at         AS CreatedAt,
                   published_at       AS PublishedAt,
                   instagram_media_id AS InstagramMediaId
            FROM posts
            WHERE id = @Id
            """;

        const string selectImages = """
            SELECT id, post_id AS PostId, storage_url AS StorageUrl,
                   sort_order AS SortOrder, created_at AS CreatedAt
            FROM post_images
            WHERE post_id = @Id
            ORDER BY sort_order
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        var row = await connection.QuerySingleOrDefaultAsync<PostRow>(selectPost, new { Id = id });
        if (row is null)
            return null;

        var post = Post.Reconstitute(
            row.Id, (PostType)row.Type, (PostStatus)row.Status,
            row.Caption, row.ErrorMessage, row.CreatedAt, row.PublishedAt, row.InstagramMediaId);

        var images = await connection.QueryAsync<PostImage>(selectImages, new { Id = id });
        post.AddImages(images);

        return post;
    }

    private sealed record PostRow(
        Guid Id, int Type, int Status, string Caption,
        string? ErrorMessage, DateTime CreatedAt, DateTime? PublishedAt, string? InstagramMediaId);
}
