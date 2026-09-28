using Microsoft.EntityFrameworkCore;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Stellt benutzerdefinierte Datenbankfunktionen für EF Core bereit.
    /// </summary>
    public static class AppDbFunctions
    {
        /// <summary>
        /// Unicode-korrekte, case-insensitive Faltung eines Strings mittels <see cref="string.ToLowerInvariant"/>.
        /// Wird als benutzerdefinierte SQLite-Funktion <c>lower_invariant</c> registriert.
        /// </summary>
        /// <param name="input">Der zu faltende Wert.</param>
        /// <returns>Der Wert nach Anwendung von <see cref="string.ToLowerInvariant"/>, oder <c>null</c>, wenn <paramref name="input"/> <c>null</c> ist.</returns>
        [DbFunction("lower_invariant")]
        public static string? LowerInvariant(string? input) => input?.ToLowerInvariant();
    }
}
