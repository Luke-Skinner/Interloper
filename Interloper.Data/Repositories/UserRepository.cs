using Interloper.Core.Interfaces;
using Interloper.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Interloper.Data.Repositories;

/// <summary>
/// Repository for user data access
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly InterloperDbContext _context;

    public UserRepository(InterloperDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets a user by Discord ID
    /// </summary>
    public async Task<User?> GetByDiscordIdAsync(long discordId)
    {
        return await _context.Users
            .Include(u => u.Alerts.Where(a => a.IsActive))
            .FirstOrDefaultAsync(u => u.DiscordId == discordId);
    }

    /// <summary>
    /// Creates a new user
    /// </summary>
    public async Task<User> CreateAsync(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// Updates a user
    /// </summary>
    public async Task<User> UpdateAsync(User user)
    {
        user.LastActive = DateTime.UtcNow;
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// Gets or creates a user (auto-creation on first command use)
    /// </summary>
    public async Task<User> GetOrCreateAsync(long discordId, string username)
    {
        var user = await GetByDiscordIdAsync(discordId);

        if (user != null)
        {
            // Update last active timestamp
            user.LastActive = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return user;
        }

        // Create new user
        var newUser = new User
        {
            DiscordId = discordId,
            Username = username,
            Tier = "free",
            AlertCount = 0,
            CreatedAt = DateTime.UtcNow,
            LastActive = DateTime.UtcNow
        };

        return await CreateAsync(newUser);
    }
}
