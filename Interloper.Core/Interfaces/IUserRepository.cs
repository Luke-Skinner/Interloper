using Interloper.Core.Models;

namespace Interloper.Core.Interfaces;

/// <summary>
/// Repository for user data access
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Gets a user by Discord ID
    /// </summary>
    Task<User?> GetByDiscordIdAsync(long discordId);

    /// <summary>
    /// Creates a new user
    /// </summary>
    Task<User> CreateAsync(User user);

    /// <summary>
    /// Updates a user
    /// </summary>
    Task<User> UpdateAsync(User user);

    /// <summary>
    /// Gets or creates a user (used for auto-creation on first command)
    /// </summary>
    Task<User> GetOrCreateAsync(long discordId, string username);
}
