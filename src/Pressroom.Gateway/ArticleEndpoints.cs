using Google.Protobuf;
using Grpc.Core;
using Pressroom.Contracts;

namespace Pressroom.Gateway;

public static class ArticleEndpoints
{
    public static void MapArticleEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/articles", async (
            CreateArticleRequest request,
            EditorialGrpc.EditorialGrpcClient editorialClient,
            CancellationToken ct) =>
        {
            var response = await editorialClient.SubmitArticleAsync(new SubmitArticleRequest
                {
                    Title = request.Title,
                    Content = request.Content,
                    AuthorId = ByteString.CopyFrom(request.AuthorId.ToByteArray())
                },
                deadline: DateTime.UtcNow.AddSeconds(30),
                cancellationToken: ct);

            return Results.Ok(new ArticleCreatedResponse
            {
                ArticleId = new Guid(response.ArticleId.ToByteArray()),
                SubmittedAt = response.SubmittedAt.ToDateTime()
            });
        });

        app.MapGet("/articles/{id}/status", async (
            string id,
            EditorialGrpc.EditorialGrpcClient editorialClient,
            CancellationToken ct) =>
        {
            var response = await editorialClient.GetArticleStatusAsync(
                new GetArticleStatusRequest
                {
                    ArticleId = ByteString.CopyFrom(Guid.Parse(id).ToByteArray())
                },
                deadline: DateTime.UtcNow.AddSeconds(5),
                cancellationToken: ct);

            return Results.Ok(new ReviewStatusUpdateResponse
            {
                ArticleId = new Guid(response.ArticleId.ToByteArray()),
                Status = response.Status,
                ReviewerComment = response.ReviewerComment,
                UpdatedAt = response.UpdatedAt.ToDateTime()
            });
        });

        app.MapGet("/articles/{id}/history", async (
            string id,
            EditorialGrpc.EditorialGrpcClient editorialClient,
            CancellationToken ct) =>
        {
            var historyCall = editorialClient.GetArticleHistory(
                new GetArticleHistoryRequest
                {
                    ArticleId = ByteString.CopyFrom(Guid.Parse(id).ToByteArray())
                },
                deadline: DateTime.UtcNow.AddSeconds(10),
                cancellationToken: ct);

            var history = new List<ReviewStatusUpdateResponse>();

            await foreach (var status in historyCall.ResponseStream.ReadAllAsync(ct))
                history.Add(new ReviewStatusUpdateResponse
                {
                    ArticleId = new Guid(status.ArticleId.ToByteArray()),
                    Status = status.Status,
                    ReviewerComment = status.ReviewerComment,
                    UpdatedAt = status.UpdatedAt.ToDateTime()
                });

            return Results.Ok(history);
        });
    }
}

public class CreateArticleRequest
{
    public required string Title { get; init; }
    public required string Content { get; init; }
    public required Guid AuthorId { get; init; }
}

public class ArticleCreatedResponse
{
    public required Guid ArticleId { get; init; }
    public required DateTime SubmittedAt { get; init; }
}

public class ReviewStatusUpdateResponse
{
    public required Guid ArticleId { get; init; }
    public required ReviewStatus Status { get; init; }
    public required string? ReviewerComment { get; init; }
    public required DateTime UpdatedAt { get; init; }
}