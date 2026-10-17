namespace VideoWebPlayer.Services;

/// <summary>
/// Resolved mDNS/DNS-SD service profile of the server: the testable output of
/// <see cref="MdnsServiceProfileBuilder"/> before it is handed to the multicast transport.
/// </summary>
internal sealed record MdnsAdvertisement
{
    /// <summary>
    /// Gets the service instance name shown in DNS-SD browses.
    /// </summary>
    public required string InstanceName { get; init; }

    /// <summary>
    /// Gets the announced service type as configured (e.g. <c>_videowebplayer._tcp.local.</c>).
    /// </summary>
    public required string ServiceType { get; init; }

    /// <summary>
    /// Gets the announced port.
    /// </summary>
    public required int Port { get; init; }

    /// <summary>
    /// Gets the machine host name the service resolves to.
    /// </summary>
    public required string HostName { get; init; }

    /// <summary>
    /// Gets the fixed TXT records identifying the service (<c>path=/</c>, <c>app=VideoWebPlayer</c>).
    /// </summary>
    public required IReadOnlyDictionary<string, string> TxtRecords { get; init; }
}
