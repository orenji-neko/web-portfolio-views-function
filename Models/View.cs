using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;

namespace PortfolioWebViews.Models;

[DynamoDBTable("PortfolioViews")]
public class View
{
    [DynamoDBHashKey("viewId")]
    public string ViewId { get; set; } = string.Empty;

    [DynamoDBRangeKey("viewedAt")]
    public string ViewedAt { get; set; } = string.Empty;

    [DynamoDBProperty("ipAddress")]
    public string IpAddress { get; set; } = string.Empty;

    [DynamoDBProperty("userAgent")]
    public string UserAgent { get; set; } = string.Empty;
}