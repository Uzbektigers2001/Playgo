using Microsoft.EntityFrameworkCore;
using Playgo.Domain.Entities;

namespace Playgo.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Content> Contents { get; }
    DbSet<Genre> Genres { get; }
    DbSet<ContentGenre> ContentGenres { get; }
    DbSet<Season> Seasons { get; }
    DbSet<Episode> Episodes { get; }
    DbSet<Favorite> Favorites { get; }
    DbSet<WatchHistory> WatchHistories { get; }
    DbSet<Review> Reviews { get; }
    DbSet<ContentTranslation> ContentTranslations { get; }
    DbSet<GenreTranslation> GenreTranslations { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
