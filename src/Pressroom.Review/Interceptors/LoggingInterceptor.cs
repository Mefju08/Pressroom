using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace Pressroom.Review.Interceptors;

public sealed class LoggingInterceptor(
    ILogger<LoggingInterceptor> logger) : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request,
        ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        return await ExecuteWithLogging(() => continuation(request, context), context.Method);
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(TRequest request,
        IServerStreamWriter<TResponse> responseStream, ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await ExecuteWithLogging(() => continuation.Invoke(request, responseStream, context), context.Method);
    }

    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream, ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        return await ExecuteWithLogging(() => continuation.Invoke(requestStream, context), context.Method);
    }

    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream, ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await ExecuteWithLogging(() => continuation.Invoke(requestStream, responseStream, context), context.Method);
    }

    private async Task<T> ExecuteWithLogging<T>(Func<Task<T>> action, string method)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await action();
            sw.Stop();
            logger.LogInformation(
                "Method: {Method} | Status: OK | Time: {ElapsedMs}ms",
                method, sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex,
                "Method: {Method} | Status: ERROR | Time: {ElapsedMs}ms",
                method, sw.ElapsedMilliseconds);
            throw;
        }
    }

    private async Task ExecuteWithLogging(Func<Task> action, string method)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await action();
            sw.Stop();
            logger.LogInformation(
                "Method: {Method} | Status: OK | Time: {ElapsedMs}ms",
                method, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex,
                "Method: {Method} | Status: ERROR | Time: {ElapsedMs}ms",
                method, sw.ElapsedMilliseconds);
            throw;
        }
    }
}