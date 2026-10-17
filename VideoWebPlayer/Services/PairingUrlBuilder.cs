namespace VideoWebPlayer.Services;

/// <summary>
/// Baut die Ticket-Adresse des Geräte-Onboardings (<c>{basis}/pairing?t=ticket</c>).
/// Die Basis ist die öffentliche Server-Basis-URL — sie trägt die Adresse, unter
/// der das Gerät den Server erreicht. Ein etwaiger Unterpfad der Basis-URL
/// (z. B. Reverse-Proxy <c>https://host/videoplayer/</c>) bleibt erhalten.
/// </summary>
internal static class PairingUrlBuilder
{
    /// <summary>
    /// Baut die Ticket-Adresse aus einer Basis-URL und dem opaken Ticket.
    /// </summary>
    /// <param name="baseUrl">Öffentliche Basis-URL des Servers (Admin-Einstellung oder Request-Basis).</param>
    /// <param name="ticket">Das opake Kopplungs-Ticket.</param>
    /// <returns>Die vollständige Ticket-Adresse für QR-Code und Anzeige.</returns>
    public static string BuildPairingUrl(string baseUrl, string ticket)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(ticket);

        return $"{baseUrl.TrimEnd('/')}/pairing?t={Uri.EscapeDataString(ticket)}";
    }
}
