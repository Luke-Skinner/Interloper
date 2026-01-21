namespace Interloper.Core.Models;

/// <summary>
/// Represents a Discord user who has interacted with the bot
/// </summary>
public class User
{
    /// <summary>
    /// Discord user ID (unique identifier from Discord)
    /// </summary>
    public long DiscordId { get; set; }

    /// <summary>
    /// Discord username
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// User tier (free, premium, etc.)
    /// </summary>
    public string Tier { get; set; } = "free";

    /// <summary>
    /// Number of active alerts the user has
    /// </summary>
    public int AlertCount { get; set; } = 0;

    /// <summary>
    /// When the user first used the bot
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last time the user interacted with the bot
    /// </summary>
    public DateTime LastActive { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property: All alerts created by this user
    /// </summary>
    public ICollection<Alert> Alerts { get; set; } = new List<Alert>();

    /// <summary>
    /// Navigation property: All notifications sent to this user
    /// </summary>
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
