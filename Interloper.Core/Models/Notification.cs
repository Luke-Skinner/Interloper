namespace Interloper.Core.Models;

/// <summary>
/// Represents a notification sent to a user
/// </summary>
public class Notification
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Discord ID of the user who received the notification
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// Alert that triggered this notification
    /// </summary>
    public Guid AlertId { get; set; }

    /// <summary>
    /// Hotel this notification is about
    /// </summary>
    public Guid HotelId { get; set; }

    /// <summary>
    /// Type of notification (e.g., 'price_drop', 'new_match', 'weekly_summary')
    /// </summary>
    public string NotificationType { get; set; } = string.Empty;

    /// <summary>
    /// Price at the time of notification
    /// </summary>
    public decimal PriceAtNotification { get; set; }

    /// <summary>
    /// When the notification was sent
    /// </summary>
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether the user clicked on the notification
    /// </summary>
    public bool UserClicked { get; set; } = false;

    // Navigation Properties
    /// <summary>
    /// The user who received this notification
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// The alert that triggered this notification
    /// </summary>
    public Alert Alert { get; set; } = null!;

    /// <summary>
    /// The hotel this notification is about
    /// </summary>
    public Hotel Hotel { get; set; } = null!;
}
