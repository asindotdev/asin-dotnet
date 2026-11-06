namespace Asin;

/// <summary>Parameters of one endpoint. Implemented by every generated <c>*Params</c> class.</summary>
public interface IAsinParams
{
    /// <summary>Wire names and values in documentation order; null values are omitted from requests.</summary>
    /// <returns>The parameters.</returns>
    IEnumerable<KeyValuePair<string, object?>> ToWire();
}
