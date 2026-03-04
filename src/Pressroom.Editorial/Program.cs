using Pressroom.Contracts;
using Pressroom.Editorial.Services;

namespace Pressroom.Editorial;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddAuthorization();
        builder.Services.AddOpenApi();

        builder.Services.AddGrpcClient<ReviewGrpc.ReviewGrpcClient>(options =>
        {
            var reviewUrl = builder.Configuration.GetValue<string>("ReviewUrl")
                            ?? throw new NullReferenceException("Review url is requied.");

            options.Address = new Uri(reviewUrl);
        });
        builder.Services.AddGrpc(options => { });
        var app = builder.Build();

        if (app.Environment.IsDevelopment()) app.MapOpenApi();

        app.MapGrpcService<EditorialGrpcService>();

        app.UseHttpsRedirection();
        app.UseAuthorization();

        app.Run();
    }
}