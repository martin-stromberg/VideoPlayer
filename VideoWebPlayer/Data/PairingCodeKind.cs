namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Describes the pairing direction represented by a <see cref="PairingCode"/>.
    /// </summary>
    public enum PairingCodeKind
    {
        /// <summary>Short-lived code created by an administrator for <c>api/pairing/exchange</c>.</summary>
        AdminCode = 0,
        /// <summary>Bootstrap ticket created by a signed-in user (QR code + short code alias) for <c>api/pairing/bootstrap</c>.</summary>
        BootstrapTicket = 1,
        /// <summary>Reserved for the device-initiated direction (device shows a code, user confirms) — not implemented yet.</summary>
        DeviceInitiated = 2
    }
}
