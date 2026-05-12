using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Playgo.Domain.Enums;
using Playgo.Infrastructure.Persistence;

namespace Playgo.Infrastructure.Identity;

public static class HangfireJobsSetup
{
    public static void SetupRecurringJobs(IServiceProvider serviceProvider)
    {
        var recurringJobManager = serviceProvider.GetRequiredService<IRecurringJobManager>();

        recurringJobManager.AddOrUpdate<HangfireJobs>(
            "cleanup-old-history",
            job => job.CleanupOldHistoryAsync(),
            "0 2 * * *",
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        recurringJobManager.AddOrUpdate<HangfireJobs>(
            "recalculate-trending",
            job => job.RecalculateTrendingAsync(),
            Cron.Hourly(),
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        recurringJobManager.AddOrUpdate<HangfireJobs>(
            "cleanup-old-view-logs",
            job => job.CleanupOldViewLogsAsync(),
            "0 3 * * *",
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
    }
}

public class HangfireJobs
{
    private readonly ApplicationDbContext _db;

    public HangfireJobs(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task CleanupOldHistoryAsync()
    {
        var threshold = DateTime.UtcNow.AddDays(-90);

        var stale = await _db.WatchHistories
            .Where(w => w.IsCompleted && w.LastWatchedAt < threshold)
            .ToListAsync();

        foreach (var entry in stale)
        {
            entry.IsDeleted = true;
            entry.UpdatedAt = DateTime.UtcNow;
        }

        if (stale.Count > 0)
            await _db.SaveChangesAsync();
    }

    public async Task CleanupOldViewLogsAsync()
    {
        var threshold = DateTime.UtcNow.AddDays(-90);

        var stale = await _db.ContentViewLogs
            .Where(l => !l.IsDeleted && l.ViewedAt < threshold)
            .ToListAsync();

        foreach (var log in stale)
        {
            log.IsDeleted = true;
            log.UpdatedAt = DateTime.UtcNow;
        }

        if (stale.Count > 0)
            await _db.SaveChangesAsync();
    }

    public async Task RecalculateTrendingAsync()
    {
        var topIds = await _db.Contents
            .Where(c => c.Status == ContentStatus.Published)
            .OrderByDescending(c => c.ViewCount)
            .Take(20)
            .Select(c => c.Id)
            .ToListAsync();

        var topSet = new HashSet<Guid>(topIds);

        var published = await _db.Contents
            .Where(c => c.Status == ContentStatus.Published)
            .ToListAsync();

        foreach (var content in published)
        {
            var shouldBeTrending = topSet.Contains(content.Id);
            if (content.IsTrending != shouldBeTrending)
            {
                content.IsTrending = shouldBeTrending;
                content.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();
    }
}
