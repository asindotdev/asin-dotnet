using Asin;

using var client = new AsinClient(); // reads ASIN_API_KEY
try
{
    var response = await client.RequestAsync("GET", "/account");
    Console.WriteLine(response.Text);
}
catch (AsinApiException exception)
{
    Console.Error.WriteLine($"HTTP {exception.StatusCode}: {exception.Message}");
}
