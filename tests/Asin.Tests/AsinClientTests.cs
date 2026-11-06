using System.Net;
using System.Text;
using System.Text.Json;
using Asin;
using Xunit;

namespace Asin.Tests;

public sealed class AsinClientTests
{
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string? Body)> Requests { get; } = new();

        public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; set; } = _ => Json(200, "{\"ok\":true}");

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request, body));
            return Responder(request);
        }
    }

    private static HttpResponseMessage Json(int status, string body) => new((HttpStatusCode)status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };

    private readonly RecordingHandler _handler = new();
    private readonly AsinClient _client;

    public AsinClientTests()
    {
        _client = new AsinClient(new AsinClientOptions
        {
            ApiKey = "test-key",
            HttpClient = new HttpClient(_handler),
            RetryDelay = TimeSpan.FromMilliseconds(1),
        });
    }

    private AsinClient ClientWithBaseUrl(string baseUrl) =>
        new(new AsinClientOptions { ApiKey = "k", BaseUrl = baseUrl, HttpClient = new HttpClient(_handler) });

    [Fact]
    public void RequiresApiKey()
    {
        Environment.SetEnvironmentVariable("ASIN_API_KEY", null);
        Assert.Throws<AsinConfigurationException>(() => new AsinClient());
        Assert.Throws<AsinConfigurationException>(() => new AsinClient(new AsinClientOptions { ApiKey = "   " }));
        Assert.Throws<AsinConfigurationException>(() => new AsinClient(new AsinClientOptions { ApiKey = "k", BaseUrl = "ftp://x" }));
    }

    [Fact]
    public async Task FeaturedOperation()
    {
        _handler.Responder = _ => Json(200, "{\"hello\":\"world\"}");
        var response = await _client.SearchAmazonAsync(new SearchAmazonParams { Query = "wireless earbuds" });
        Assert.Equal("world", response.Json.GetProperty("hello").GetString());
        var request = _handler.Requests[0].Request;
        Assert.Equal("GET", request.Method.Method);
        Assert.Equal("https://www.asin.dev/api/v1/search", request.RequestUri!.GetLeftPart(UriPartial.Path));
        Assert.Equal("Bearer test-key", request.Headers.GetValues("Authorization").Single());
        Assert.StartsWith("asin-dotnet/", string.Join(" ", request.Headers.GetValues("User-Agent")));
    }

    [Fact]
    public async Task QuerySerialization()
    {
        using var client = ClientWithBaseUrl("https://example.test/");
        await client.RequestAsync("GET", "/anything", new Dictionary<string, object?>
        {
            ["text"] = "a b&c",
            ["flag"] = false,
            ["count"] = 0,
            ["ratio"] = 1.5,
            ["whole"] = 2.0,
            ["list"] = new[] { "x", "y" },
            ["nothing"] = null,
        });
        Assert.Equal(
            "https://example.test/anything?text=a%20b%26c&flag=false&count=0&ratio=1.5&whole=2&list=x&list=y",
            _handler.Requests[0].Request.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task PostSendsJsonBody()
    {
        await _client.RequestAsync("POST", "/anything", new Dictionary<string, object?> { ["query"] = "x", ["page"] = 2, ["gone"] = null });
        var (request, body) = _handler.Requests[0];
        Assert.Equal("POST", request.Method.Method);
        Assert.Equal("application/json", request.Content!.Headers.ContentType!.MediaType);
        Assert.Equal("{\"query\":\"x\",\"page\":2}", body);
        Assert.Equal(string.Empty, request.RequestUri!.Query);
    }

    [Fact]
    public async Task WaitLocationDependsOnTheEndpoint()
    {
        await _client.RequestAsync("POST", "/products/{id}/check", new Dictionary<string, object?> { ["id"] = "p1", ["wait"] = true });
        Assert.Equal("?wait=true", _handler.Requests[0].Request.RequestUri!.Query);
        Assert.Equal("{}", _handler.Requests[0].Body);
        await _client.RequestAsync("POST", "/products", new Dictionary<string, object?> { ["wait"] = true });
        Assert.Equal("", _handler.Requests[1].Request.RequestUri!.Query);
        Assert.Equal("{\"wait\":true}", _handler.Requests[1].Body);
    }

    [Fact]
    public async Task PatchAndDelete()
    {
        await _client.RequestAsync("PATCH", "/products/{id}", new Dictionary<string, object?> { ["id"] = "p1", ["paused"] = true });
        Assert.Equal("PATCH", _handler.Requests[0].Request.Method.Method);
        Assert.Equal("{\"paused\":true}", _handler.Requests[0].Body);
        await _client.RequestAsync("DELETE", "/products/{id}", new Dictionary<string, object?> { ["id"] = "p1" });
        Assert.Equal("DELETE", _handler.Requests[1].Request.Method.Method);
        Assert.True(string.IsNullOrEmpty(_handler.Requests[1].Body));
    }

    [Fact]
    public async Task PathParameters()
    {
        await _client.RequestAsync("GET", "/items/{id}", new Dictionary<string, object?> { ["id"] = "a/b c" });
        Assert.Equal("https://www.asin.dev/api/v1/items/a%2Fb%20c", _handler.Requests[0].Request.RequestUri!.AbsoluteUri);
        await Assert.ThrowsAsync<AsinSerializationException>(() => _client.RequestAsync("GET", "/items/{id}"));
    }

    [Fact]
    public async Task UnsupportedMethod()
    {
        await Assert.ThrowsAsync<AsinConfigurationException>(() => _client.GetAccountAsync(new RequestOptions { Method = "POST" }));
    }

    [Fact]
    public async Task EveryEndpoint()
    {
        var calls = new Dictionary<string, Func<Task<ApiResponse>>>
        {
            ["check_product"] = () => _client.CheckProductAsync(new CheckProductParams { Id = "test" }),
            ["delete_product"] = () => _client.DeleteProductAsync(new DeleteProductParams { Id = "test" }),
            ["fetch_offers"] = () => _client.FetchOffersAsync(new FetchOffersParams { Asin = "test" }),
            ["get_account"] = () => _client.GetAccountAsync(),
            ["get_asin_history"] = () => _client.GetAsinHistoryAsync(new GetAsinHistoryParams { Asin = "test" }),
            ["get_offers"] = () => _client.GetOffersAsync(new GetOffersParams { Asin = "test" }),
            ["get_product"] = () => _client.GetProductAsync(new GetProductParams { Id = "test" }),
            ["list_products"] = () => _client.ListProductsAsync(new ListProductsParams()),
            ["search_amazon"] = () => _client.SearchAmazonAsync(new SearchAmazonParams { Query = "test" }),
            ["track_product"] = () => _client.TrackProductAsync(new TrackProductParams { Product = "test" }),
            ["update_product"] = () => _client.UpdateProductAsync(new UpdateProductParams { Id = "test" }),
        };
        foreach (var (name, endpoint) in Endpoints.All)
        {
            await calls[name]();
            var request = _handler.Requests[^1].Request;
            Assert.Equal("https://www.asin.dev/api/v1" + endpoint.Path.Replace("{id}", "test"), request.RequestUri!.GetLeftPart(UriPartial.Path));
            Assert.Equal(endpoint.Methods[0], request.Method.Method);
        }
        Assert.Equal(Endpoints.All.Count, calls.Count);
    }

    [Fact]
    public async Task BinaryResponse()
    {
        _handler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[] { 0x89, 0x50, 0x4E, 0x47 }) { Headers = { { "Content-Type", "image/png" } } },
        };
        var response = await _client.RequestAsync("GET", "/anything");
        Assert.False(response.IsJson);
        Assert.Equal(4, response.Bytes.Length);
        Assert.Equal("image/png", response.ContentType);
    }

    [Fact]
    public async Task NonJsonSuccessForJsonOperation()
    {
        _handler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>", Encoding.UTF8, "text/html") };
        await Assert.ThrowsAsync<AsinResponseDecodingException>(() => _client.SearchAmazonAsync(new SearchAmazonParams { Query = "wireless earbuds" }));
    }

    [Fact]
    public async Task ApiError()
    {
        _handler.Responder = _ => Json(401, "{\"error\":\"Invalid API Key\"}");
        var error = await Assert.ThrowsAsync<AsinApiException>(() => _client.RequestAsync("GET", "/account"));
        Assert.Equal(401, error.StatusCode);
        Assert.Equal("Invalid API Key", error.Message);
        Assert.Equal("Invalid API Key", error.ResponseBody!.Value.GetProperty("error").GetString());
    }

    [Fact]
    public async Task NonJsonErrorBody()
    {
        _handler.Responder = _ => new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("Bad gateway") };
        var error = await Assert.ThrowsAsync<AsinApiException>(
            () => _client.RequestAsync("GET", "/account", null, new RequestOptions { MaxRetries = 0 }));
        Assert.Equal(502, error.StatusCode);
        Assert.Equal("Bad gateway", Encoding.UTF8.GetString(error.RawResponse.Body));
    }

    [Fact]
    public async Task TransportError()
    {
        _handler.Responder = _ => throw new HttpRequestException("network down");
        await Assert.ThrowsAsync<AsinTransportException>(() => _client.RequestAsync("GET", "/account"));
    }

    [Fact]
    public async Task DeserializesIntoTypes()
    {
        _handler.Responder = _ => Json(200, "{\"value\":42}");
        var response = await _client.RequestAsync("GET", "/account");
        Assert.Equal(42, response.Deserialize<Payload>()!.Value);
    }

    private sealed record Payload(int Value);

    [Fact]
    public async Task RetriesThenSucceeds()
    {
        var calls = 0;
        _handler.Responder = _ =>
        {
            calls++;
            if (calls == 1)
            {
                throw new HttpRequestException("reset");
            }
            if (calls == 2)
            {
                var busy = Json(429, "{}");
                busy.Headers.TryAddWithoutValidation("Retry-After", "0");
                return busy;
            }
            return calls == 3 ? Json(503, "{}") : Json(200, "{}");
        };
        var response = await _client.RequestAsync("GET", "/account");
        Assert.True(response.IsJson);
        Assert.Equal(4, calls);
    }

    [Fact]
    public async Task RetryLimitsAndClientErrors()
    {
        using var limited = new AsinClient(new AsinClientOptions
        {
            ApiKey = "k",
            HttpClient = new HttpClient(_handler),
            MaxRetries = 2,
            RetryDelay = TimeSpan.FromMilliseconds(1),
        });
        _handler.Responder = _ => Json(503, "{}");
        await Assert.ThrowsAsync<AsinApiException>(() => limited.RequestAsync("GET", "/account"));
        Assert.Equal(3, _handler.Requests.Count);
        _handler.Responder = _ => Json(402, "{}");
        await Assert.ThrowsAsync<AsinApiException>(() => limited.RequestAsync("GET", "/account"));
        Assert.Equal(4, _handler.Requests.Count);
        _handler.Responder = _ => Json(500, "{}");
        await Assert.ThrowsAsync<AsinApiException>(
            () => limited.RequestAsync("GET", "/account", null, new RequestOptions { MaxRetries = 0 }));
        Assert.Equal(5, _handler.Requests.Count);
    }

    [Theory]
    [InlineData("Rate limit exceeded: You've exceeded the rate limit on your subscription", 1)]
    [InlineData("Rate limit exceeded for the API key: quota daily", 1)]
    [InlineData("Invalid API Key", 1)]
    public async Task UnauthorizedIsNeverRetried(string message, int expectedCalls)
    {
        var calls = 0;
        _handler.Responder = _ => ++calls == 1 ? Json(401, JsonSerializer.Serialize(new { error = message })) : Json(200, "{}");
        try
        {
            await _client.RequestAsync("GET", "/account");
        }
        catch (AsinApiException)
        {
        }
        Assert.Equal(expectedCalls, calls);
    }

    [Fact]
    public async Task ReadsNestedErrorMessage()
    {
        _handler.Responder = _ => Json(402, "{\"error\":{\"code\":\"insufficient_credits\",\"message\":\"No credits left\"}}");
        var error = await Assert.ThrowsAsync<AsinApiException>(() => _client.RequestAsync("GET", "/account"));
        Assert.Equal("No credits left", error.ErrorMessage);
        _handler.Responder = _ => Json(200, "{}");
        await _client.SearchAmazonAsync(new SearchAmazonParams { Query = "wireless earbuds" });
    }

    [Fact]
    public async Task BatchKeepsOrderAndIsolatesFailures()
    {
        var running = 0;
        var peak = 0;
        var results = await _client.BatchAsync(Enumerable.Range(1, 6), async (n, ct) =>
        {
            var now = Interlocked.Increment(ref running);
            InterlockedMax(ref peak, now);
            await Task.Delay(5, ct);
            Interlocked.Decrement(ref running);
            if (n == 4)
            {
                throw new InvalidOperationException("boom");
            }
            return n * 10;
        }, concurrency: 2);
        Assert.Equal(2, peak);
        Assert.Equal(6, results.Count);
        Assert.False(results[3].Ok);
        Assert.Equal(60, results[5].Value);
        Assert.Equal(3, results[3].Index);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while ((current = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, current) != current)
        {
        }
    }
}
