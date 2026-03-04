using Grpc.Core;
using Grpc.Core.Interceptors;

namespace Pressroom.Review.Interceptors;

public sealed class CorrelationIdInterceptor : Interceptor
{
    private const string CorrelationHeader = "x-correlation-id";

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request,
        ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        return await ExecuteWithCorrelactionId(() => continuation.Invoke(request, context), context);
    }

    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream, ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        return await ExecuteWithCorrelactionId(() => continuation(requestStream, context), context);
    }

    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream, ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await ExecuteWithCorrelactionId(() => continuation.Invoke(requestStream, responseStream, context), context);
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(TRequest request,
        IServerStreamWriter<TResponse> responseStream, ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await ExecuteWithCorrelactionId(() => continuation.Invoke(request, responseStream, context), context);
    }


    private async Task<T> ExecuteWithCorrelactionId<T>(Func<Task<T>> action, ServerCallContext context)
    {
        if (context.RequestHeaders.GetValue(CorrelationHeader) is null)
        {
            var newCorrelationId = Guid.CreateVersion7().ToString();
            context.RequestHeaders.Add(CorrelationHeader, newCorrelationId);

            await context.WriteResponseHeadersAsync(new Metadata
            {
                { CorrelationHeader, newCorrelationId }
            });
        }

        return await action();
    }

    private async Task ExecuteWithCorrelactionId(Func<Task> action, ServerCallContext context)
    {
        if (context.RequestHeaders.GetValue(CorrelationHeader) is null)
            context.RequestHeaders.Add(CorrelationHeader, $"{Guid.CreateVersion7()}");

        await action();
    }
}