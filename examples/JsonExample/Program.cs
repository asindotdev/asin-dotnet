using Asin;

using var client = new AsinClient(); // reads ASIN_API_KEY
var response = await client.SearchAmazonAsync(new SearchAmazonParams { Query = "wireless earbuds" });
Console.WriteLine(response.Json);
