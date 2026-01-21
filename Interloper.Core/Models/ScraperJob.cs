namespace Interloper.Core.Models;

/// <summary>
/// Represents a scraper job in the queue
/// </summary>
public class ScraperJob
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Alert this job is for
    /// </summary>
    public Guid AlertId { get; set; }

    /// <summary>
    /// Job type (e.g., 'track_hotel', 'track_city')
    /// </summary>
    public string JobType { get; set; } = string.Empty;

    /// <summary>
    /// Search parameters (stored as JSON)
    /// </summary>
    public string SearchParams { get; set; } = string.Empty;

    /// <summary>
    /// Job status: 'pending', 'processing', 'completed', 'failed'
    /// </summary>
    public string Status { get; set; } = "pending";

    /// <summary>
    /// Priority (1-10, higher = more urgent)
    /// </summary>
    public int Priority { get; set; } = 5;

    /// <summary>
    /// When the job was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the job started processing
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When the job completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Error message if the job failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Number of retry attempts
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Maximum number of retries allowed
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    // Navigation Properties
    /// <summary>
    /// The alert this job is for
    /// </summary>
    public Alert Alert { get; set; } = null!;
}
