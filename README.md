# Playgo Backend

Video streaming platform backend built with .NET 8, PostgreSQL, Redis, and Docker.

## Tech Stack
- **Runtime**: .NET 8 (ASP.NET Core Web API)
- **Database**: PostgreSQL 16 + Entity Framework Core 8
- **Cache**: Redis 7
- **Auth**: JWT Bearer (access 15min + refresh 7days)
- **Background jobs**: Hangfire
- **Logging**: Serilog
- **Docs**: Swagger / OpenAPI
- **Architecture**: Clean Architecture (Domain → Application → Infrastructure → API)

---

## Quick Start (Docker)

Requirements: Docker + Docker Compose

```bash
git clone <your-repo-url>
cd playgo-backend
cp .env.example .env
# Edit .env: set DB_PASSWORD and JWT_SECRET
docker-compose up -d
```

URLs:
- API: http://localhost:8080
- Swagger: http://localhost:8080/swagger
- Hangfire dashboard: http://localhost:8080/hangfire
- Health check: http://localhost:8080/health

---

## Local Development (without Docker)

Requirements: .NET 8 SDK, PostgreSQL, Redis running locally.

```bash
# Restore packages
dotnet restore

# Update connection strings in src/Playgo.API/appsettings.Development.json

# Run migrations
dotnet ef migrations add InitialCreate \
  --project src/Playgo.Infrastructure \
  --startup-project src/Playgo.API \
  --output-dir Persistence/Migrations

dotnet ef database update \
  --project src/Playgo.Infrastructure \
  --startup-project src/Playgo.API

# Run
cd src/Playgo.API
dotnet run
```

---

## API Endpoints

### Auth — /api/auth
| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | /register | — | Ro'yxatdan o'tish |
| POST | /login | — | Kirish |
| POST | /refresh | — | Token yangilash |
| POST | /logout | ✓ | Chiqish |
| GET | /me | ✓ | Profil |
| PUT | /me | ✓ | Profilni yangilash |
| PUT | /me/password | ✓ | Parol o'zgartirish |

### Contents — /api/contents
| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | / | — | Katalog (filter + pagination) |
| GET | /featured | — | Featured kontentlar |
| GET | /trending | — | Trending kontentlar |
| GET | /{id} | — | Kontent detali |
| GET | /slug/{slug} | — | Slug bo'yicha |
| GET | /{id}/similar | — | O'xshash kontentlar |
| POST | /{id}/view | — | Ko'rishni hisoblash |

### Admin — /api/admin/contents
| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | / | Admin | Kontent yaratish |
| PUT | /{id} | Admin | Kontent yangilash |
| DELETE | /{id} | Admin | O'chirish |
| POST | /{id}/seasons | Admin | Mavsum qo'shish |
| POST | /seasons/{id}/episodes | Admin | Epizod qo'shish |

### Other endpoints
- GET /api/genres — Janrlar ro'yxati
- POST /api/genres [Admin] — Janr yaratish
- GET,POST,PUT,DELETE /api/reviews — Sharhlar
- GET,POST,DELETE /api/favorites [Auth] — Sevimlilar
- GET,PUT,DELETE /api/watch-history [Auth] — Ko'rish tarixi
- POST /api/upload/image [Admin] — Rasm yuklash
- POST /api/upload/video [Admin] — Video yuklash

### Watchlist — /api/watchlist [Auth]
"Save for later" — separate from Favorites. Soft-deleted entries are revived on re-add so duplicates can't accumulate.
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | / | Paged watchlist (ordered by `priority` asc then `createdAt` desc). |
| POST | /{contentId} | Add. Optional JSON body `{ priority, note }`. Re-add of a removed entry revives the original row. |
| DELETE | /{contentId} | Soft-remove. |
| GET | /{contentId}/check | `{ isInWatchlist: bool }`. |
| PUT | /{contentId}/priority | Body `{ priority, note }`. |

### Playlists — /api/playlists
User-created collections of content. Owner has full CRUD; public playlists are visible to everyone.
| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | /mine | ✓ | Paged playlists of the caller. |
| GET | /public | — | Paged public playlists, optional `?search=`. |
| GET | /{id} | optional | Detail with ordered items. Private playlists return 400 to non-owners. |
| POST | / | ✓ | Create. Body `{ name, description?, isPublic, coverImageUrl? }`. |
| PUT | /{id} | ✓ owner | Rename / change visibility / cover. |
| DELETE | /{id} | ✓ owner | Soft-delete playlist AND its items. |
| POST | /{id}/items | ✓ owner | Add `{ contentId, orderIndex? }`. Defaults to append. Duplicates rejected. |
| DELETE | /{id}/items/{itemId} | ✓ owner | Remove item. |
| PUT | /{id}/reorder | ✓ owner | Body `{ itemIdsInOrder: [guid, …] }` — must list every current item exactly once; OrderIndex is reassigned by position. |

### Review moderation

Reviews carry `likesCount`, `dislikesCount`, and (for authenticated callers) `myVote: "like" | "dislike" | null`. Voting is a single endpoint with toggle semantics — a same-type vote cancels itself, opposite-type swaps counters atomically.

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | /api/reviews/{id}/vote | ✓ | Body `{ "voteType": 1 \| 2 }` (1=Like, 2=Dislike). Returns `{ likesCount, dislikesCount, myVote }`. |
| GET | /api/admin/reviews?page=&pageSize=&contentId=&isApproved=&userId=&search= | Admin | Paged list including unapproved entries. |
| PUT | /api/admin/reviews/{id}/approve | Admin | Set `IsApproved=true`; folds the rating back into the content average if it was previously pending. |
| PUT | /api/admin/reviews/{id}/reject | Admin | Body `{ "reason": "..." }`. Stores `RejectionReason`; pulls a previously-approved rating out of the average. |
| DELETE | /api/admin/reviews/{id} | Admin | Soft-delete. |

The public `GET /api/reviews/content/{contentId}` only shows `IsApproved=true` reviews. Anonymous and non-author callers always see `myVote=null`.

`appsettings.json` carries a `Reviews:RequireModeration` flag — when `true`, new reviews from `POST /api/reviews` are created with `IsApproved=false` (and not folded into the content average) until an admin approves them.

---

## Admin endpoints

The admin panel (Next.js `/admin` plus the legacy HTML pages `/admin/users.html`, `/admin/edit-user.html`, `/admin/index.html`) is wired to three tabs of endpoints. All routes require an `Admin` role JWT (also enforceable via the `AdminOnly` / `AdminOrModerator` policies registered in `Program.cs`).

### Users tab — `/api/admin/users`
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | Paged list. Query params: `page`, `pageSize`, `search` (matches username / email / fullName, case-insensitive), `role`, `isBanned`, `isEmailVerified`, `sortBy` (`createdAt` default, `lastLoginAt`, `email`). |
| GET | `/{id}` | Detail with `reviewsCount`, `favoritesCount`, `watchlistCount`, `playlistsCount`, `banReason`, `bannedAt`, and the user's 10 most recent activities. |
| PUT | `/{id}` | Body `{ fullName?, avatarUrl?, role?, isBanned?, banReason? }`. Role changes are logged; flipping `isBanned` also clears the user's refresh token. |
| POST | `/{id}/ban` | Body `{ "reason": "..." }`. Marks the user banned, stores the reason, stamps `BannedAt`, and clears `RefreshToken`. Rejects an admin trying to ban themselves. |
| POST | `/{id}/unban` | Clears `IsBanned`, `BanReason`, `BannedAt`. |
| DELETE | `/{id}` | Soft-delete. Rejects an admin trying to delete themselves; clears the deleted user's refresh token. |

### Dashboard tab — `/api/admin/dashboard`
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/stats` | Returns `AdminDashboardStatsDto` — `totalUsers`, `newUsersThisMonth`, `totalContents`, `publishedContents`, `draftContents`, `totalViews`, `totalReviews`, `averageRating`, `totalFavorites`, `totalWatchlists`, plus `recentMovies` (top 5), `recentUsers` (top 5), `topGenres` (top 5 by content count, with `totalViews`), and `viewsLast30Days` (always 30 entries, zero-filled). Cached in Redis under `admin:dashboard:stats` with a 60-second TTL. |

### Reviews tab — `/api/admin/reviews`
See the [Review moderation](#review-moderation) section above for the full table.

### View logging
`POST /api/contents/{id}/view` now writes one `ContentViewLog` row per call (capturing `UserId` if authenticated, plus the request `IpAddress` and `UserAgent` truncated to 45/500 chars). These rows drive the `viewsLast30Days` series on the dashboard. The Hangfire recurring job `cleanup-old-view-logs` (daily 03:00 UTC) soft-deletes anything older than 90 days.

---

## Environment Variables

| Variable | Description | Example |
|----------|-------------|---------|
| DB_PASSWORD | PostgreSQL password | strong_pass_here |
| JWT_SECRET | JWT signing key (min 32 chars) | random_string_min_32_chars |
| API_BASE_URL | Public API URL | https://api.playgo.uz |
| FRONTEND_URL | Frontend origin for CORS | https://playgo.uz |

---

## Project Structure

```
src/
├── Playgo.Domain/          # Entities, Enums, BaseEntity (no dependencies)
├── Playgo.Application/     # Services, DTOs, Validators, Interfaces
├── Playgo.Infrastructure/  # EF Core, JWT, BCrypt, Redis, Hangfire
└── Playgo.API/             # Controllers, Middleware, Program.cs
tests/
└── Playgo.Tests/           # Unit tests (xUnit + Moq)
```

---

## i18n support

Content and Genre carry per-language overrides via `ContentTranslation` and `GenreTranslation`. The original columns on `Content`/`Genre` are kept as the **primary (default) locale** and serve as a fallback when no translation matches the requested language.

Supported languages: `uz`, `ru`, `en`. Default fallback: `en`, then the primary columns.

### Request language detection
The active language is resolved per-request by `ILocalizationContext` in this order:
1. Query string `?lang=uz|ru|en` (overrides everything).
2. `Accept-Language` header — the first supported primary tag wins (e.g. `Accept-Language: ru-RU,en;q=0.5` → `ru`).
3. Default `en`.

When the resolved language matches a translation, `GET /api/contents/{id}` and `GET /api/contents/slug/{slug}` return the localized `title`, `originalTitle`, `description`, `shortDescription`, `director`, `cast` at the top level. The full `translations` array is always present so clients can render any language without a second round-trip.

### Endpoints
| Method | Route | Auth | Purpose |
|--------|-------|------|---------|
| GET | `/api/contents/{id}/translations` | — | All translations for a content. |
| POST | `/api/admin/contents/{contentId}/translations` | Admin | Upsert a translation. Idempotent per `(contentId, languageCode)`. |
| DELETE | `/api/admin/contents/{contentId}/translations/{lang}` | Admin | Soft-delete a translation. |
| GET | `/api/genres/{genreId}/translations` | — | All translations for a genre. |
| POST | `/api/genres/{genreId}/translations` | Admin | Upsert genre translation. |
| DELETE | `/api/genres/{genreId}/translations/{lang}` | Admin | Soft-delete genre translation. |

`POST /api/admin/contents` and `PUT /api/admin/contents/{id}` also accept an optional `translations: [{ languageCode, title, originalTitle, description, shortDescription, director, cast }, ...]` array — on update, the previous translations are replaced wholesale by the supplied list.

---

## Roadmap (keyingi bosqich)
- [ ] HLS transcoding — FFmpeg worker service
- [ ] CDN integration — Bunny.net signed URLs
- [ ] Full-text search — PostgreSQL tsvector
- [ ] Keyset pagination — OFFSET o'rniga cursor-based
- [ ] Redis caching — Featured/Trending/Genre endpointlar uchun
- [ ] Email verification — SMTP + confirmation link
- [ ] Admin analytics dashboard endpoint
