using Pressroom.Review.Interceptors;
using Pressroom.Review.Services;

namespace Pressroom.Review;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddSingleton<LoggingInterceptor>();
        builder.Services.AddSingleton<CorrelationIdInterceptor>();

        builder.Services.AddGrpc(options =>
        {
            options.Interceptors.Add<CorrelationIdInterceptor>();
            options.Interceptors.Add<LoggingInterceptor>();
        });

        builder.Services.AddAuthorization();
        builder.Services.AddOpenApi();

        var app = builder.Build();
        if (app.Environment.IsDevelopment()) app.MapOpenApi();

        app.MapGrpcService<ReviewGrpcService>();

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.Run();
    }
}