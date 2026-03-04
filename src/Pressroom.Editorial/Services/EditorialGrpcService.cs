using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Pressroom.Contracts;

namespace Pressroom.Editorial.Services;

public class EditorialGrpcService(
    ReviewGrpc.ReviewGrpcClient reviewClient) : EditorialGrpc.EditorialGrpcBase
{
    public override async Task<SubmitArticleResponse> SubmitArticle(SubmitArticleRequest request,
        ServerCallContext context)
    {
        if (string.IsNullOrEmpty(request.Title) || string.IsNullOrEmpty(request.Content) || request.AuthorId.IsEmpty)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Title, Content and AuthorId are required"));

        var articleId = ByteString.CopyFrom(Guid.CreateVersion7().ToByteArray());
        var articleMessage = new ArticleMessage
        {
            ArticleId = articleId,
            Title = request.Title,
            Content = request.Content,
            AuthorId = request.AuthorId
        };

        var ct = context.CancellationToken;

        using var reviewCall = reviewClient.SubmitForReview(deadline: context.Deadline, cancellationToken: ct);
        await reviewCall.RequestStream.WriteAsync(articleMessage, ct);

        await foreach (var review in reviewCall.ResponseStream.ReadAllAsync(ct))
            switch (review.Status)
            {
                case ReviewStatus.ChangesRequested:
                    articleMessage.Content += "[revised]";
                    await reviewCall.RequestStream.WriteAsync(articleMessage, ct);
                    break;
                case ReviewStatus.Approved or ReviewStatus.Rejected:
                    break;
            }

        await reviewCall.RequestStream.CompleteAsync();
        return new SubmitArticleResponse
        {
            ArticleId = articleId,
            SubmittedAt = DateTimeOffset.UtcNow.ToTimestamp()
        };
    }

    public override async Task<ReviewStatusUpdate> GetArticleStatus(GetArticleStatusRequest request,
        ServerCallContext context)
    {
        if (request.ArticleId.IsEmpty)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Article id is required."));

        return await reviewClient.GetFinalStatusAsync(
            new GetFinalStatusRequest { ArticleId = request.ArticleId },
            cancellationToken: context.CancellationToken,
            deadline: context.Deadline);
    }

    public override async Task GetArticleHistory(GetArticleHistoryRequest request,
        IServerStreamWriter<ReviewStatusUpdate> responseStream, ServerCallContext context)
    {
        if (request.ArticleId.IsEmpty)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Article id is required."));

        var ct = context.CancellationToken;
        using var getHistoryCall = reviewClient.GetReviewHistory(
            new GetReviewHistoryRequest { ArticleId = request.ArticleId },
            cancellationToken: ct,
            deadline: context.Deadline);

        await foreach (var status in getHistoryCall.ResponseStream.ReadAllAsync(ct))
            await responseStream.WriteAsync(status, ct);
    }
}