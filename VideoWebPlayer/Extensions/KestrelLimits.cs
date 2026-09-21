namespace VideoWebPlayer.Extensions;

/// <summary>
/// Parses Kestrel limit values from configuration.
/// </summary>
internal static class KestrelLimits
{
    /// <summary>
    /// Parses the configured <c>Kestrel:Limits:MaxRequestBodySize</c> value.
    /// Returns <c>null</c> for 0 or negative values (unlimited) and throws for non-numeric values.
    /// </summary>
    public static long? ParseMaxRequestBodySize(string configured)
    {
        if (!long.TryParse(configured, out var limit))
        {
            throw new InvalidOperationException(
                $"Ungültiger Wert für 'Kestrel:Limits:MaxRequestBodySize': \"{configured}\". Erwartet wird eine ganze Zahl in Bytes (0 oder negativ = unbegrenzt).");
        }

        return limit > 0 ? limit : null;
    }
}
