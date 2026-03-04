using Grpc.Core;
using Pressroom.Contracts;

namespace Pressroom.Gateway;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddAuthorization();
        builder.Services.AddOpenApi();

        builder.Services.AddGrpcClient<EditorialGrpc.EditorialGrpcClient>(options =>
        {
            var url = builder.Configuration.GetValue<string>("EditorialUrl")
                      ?? throw new ArgumentNullException("Editorial url is required.");

            options.Address = new Uri(url);
        });
        builder.Services.AddGrpcClient<ReviewGrpc.ReviewGrpcClient>(options =>
        {
            var url = builder.Configuration.GetValue<string>("ReviewUrl")
                      ?? throw new ArgumentNullException("Review url is required");

            options.Address = new Uri(url);
        });

        var app = builder.Build();

        app.Use(async (context, next) =>
        {
            try
            {
                await next();
            }
            catch (RpcException ex)
            {
                context.Response.StatusCode = ex.StatusCode switch
                {
                    StatusCode.NotFound => 404,
                    StatusCode.InvalidArgument => 400,
                    StatusCode.Unauthenticated => 401,
                    StatusCode.PermissionDenied => 403,
                    _ => 500
                };

                await context.Response.WriteAsJsonAsync(new { error = ex.Status.Detail });
            }
        });

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseSwaggerUI(options => { options.SwaggerEndpoint("/openapi/v1.json", "Pressroom Gateway API v1"); });
        }

        app.MapArticleEndpoints();

        app.UseHttpsRedirection();
        app.UseAuthorization();

        app.Run();
    }
}