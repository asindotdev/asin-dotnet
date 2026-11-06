using System.Text.Json;

namespace Asin;

/// <summary>Base class for every SDK-defined exception.</summary>
public class AsinException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">Error message.</param>
    /// <param name="innerException">Cause.</param>
    public AsinException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>The client configuration is missing or unusable.</summary>
public sealed class AsinConfigurationException : AsinException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">Error message.</param>
    public AsinConfigurationException(string message)
        : base(message)
    {
    }
}

/// <summary>A required parameter is missing or a value cannot be represented on the wire.</summary>
public sealed class AsinSerializationException : AsinException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">Error message.</param>
    /// <param name="parameter">The parameter at fault.</param>
    public AsinSerializationException(string message, string? parameter = null)
        : base(message)
    {
        Parameter = parameter;
    }

    /// <summary>The parameter at fault, or null.</summary>
    public string? Parameter { get; }
}

/// <summary>The request failed before a complete HTTP response was received (network error, timeout).</summary>
public sealed class AsinTransportException : AsinException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">Error message.</param>
    /// <param name="innerException">Cause.</param>
    public AsinTransportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Asin returned a non-success HTTP status.</summary>
public sealed class AsinApiException : AsinException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="rawResponse">The buffered response.</param>
    /// <param name="responseBody">The parsed JSON body, if any.</param>
    /// <param name="errorMessage">The "error" field of the body, if any.</param>
    public AsinApiException(RawResponse rawResponse, JsonElement? responseBody, string? errorMessage)
        : base(errorMessage ?? $"Asin API request failed with status {rawResponse.StatusCode}.")
    {
        RawResponse = rawResponse;
        ResponseBody = responseBody;
        ErrorMessage = errorMessage;
    }

    /// <summary>HTTP status code.</summary>
    public int StatusCode => RawResponse.StatusCode;

    /// <summary>The "error" (or "message") field of the JSON body, or null.</summary>
    public string? ErrorMessage { get; }

    /// <summary>The parsed JSON body, or null when the body is not JSON.</summary>
    public JsonElement? ResponseBody { get; }

    /// <summary>The buffered response.</summary>
    public RawResponse RawResponse { get; }
}

/// <summary>A successful response could not be decoded as the documented representation.</summary>
public sealed class AsinResponseDecodingException : AsinException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">Error message.</param>
    /// <param name="rawResponse">The buffered response.</param>
    /// <param name="innerException">Cause.</param>
    public AsinResponseDecodingException(string message, RawResponse rawResponse, Exception? innerException = null)
        : base(message, innerException)
    {
        RawResponse = rawResponse;
    }

    /// <summary>The buffered response.</summary>
    public RawResponse RawResponse { get; }
}
