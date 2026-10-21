using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Controllers;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services;

/// <summary>
/// Implements <see cref="IProgramRuleService"/>: CRUD for the single delivered TV channel and its
/// program rules, with server-side administrator enforcement and fachliche Validierung.
/// </summary>
public sealed class ProgramRuleService : IProgramRuleService
{
    private static readonly ProgramRuleMaterialType[] AllMaterialTypes =
        Enum.GetValues<ProgramRuleMaterialType>();

    private readonly ApplicationDbContext _db;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    /// <param name="db">Database context.</param>
    public ProgramRuleService(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public IReadOnlyList<ProgramRuleMaterialType> GetAllowedMaterialTypes(ProgramRuleType ruleType)
        => ruleType switch
        {
            ProgramRuleType.SeriesFixedTime => [ProgramRuleMaterialType.TVShow],
            ProgramRuleType.MoviePrimeTime => [ProgramRuleMaterialType.Movie, ProgramRuleMaterialType.MovieCollection],
            ProgramRuleType.GenreRotation => [ProgramRuleMaterialType.Genre],
            ProgramRuleType.Repeat => AllMaterialTypes,
            ProgramRuleType.HolidaySpecial => AllMaterialTypes,
            ProgramRuleType.Filler => [ProgramRuleMaterialType.Movie, ProgramRuleMaterialType.MovieCollection, ProgramRuleMaterialType.Genre, ProgramRuleMaterialType.Playlist],
            _ => throw new ArgumentException($"Unbekannte Regelart: {ruleType}")
        };

    /// <inheritdoc />
    public async Task<TVChannel> GetOrCreateChannelAsync(CancellationToken cancellationToken = default)
    {
        var channel = await _db.TVChannels.FirstOrDefaultAsync(cancellationToken);
        if (channel is not null)
            return channel;

        channel = new TVChannel { Name = "TV-Sender" };
        _db.TVChannels.Add(channel);
        await _db.SaveChangesAsync(cancellationToken);
        return channel;
    }

    /// <inheritdoc />
    public async Task<TVChannel> RenameChannelAsync(string userId, string name, CancellationToken cancellationToken = default)
    {
        await RequireAdminAsync(userId, cancellationToken);
        ValidateChannelName(name);

        var channel = await GetOrCreateChannelAsync(cancellationToken);
        channel.Name = name.Trim();
        channel.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return channel;
    }

    /// <inheritdoc />
    public async Task<List<ProgramRule>> GetRulesAsync(string userId, long channelId, CancellationToken cancellationToken = default)
    {
        await RequireAdminAsync(userId, cancellationToken);

        return await _db.ProgramRules
            .AsNoTracking()
            .Where(r => r.TVChannelId == channelId)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ProgramRule> CreateRuleAsync(string userId, long channelId, ProgramRuleInput input, CancellationToken cancellationToken = default)
    {
        await RequireAdminAsync(userId, cancellationToken);
        await ValidateRuleInputAsync(input, cancellationToken);

        if (!await _db.TVChannels.AnyAsync(c => c.Id == channelId, cancellationToken))
            throw new KeyNotFoundException("Der Sender wurde nicht gefunden.");

        var now = DateTime.UtcNow;
        var rule = new ProgramRule
        {
            TVChannelId = channelId,
            Name = input.Name.Trim(),
            RuleType = input.RuleType,
            Recurrence = input.Recurrence,
            DayOfWeek = input.Recurrence == ProgramRuleRecurrence.Weekly ? input.DayOfWeek : null,
            SingleDate = input.Recurrence == ProgramRuleRecurrence.Once ? input.SingleDate : null,
            TimeOfDay = input.TimeOfDay,
            EpisodeCount = input.EpisodeCount,
            SlotDuration = input.SlotDuration,
            MaterialType = input.MaterialType,
            MaterialId = input.MaterialId,
            Priority = input.Priority,
            IsActive = input.IsActive,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.ProgramRules.Add(rule);
        await _db.SaveChangesAsync(cancellationToken);
        return rule;
    }

    /// <inheritdoc />
    public async Task<ProgramRule> UpdateRuleAsync(string userId, long ruleId, ProgramRuleInput input, CancellationToken cancellationToken = default)
    {
        await RequireAdminAsync(userId, cancellationToken);
        await ValidateRuleInputAsync(input, cancellationToken);

        var rule = await _db.ProgramRules.FindAsync([ruleId], cancellationToken)
            ?? throw new KeyNotFoundException("Die Programmregel wurde nicht gefunden.");

        rule.Name = input.Name.Trim();
        rule.RuleType = input.RuleType;
        rule.Recurrence = input.Recurrence;
        rule.DayOfWeek = input.Recurrence == ProgramRuleRecurrence.Weekly ? input.DayOfWeek : null;
        rule.SingleDate = input.Recurrence == ProgramRuleRecurrence.Once ? input.SingleDate : null;
        rule.TimeOfDay = input.TimeOfDay;
        rule.EpisodeCount = input.EpisodeCount;
        rule.SlotDuration = input.SlotDuration;
        rule.MaterialType = input.MaterialType;
        rule.MaterialId = input.MaterialId;
        rule.Priority = input.Priority;
        rule.IsActive = input.IsActive;
        rule.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return rule;
    }

    /// <inheritdoc />
    public async Task<ProgramRule> SetRuleActiveAsync(string userId, long ruleId, bool isActive, CancellationToken cancellationToken = default)
    {
        await RequireAdminAsync(userId, cancellationToken);

        var rule = await _db.ProgramRules.FindAsync([ruleId], cancellationToken)
            ?? throw new KeyNotFoundException("Die Programmregel wurde nicht gefunden.");

        rule.IsActive = isActive;
        rule.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return rule;
    }

    /// <inheritdoc />
    public async Task DeleteRuleAsync(string userId, long ruleId, CancellationToken cancellationToken = default)
    {
        await RequireAdminAsync(userId, cancellationToken);

        var rule = await _db.ProgramRules.FindAsync([ruleId], cancellationToken)
            ?? throw new KeyNotFoundException("Die Programmregel wurde nicht gefunden.");

        _db.ProgramRules.Remove(rule);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task RequireAdminAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Sie sind nicht angemeldet.");

        if (!user.IsAdmin)
            throw new ForbiddenAccessException("Nur Administratoren dürfen den TV-Sender verwalten.");
    }

    private async Task ValidateRuleInputAsync(ProgramRuleInput input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            throw new ArgumentException("Der Name der Programmregel darf nicht leer sein.");
        if (input.Name.Trim().Length > ProgramRule.NameMaxLength)
            throw new ArgumentException($"Der Name der Programmregel darf höchstens {ProgramRule.NameMaxLength} Zeichen lang sein.");

        if (!Enum.IsDefined(input.RuleType))
            throw new ArgumentException($"Unbekannte Regelart: {input.RuleType}");
        if (!Enum.IsDefined(input.Recurrence))
            throw new ArgumentException($"Unbekanntes Wiederholungsmuster: {input.Recurrence}");
        if (!Enum.IsDefined(input.MaterialType))
            throw new ArgumentException($"Unbekannte Materialart: {input.MaterialType}");

        if (input.Recurrence == ProgramRuleRecurrence.Weekly && input.DayOfWeek is null)
            throw new ArgumentException("Bei wöchentlicher Wiederholung muss ein Wochentag gewählt werden.");
        if (input.Recurrence == ProgramRuleRecurrence.Once && input.SingleDate is null)
            throw new ArgumentException("Bei einmaliger Wiederholung muss ein Datum gewählt werden.");

        if ((input.EpisodeCount is null || input.EpisodeCount <= 0) &&
            (input.SlotDuration is null || input.SlotDuration <= TimeSpan.Zero))
            throw new ArgumentException("Es muss eine Länge oder eine Anzahl von Folgen angegeben werden.");
        if (input.RuleType == ProgramRuleType.SeriesFixedTime && (input.EpisodeCount is null || input.EpisodeCount <= 0))
            throw new ArgumentException("Bei einer Serien-Sendezeit muss die Anzahl der Folgen angegeben werden.");
        if (input.EpisodeCount is <= 0)
            throw new ArgumentException("Die Anzahl der Folgen muss größer als 0 sein.");
        if (input.SlotDuration is not null && input.SlotDuration <= TimeSpan.Zero)
            throw new ArgumentException("Die Länge muss größer als 0 sein.");

        if (!GetAllowedMaterialTypes(input.RuleType).Contains(input.MaterialType))
            throw new ArgumentException($"Die Materialart {input.MaterialType} ist für die Regelart {input.RuleType} nicht zulässig.");

        var exists = input.MaterialType switch
        {
            ProgramRuleMaterialType.TVShow => await _db.TVShows.AnyAsync(m => m.Id == input.MaterialId, cancellationToken),
            ProgramRuleMaterialType.Movie => await _db.Movies.AnyAsync(m => m.Id == input.MaterialId, cancellationToken),
            ProgramRuleMaterialType.MovieCollection => await _db.MovieCollections.AnyAsync(m => m.Id == input.MaterialId, cancellationToken),
            ProgramRuleMaterialType.Genre => await _db.Genres.AnyAsync(m => m.Id == input.MaterialId, cancellationToken),
            ProgramRuleMaterialType.Playlist => await _db.Playlists.AnyAsync(m => m.Id == input.MaterialId, cancellationToken),
            _ => false
        };

        if (!exists)
            throw new KeyNotFoundException("Das gewählte Programmmaterial wurde nicht gefunden.");
    }

    private static void ValidateChannelName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Der Name des Senders darf nicht leer sein.");
        if (name.Trim().Length > TVChannel.NameMaxLength)
            throw new ArgumentException($"Der Name des Senders darf höchstens {TVChannel.NameMaxLength} Zeichen lang sein.");
    }
}
