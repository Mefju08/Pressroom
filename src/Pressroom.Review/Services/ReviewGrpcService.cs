using System.Collections.Concurrent;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Pressroom.Contracts;

namespace Pressroom.Review.Services;

public class ReviewGrpcService : ReviewGrpc.ReviewGrpcBase
{
    private static readonly ConcurrentDictionary<ByteString, ConcurrentQueue<ReviewStatusUpdate>> _db = new();

    public override async Task SubmitForReview(IAsyncStreamReader<ArticleMessage> requestStream,
        IServerStreamWriter<ReviewStatusUpdate> responseStream, ServerCallContext context)
    {
        var ct = context.CancellationToken;

        if (!await requestStream.MoveNext(ct))
            return;

        var currentArticle = requestStream.Current;
        var history = _db.GetOrAdd(currentArticle.ArticleId, _ => []);

        var pending = BuildStatus(currentArticle.ArticleId, ReviewStatus.Pending);
        history.Enqueue(pending);
        await responseStream.WriteAsync(pending, ct);

        await Task.Delay(1000, ct);

        var inReview = BuildStatus(currentArticle.ArticleId, ReviewStatus.InReview);
        history.Enqueue(inReview);
        await responseStream.WriteAsync(inReview, ct);

        await Task.Delay(2000, ct);

        var changesRequested = BuildStatus(currentArticle.ArticleId, ReviewStatus.ChangesRequested,
            "Changes requested.");
        history.Enqueue(changesRequested);
        await responseStream.WriteAsync(changesRequested, ct);

        var hasCorrections = await requestStream.MoveNext(ct);
        if (!hasCorrections)
            return;

        var correctedArticle = requestStream.Current;

        var finalStatus = Random.Shared.Next(0, 10) < 5
            ? ReviewStatus.Approved
            : ReviewStatus.Rejected;

        var final = BuildStatus(correctedArticle.ArticleId, finalStatus);
        history.Enqueue(final);
        await responseStream.WriteAsync(final, ct);
    }

    private static ReviewStatusUpdate BuildStatus(ByteString articleId, ReviewStatus status, string comment = "")
    {
        return new ReviewStatusUpdate
        {
            ArticleId = articleId,
            Status = status,
            UpdatedAt = DateTime.UtcNow.ToTimestamp(),
            ReviewerComment = comment
        };
    }


    public override async Task GetReviewHistory(GetReviewHistoryRequest request,
        IServerStreamWriter<ReviewStatusUpdate> responseStream, ServerCallContext context)
    {
        var ct = context.CancellationToken;

        if (request.ArticleId.IsEmpty)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "ArticleId cannot be empty."));

        if (!_db.TryGetValue(request.ArticleId, out var history))
            throw new RpcException(new Status(StatusCode.NotFound, "Article not found."));

        foreach (var status in history)
        {
            ct.ThrowIfCancellationRequested();

            await responseStream.WriteAsync(status, ct);
            await Task.Delay(1000, ct);
        }
    }


    public override async Task<ReviewStatusUpdate> GetFinalStatus(GetFinalStatusRequest request,
        ServerCallContext context)
    {
        if (request.ArticleId.IsEmpty)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "ArticleId cannot be empty."));

        if (!_db.TryGetValue(request.ArticleId, out var history))
            throw new RpcException(new Status(StatusCode.NotFound, "Article not found."));

        var finalStats = history.LastOrDefault();
        if (finalStats is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Article history is empty."));

        return history.Last();
    }
}