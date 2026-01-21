using Interloper.Core.Models;

namespace Interloper.Core.Interfaces;

/// <summary>
/// Repository for alert data access
/// </summary>
public interface IAlertRepository
{
    /// <summary>
    /// Gets an alert by ID
    /// </summary>
    Task<Alert?> GetByIdAsync(Guid id);

    /// <summary>
    /// Gets all alerts for a user
    /// </summary>
    Task<IEnumerable<Alert>> GetByUserIdAsync(long userId, bool activeOnly = true);

    /// <summary>
    /// Creates a new alert
    /// </summary>
    Task<Alert> CreateAsync(Alert alert);

    /// <summary>
    /// Updates an alert
    /// </summary>
    Task<Alert> UpdateAsync(Alert alert);

    /// <summary>
    /// Deletes an alert
    /// </summary>
    Task<bool> DeleteAsync(Guid id);

    /// <summary>
    /// Gets alerts that are due for checking
    /// </summary>
    Task<IEnumerable<Alert>> GetAlertsDueForCheckAsync();
}
