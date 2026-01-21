namespace Interloper.Core.Models;

/// <summary>
/// Represents a price tracking alert created by a user
/// </summary>
public class Alert
{
    /// <summary>
    /// Unique identifier for the alert
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Discord ID of the user who created this alert
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// Type of alert: 'hotel' (specific hotel) or 'city' (all hotels in city)
    /// </summary>
    public string AlertType { get; set; } = string.Empty;

    // Search Criteria
    /// <summary>
    /// Hotel name (null for city alerts)
    /// </summary>
    public string? HotelName { get; set; }

    /// <summary>
    /// City to search in
    /// </summary>
    public string City { get; set; } = string.Empty;

    /// <summary>
    /// Check-in date
    /// </summary>
    public DateOnly CheckIn { get; set; }

    /// <summary>
    /// Check-out date
    /// </summary>
    public DateOnly CheckOut { get; set; }

    /// <summary>
    /// Number of guests
    /// </summary>
    public int Guests { get; set; }

    // Alert Thresholds
    /// <summary>
    /// Maximum price threshold (alert triggers when price is below this)
    /// </summary>
    public decimal MaxPrice { get; set; }

    /// <summary>
    /// Minimum rating filter (0-5)
    /// </summary>
    public decimal MinRating { get; set; } = 0;

    // Optional Filters (stored as JSON in database)
    /// <summary>
    /// Required amenities (e.g., ["wifi", "pool", "parking"])
    /// </summary>
    public List<string>? RequiredAmenities { get; set; }

    /// <summary>
    /// Require free cancellation
    /// </summary>
    public bool FreeCancellation { get; set; } = false;

    /// <summary>
    /// Property types to include (e.g., ["hotel", "apartment", "vacation_rental"])
    /// </summary>
    public List<string>? PropertyTypes { get; set; }

    // Scheduling
    /// <summary>
    /// How often to check prices: 'hourly', '6h', '12h', 'daily', 'weekly'
    /// </summary>
    public string CheckFrequency { get; set; } = string.Empty;

    /// <summary>
    /// Last time this alert was checked
    /// </summary>
    public DateTime? LastCheckedAt { get; set; }

    /// <summary>
    /// Next scheduled check time
    /// </summary>
    public DateTime? NextCheckAt { get; set; }

    // Status
    /// <summary>
    /// Whether this alert is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Number of times this alert has triggered a notification
    /// </summary>
    public int TimesTriggered { get; set; } = 0;

    // Metadata
    /// <summary>
    /// When the alert was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the alert was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    /// <summary>
    /// The user who created this alert
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// Price history records associated with this alert
    /// </summary>
    public ICollection<PriceHistory> PriceHistories { get; set; } = new List<PriceHistory>();

    /// <summary>
    /// Scraper jobs for this alert
    /// </summary>
    public ICollection<ScraperJob> ScraperJobs { get; set; } = new List<ScraperJob>();

    /// <summary>
    /// Notifications sent for this alert
    /// </summary>
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
