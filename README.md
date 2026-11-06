# Asin .NET SDK

The official .NET SDK for the [Asin](https://www.asin.dev) Amazon price tracking API.

Track Amazon prices and products with a scheduled watchlist, price history, offers and alerts.

The SDK exposes **11 typed async methods**, one per documented endpoint (with `required` properties checked at compile time), plus a generic `RequestAsync`.

## Requirements

- .NET 8 or newer

## Installation

```sh
dotnet add package Asin
```

## Get your API key

[Sign up for Asin](https://www.asin.dev/signup) or sign in, then copy your API key from the [dashboard](https://www.asin.dev/app). `new AsinClient()` reads the `ASIN_API_KEY` environment variable.

## Quick start

```csharp
using Asin;

using var client = new AsinClient();
var response = await client.SearchAmazonAsync(new SearchAmazonParams { Query = "wireless earbuds" });
Console.WriteLine(response.Json);
```

Parameter classes mirror the [API reference](https://www.asin.dev/docs). Use `response.Deserialize<MyType>()` to map the JSON onto your own types.

## HTTP methods

Each method uses the HTTP verb of its endpoint. `GET` and `DELETE` send parameters in the query string; `POST` and `PATCH` send them as a JSON body, except `wait` on `/products/{id}/check`, which stays in the query string. Null values are never sent, so `target_price` cannot be removed through the SDK yet.

## Any endpoint

```csharp
var response = await client.RequestAsync("GET", "/account");
```

## Errors

```csharp
try
{
    await client.SearchAmazonAsync(new SearchAmazonParams { Query = "wireless earbuds" });
}
catch (AsinApiException exception)
{
    Console.Error.WriteLine($"{exception.StatusCode}: {exception.Message}");
}
```

| Exception | When |
| --- | --- |
| `AsinConfigurationException` | Missing API key, invalid options or unsupported method. |
| `AsinSerializationException` | A required parameter is missing or a value can't be sent. |
| `AsinTransportException` | Network failure or timeout. |
| `AsinApiException` | Non-2xx status (`StatusCode`, `ErrorMessage`, `ResponseBody`, `RawResponse`). |
| `AsinResponseDecodingException` | A success response could not be decoded. |

All of them derive from `AsinException`. Caller cancellation surfaces as `OperationCanceledException`.

## Retries

Requests are retried up to 3 times after a network error, a `408`, `429`, `500`, `502`, `503` or `504`. Other statuses, such as `400`, `401`, `402` or `404`, and client-side timeouts are not retried. The delay starts at 1 second and grows 1.5× per retry (capped at 30 seconds); a `Retry-After` header wins.

```csharp
using var client = new AsinClient(new AsinClientOptions { MaxRetries = 5, RetryDelay = TimeSpan.FromMilliseconds(500) });
await client.SearchAmazonAsync(new SearchAmazonParams { Query = "wireless earbuds" }, new RequestOptions { MaxRetries = 0 }); // one request without retries
```

## Batch requests

`BatchAsync` runs a function per item with at most 10 in flight (`concurrency`) and returns one `BatchResult` per item, in input order. A failing item never stops the others.

```csharp
string[] inputs = { "wireless earbuds", "mechanical keyboard", "usb-c hub" };
var results = await client.BatchAsync(inputs, (value, ct) =>
    client.SearchAmazonAsync(new SearchAmazonParams { Query = value }, cancellationToken: ct), concurrency: 5);
foreach (var result in results)
{
    if (result.Ok)
    {
        Console.WriteLine(result.Value!.Json);
    }
    else
    {
        Console.Error.WriteLine($"{inputs[result.Index]}: {result.Error!.Message}");
    }
}
```

| Constant | Default | Description |
| --- | --- | --- |
| `AsinClient.DefaultConcurrency` | `10` | Tasks run at the same time by batch |
| `AsinClient.DefaultTimeout` | `TimeSpan.FromSeconds(120)` | Request timeout |
| `AsinClient.MaxRetries` | `3` | Retry attempts after a network error, a 408/429/5xx |
| `AsinClient.RetryBackoffFactor` | `1.5` | Exponential backoff multiplier |
| `AsinClient.DefaultRetryDelay` | `TimeSpan.FromSeconds(1)` | Delay before the first retry |

## Configuration

```csharp
using var client = new AsinClient(new AsinClientOptions
{
    ApiKey = Environment.GetEnvironmentVariable("ASIN_API_KEY"),
    Timeout = TimeSpan.FromSeconds(60), // default 120 s
    HttpClient = myHttpClient,           // optional, you keep ownership
});
```

## Endpoints

11 methods, generated from the [OpenAPI document](https://www.asin.dev/openapi.json).

| Method | HTTP | Description |
| --- | --- | --- |
| `CheckProductAsync` | POST `/products/{id}/check` | Asks for a check right away (1 credit), at most once per 5 minutes; earlier calls return the product unchanged. A paused product has to be… |
| `DeleteProductAsync` | DELETE `/products/{id}` | Removes the product and its price history. |
| `FetchOffersAsync` | POST `/offers` | Reads the offers on Amazon for 1 credit, refunded if it doesn't answer. A read made in the last 3 hours, by anyone, is returned instead, fo… |
| `GetAccountAsync` | GET `/account` | Your plan, the credits left (monthly allowance and packs), how many products you track and the credits they use per month. |
| `GetAsinHistoryAsync` | GET `/history` | Every price Asin read for an ASIN on a marketplace over the period, whoever tracks it, oldest first, with the lowest, highest and average o… |
| `GetOffersAsync` | GET `/offers` | Who sells the product and at what price, as last read, cheapest first, with Amazon's own price and the cheapest new and used offers (used i… |
| `GetProductAsync` | GET `/products/{id}` | The product's latest state and every price read of the period, oldest first. The history covers every read of this ASIN on this marketplace… |
| `ListProductsAsync` | GET `/products` | Your watchlist, newest first, with each product's latest state and its recent prices (trend). Filter with asin (and marketplace) to know if… |
| `SearchAmazonAsync` | GET `/search` | Amazon search results (up to 60, sponsored included), to find the ASIN of a product. Free, limited to 30 searches per hour. |
| `TrackProductAsync` | POST `/products` | Adds an ASIN, an Amazon link or a short link (amzn.to, a.co…) to your watchlist. The first check runs in the background within seconds (che… |
| `UpdateProductAsync` | PATCH `/products/{id}` | Target price, drop alerts, check interval, or pause. Send only what changes. |

## All Asin SDKs

| SDK | Package | Install |
| --- | --- | --- |
| Node.js | [`@asindev/sdk`](https://www.npmjs.com/package/@asindev/sdk) | `npm install @asindev/sdk` |
| Python | [`asindotdev`](https://pypi.org/project/asindotdev/) | `pip install asindotdev` |
| Go | [`asin-go`](https://pkg.go.dev/github.com/asindotdev/asin-go) | `go get github.com/asindotdev/asin-go` |
| .NET | [`Asin`](https://www.nuget.org/packages/Asin) | `dotnet add package Asin` |
| Ruby | [`asindotdev`](https://rubygems.org/gems/asindotdev) | `gem install asindotdev` |
| PHP | [`asindotdev/sdk`](https://packagist.org/packages/asindotdev/sdk) | `composer require asindotdev/sdk` |
| Java | [`asin-java`](https://github.com/asindotdev/asin-java) | build from source, see its README |
| CLI | [`@asindev/cli`](https://www.npmjs.com/package/@asindev/cli) | `npm install -g @asindev/cli` |
| MCP server | [`@asindev/mcp`](https://www.npmjs.com/package/@asindev/mcp) | `npx -y @asindev/mcp` |

## License

MIT — see [LICENSE](LICENSE).
