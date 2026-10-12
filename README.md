# Portfolio Web Views

A small AWS Lambda function, written in .NET 10, that records a visit each time someone opens my portfolio website. Each request is saved as one item in a DynamoDB table.

## How it works

```
Portfolio site ──HTTP──▶ API Gateway (HTTP API) ──▶ Lambda: portfolio-web-views ──▶ DynamoDB: PortfolioViews
```

1. The portfolio site sends a request to an API Gateway HTTP API endpoint when the page loads.
2. API Gateway calls the function with a payload format 2.0 event (`APIGatewayHttpApiV2ProxyRequest`).
3. The function reads the caller's source IP and user agent and writes a new `View` item to DynamoDB.
4. It returns `204 No Content`.

The function doesn't check the HTTP method or path, so every request routed to it counts as one view.

## Data model

Table: **`PortfolioViews`**

| Attribute   | Type   | Key           | Description                                            |
|-------------|--------|---------------|--------------------------------------------------------|
| `viewId`    | String | Partition key | Random GUID generated per request                      |
| `viewedAt`  | String | Sort key      | UTC timestamp in ISO 8601 round-trip format (`"o"`)    |
| `ipAddress` | String |               | Source IP reported by API Gateway                      |
| `userAgent` | String |               | Browser user agent, truncated to 512 characters        |

## Tech stack

- .NET 10 (`dotnet10` managed Lambda runtime, x86_64)
- [Amazon.Lambda.APIGatewayEvents](https://www.nuget.org/packages/Amazon.Lambda.APIGatewayEvents) for the HTTP API event types
- [AWSSDK.DynamoDBv2](https://www.nuget.org/packages/AWSSDK.DynamoDBv2) object persistence model (`DynamoDBContext`)
- ReadyToRun publishing to reduce cold start time

## Project structure

```
.
├── Function.cs                       # Lambda handler
├── Models/
│   └── View.cs                       # DynamoDB item model
├── PortfolioWebViews.csproj
└── aws-lambda-tools-defaults.json    # Deployment defaults for `dotnet lambda`
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Amazon.Lambda.Tools for the .NET CLI:
  ```bash
  dotnet tool install -g Amazon.Lambda.Tools
  ```
- An AWS CLI profile named `personal`, or change `profile` in [aws-lambda-tools-defaults.json](aws-lambda-tools-defaults.json)

## AWS setup

Do this once before the first deploy.

### 1. DynamoDB table

Create the table in `ap-southeast-1`:

```bash
aws dynamodb create-table \
  --table-name PortfolioViews \
  --attribute-definitions AttributeName=viewId,AttributeType=S AttributeName=viewedAt,AttributeType=S \
  --key-schema AttributeName=viewId,KeyType=HASH AttributeName=viewedAt,KeyType=RANGE \
  --billing-mode PAY_PER_REQUEST \
  --region ap-southeast-1 --profile personal
```

### 2. IAM execution role

The function's role needs:

- The `AWSLambdaBasicExecutionRole` managed policy, for CloudWatch Logs
- Write access to the table:

```json
{
  "Effect": "Allow",
  "Action": ["dynamodb:PutItem", "dynamodb:UpdateItem"],
  "Resource": "arn:aws:dynamodb:ap-southeast-1:<account-id>:table/PortfolioViews"
}
```

The context is built with `DisableFetchingTableMetadata = true`, so the function never calls `DescribeTable` and doesn't need that permission.

### 3. API Gateway

1. Create an **HTTP API** and add a route such as `POST /views` with a Lambda integration pointing to `portfolio-web-views`. Use payload format version **2.0**.
2. Configure CORS on the API to allow your portfolio's origin. The function doesn't set CORS headers itself.

## Build and deploy

```bash
# Build
dotnet build

# Deploy (settings are read from aws-lambda-tools-defaults.json)
dotnet lambda deploy-function --function-role <role-name-or-arn>
```

The defaults file sets the function name (`portfolio-web-views`), region (`ap-southeast-1`), memory (512 MB), timeout (30 s), and handler. You only need `--function-role` on the first deploy, when the function is created.

## Testing

Invoke the deployed function directly with a minimal HTTP API event:

```bash
dotnet lambda invoke-function portfolio-web-views \
  --payload '{"requestContext":{"http":{"sourceIp":"127.0.0.1","userAgent":"manual-test"}}}'
```

Or call it through API Gateway:

```bash
curl -i -X POST https://<api-id>.execute-api.ap-southeast-1.amazonaws.com/views
```

A successful call returns `204` and adds one item to `PortfolioViews`.

## Calling it from the website

Send the request once per page load. `keepalive` lets the request finish even if the visitor navigates away right away:

```js
fetch("https://<api-id>.execute-api.ap-southeast-1.amazonaws.com/views", {
  method: "POST",
  keepalive: true,
}).catch(() => {});
```

## Reading the data

Count the total number of views:

```bash
aws dynamodb scan --table-name PortfolioViews --select COUNT \
  --region ap-southeast-1 --profile personal
```

## Notes

- **No deduplication or bot filtering.** Every request is stored, so refreshes, crawlers, and uptime checks all count as views.
- **IP addresses are stored as-is.** Depending on where your visitors are, this can count as personal data. Consider hashing or truncating the IP, or adding a DynamoDB TTL attribute so old items expire.
- **Counting uses a full scan.** `Scan` reads the whole table. That's fine at portfolio scale, but a separate counter item would be cheaper if traffic grows.
