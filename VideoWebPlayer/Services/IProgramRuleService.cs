using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services;

/// <summary>
/// Provides administration operations for the automated TV channel ("Sender") and its program
/// rules ("Programmregeln"). Every mutation enforces administrator rights server-side by checking
/// <see cref="ApplicationUser.IsAdmin"/> on the calling user's database record.
/// </summary>
public interface IProgramRuleService
{
    /// <summary>
    /// The material kinds a given <see cref="ProgramRuleType"/> accepts — exactly one material
    /// reference per rule, chosen from this set.
    /// </summary>
    IReadOnlyList<ProgramRuleMaterialType> GetAllowedMaterialTypes(ProgramRuleType ruleType);

    /// <summary>
    /// Returns the single delivered channel, creating it named "TV-Sender" on first use.
    /// </summary>
    Task<TVChannel> GetOrCreateChannelAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames the channel. Requires administrator rights.
    /// </summary>
    Task<TVChannel> RenameChannelAsync(string userId, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all program rules of the channel, ordered by name. Requires administrator rights.
    /// </summary>
    Task<List<ProgramRule>> GetRulesAsync(string userId, long channelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a program rule after validating the input. Requires administrator rights.
    /// </summary>
    Task<ProgramRule> CreateRuleAsync(string userId, long channelId, ProgramRuleInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing program rule after validating the input. Requires administrator rights.
    /// </summary>
    Task<ProgramRule> UpdateRuleAsync(string userId, long ruleId, ProgramRuleInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates or deactivates a program rule. Requires administrator rights.
    /// </summary>
    Task<ProgramRule> SetRuleActiveAsync(string userId, long ruleId, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a program rule. Requires administrator rights.
    /// </summary>
    Task DeleteRuleAsync(string userId, long ruleId, CancellationToken cancellationToken = default);
}
