using Interloper.Core.Interfaces;
using Interloper.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Interloper.Data.Repositories;

/// <summary>
/// Repository for alert data access
/// </summary>
public class AlertRepository : IAlertRepository
{
    private readonly InterloperDbContext _context;

    public AlertRepository(InterloperDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets an alert by ID
    /// </summary>
    public async Task<Alert?> GetByIdAsync(Guid id)
    {
        return await _context.Alerts
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    /// <summary>
    /// Gets all alerts for a user
    /// </summary>
    public async Task<IEnumerable<Alert>> GetByUserIdAsync(long userId, bool activeOnly = true)
    {
        var query = _context.Alerts.AsQueryable();

        if (activeOnly)
        {
            query = query.Where(a => a.IsActive);
        }

        return await query
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Creates a new alert
    /// </summary>
    public async Task<Alert> CreateAsync(Alert alert)
    {
        _context.Alerts.Add(alert);
        await _context.SaveChangesAsync();
        return alert;
    }

    /// <summary>
    /// Updates an alert
    /// </summary>
    public async Task<Alert> UpdateAsync(Alert alert)
    {
        alert.UpdatedAt = DateTime.UtcNow;
        _context.Alerts.Update(alert);
        await _context.SaveChangesAsync();
        return alert;
    }

    /// <summary>
    /// Deletes an alert
    /// </summary>
    public async Task<bool> DeleteAsync(Guid id)
    {
        var alert = await _context.Alerts.FindAsync(id);
        if (alert == null)
        {
            return false;
        }

        _context.Alerts.Remove(alert);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Gets alerts that are due for checking
    /// </summary>
    public async Task<IEnumerable<Alert>> GetAlertsDueForCheckAsync()
    {
        var now = DateTime.UtcNow;

        return await _context.Alerts
            .Where(a => a.IsActive)
            .Where(a => a.NextCheckAt == null || a.NextCheckAt <= now)
            .Include(a => a.User)
            .ToListAsync();
    }
}
