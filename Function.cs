using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using PortfolioWebViews.Models;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace PortfolioWebViews;

public class Function
{
    private static readonly IAmazonDynamoDB Client = new AmazonDynamoDBClient();

    private static readonly IDynamoDBContext Db = new DynamoDBContextBuilder()
        .WithDynamoDBClient(() => Client)
        .ConfigureContext(c => c.DisableFetchingTableMetadata = true)
        .Build();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(
        APIGatewayHttpApiV2ProxyRequest request,
        ILambdaContext context)
    {
        var view = new View()
        {
            ViewId = Guid.NewGuid().ToString(),
            ViewedAt = DateTime.UtcNow.ToString("o"),
            IpAddress = request.RequestContext.Http.SourceIp,
            UserAgent = Truncate(request.RequestContext.Http.UserAgent, 512)
        };

        await Db.SaveAsync(view);
        return new APIGatewayHttpApiV2ProxyResponse { StatusCode = 204 };
    }

    private static string Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? string.Empty : value.Length <= max ? value : value[..max];
}
