namespace VideoWebPlayer.Components.Shared.Media;

/// <summary>
/// Represents a context action rendered by <see cref="MediaBox"/>.
/// </summary>
/// <param name="Key">Stable action key passed back to the parent component.</param>
/// <param name="Label">Visible action label.</param>
public sealed record MediaBoxAction(string Key, string Label);
