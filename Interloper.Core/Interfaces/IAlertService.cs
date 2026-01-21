using Interloper.Core.Models;

namespace Interloper.Core.Interfaces;

/// <summary>
/// Service for managing alerts
/// </summary>
public interface IAlertService
{
    /// <summary>
    /// Creates a new alert
    /// </summary>
    Task<Alert> CreateAlertAsync(Alert alert);

    /// <summary>
    /// Gets an alert by ID
    /// </summary>
    Task<Alert?> GetAlertByIdAsync(Guid alertId);

    /// <summary>
    /// Gets all alerts for a user
    /// </summary>
    Task<IEnumerable<Alert>> GetUserAlertsAsync(long discordId, bool activeOnly = true);

    /// <summary>
    /// Updates an existing alert
    /// </summary>
    Task<Alert> UpdateAlertAsync(Alert alert);

    /// <summary>
    /// Pauses an alert
    /// </summary>
    Task<bool> PauseAlertAsync(Guid alertId);

    /// <summary>
    /// Resumes a paused alert
    /// </summary>
    Task<bool> ResumeAlertAsync(Guid alertId);

    /// <summary>
    /// Deletes an alert
    /// </summary>
    Task<bool> DeleteAlertAsync(Guid alertId);

    /// <summary>
    /// Gets alerts that need to be checked
    /// </summary>
    Task<IEnumerable<Alert>> GetAlertsToCheckAsync();

    /// <summary>
    /// Updates the next check time for an alert
    /// </summary>
    Task UpdateNextCheckTimeAsync(Guid alertId, DateTime nextCheckTime);
}
