# Changelog

All notable changes to the Playgo backend will be documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Setup
- Initial project audit and environment setup

### Documentation
- Added comprehensive project audit under `docs/audit/` (01 Architecture Overview, 02 Endpoints Inventory, 03 Issues & Technical Debt, 04 Frontend Integration Gaps, 05 Roadmap, 00 Index).
- Identified 20+ frontend-backend integration gaps with per-row severity and solution strategy.
- Created 10-phase roadmap (Prompts 2–10) covering compatibility, domain extensions, admin features, business features, and production readiness.

### Phase 2: User Preferences (Prompt 3)
- New `UserPreferences` entity with `Language` (PreferredLanguage: Uz/Ru/En), `Quality` (PreferredQuality: Auto/SD480/HD720/FHD1080/UHD4K), `Autoplay`, `EmailNotifications`, `PushNotifications`.
- `User.Preferences` 1:1 navigation; unique index on `user_preferences.UserId` with cascade delete.
- New endpoints `GET /api/auth/me/preferences` and `PUT /api/auth/me/preferences` (PATCH semantics — only supplied fields are updated; invalid enum values return 400).
- Default preferences (Language=En, Quality=Auto, Autoplay=true, EmailNotifications=true, PushNotifications=false) auto-created on user registration; also lazily created on first GET for legacy users.
- Migration `AddUserPreferences` adds `user_preferences` table with FK to `users.Id` ON DELETE CASCADE.
- 3 unit tests added in `AuthServicePreferencesTests` covering default-on-get, PATCH semantics, invalid-enum failure.

### Phase 2: i18n Support (Prompt 4)
- Multi-language Content via `ContentTranslation` entity (`LanguageCode`, `Title`, `OriginalTitle`, `Description`, `ShortDescription`, `Director`, `Cast`) with unique index on `(ContentId, LanguageCode)`.
- Multi-language Genre via `GenreTranslation` entity (`LanguageCode`, `Name`, `Description`) with unique index on `(GenreId, LanguageCode)`.
- `ILocalizationContext` resolves the active request language from `?lang=` query string or `Accept-Language` header, falling back to `en`. Bound to `IHttpContextAccessor` and registered as scoped.
- `ContentService` localizes `GetById`, `GetBySlug`, `GetContents`, featured, trending, similar — top-level `title`/`description`/`director`/`cast` are swapped for the active language when a translation exists, with graceful fallback to the original columns.
- `ContentDetailDto` now carries the full `translations` array on every detail response.
- `CreateContentRequest` and `UpdateContentRequest` accept an optional `translations: [...]` array; update replaces the previous set atomically.
- Admin endpoints for translation management:
  - `GET / POST / DELETE /api/admin/contents/{contentId}/translations[/{lang}]`
  - `POST / DELETE /api/genres/{genreId}/translations[/{lang}]`
- Public endpoints:
  - `GET /api/contents/{id}/translations`
  - `GET /api/genres/{genreId}/translations`
- Migrations `AddContentTranslations` and `AddGenreTranslations` create the new tables, FK ON DELETE CASCADE, unique composite indexes, and idempotently backfill an `en` row from the existing primary columns for every non-deleted content/genre.
- 4 unit tests added in `ContentTranslationsTests` (create with translations, update replaces set, `?lang=` returns localized title with fallback, upsert idempotency for `(contentId, languageCode)`).

### Phase 2: Watchlist & Playlists (Prompt 5)
- New `Watchlist` entity (separate from `Favorites`) for save-for-later — carries optional `priority` and `note`. Soft-deleted entries are revived on re-add so duplicates can't accumulate.
- New `Playlist` and `PlaylistItem` entities for user-created collections, with ordered items and an `isPublic` flag.
- Public/private playlist visibility: public list endpoint, private playlists rejected for non-owners on detail.
- Item reordering via `PUT /api/playlists/{id}/reorder` — requires every existing item id exactly once; `OrderIndex` is reassigned by position.
- Partial unique indexes (`WHERE "IsDeleted" = false`) on `watchlists(UserId, ContentId)` and `playlist_items(PlaylistId, ContentId)` allow soft-deleted history to coexist with a single active row per pair.
- Cascade only on `playlist_items → playlists` (DB level); other relationships are `NoAction` to avoid cascade-cycle errors. Application-level soft-delete cascades items when a playlist is deleted.
- FluentValidation validators (`CreatePlaylistRequestValidator`, `UpdatePlaylistRequestValidator`, `AddItemToPlaylistRequestValidator`, `AddToWatchlistRequestValidator`).
- New controllers `WatchlistController` and `PlaylistsController`; all endpoints behind `[Authorize]` except public playlist read.
- Migration `AddWatchlistAndPlaylists` creates `watchlists`, `playlists`, `playlist_items` tables with the partial unique indexes above.
- 11 unit tests added (`WatchlistServiceTests` x5, `PlaylistServiceTests` x6) covering happy paths, ownership enforcement, dup prevention, pagination, reorder, public visibility, and soft-delete cascade.

### Phase 2: Review Votes & Moderation (Prompt 6)
- New `ReviewVote` entity with `Like` / `Dislike` toggle semantics — same-type vote cancels, opposite-type swaps and adjusts both counters in a single `SaveChangesAsync`. Soft-deleted votes are revived on re-vote so the partial unique index `(ReviewId, UserId) WHERE "IsDeleted" = false` keeps history clean.
- `Review.DislikesCount` and `Review.RejectionReason` fields added; existing `LikesCount` stays. Counters are mutated alongside the votes navigation, never via separate write paths.
- `ReviewDto` exposes `dislikesCount` and `myVote` (`"like"`/`"dislike"`/`null`). `myVote` is resolved per-request from `ICurrentUserService`, batched across the page of reviews.
- New `POST /api/reviews/{id}/vote` endpoint (authenticated) — body `{ "voteType": 1|2 }`, returns `{ likesCount, dislikesCount, myVote }`.
- New admin moderation pipeline:
  - `IAdminReviewService` + `AdminReviewService`.
  - `GET /api/admin/reviews?page=&pageSize=&contentId=&isApproved=&userId=&search=`.
  - `PUT /api/admin/reviews/{id}/approve` — folds the rating back into the content average if it was pending.
  - `PUT /api/admin/reviews/{id}/reject` — stores `RejectionReason`, pulls a previously-approved rating out of the average.
  - `DELETE /api/admin/reviews/{id}` — soft-delete with average rollback.
- Configurable `Reviews:RequireModeration` flag in `appsettings.json` (default `false`). When `true`, `POST /api/reviews` creates reviews with `IsApproved=false` and skips the average aggregation until an admin approves.
- `ICurrentUserService` and `IConfiguration` injected into `ReviewService`. `Microsoft.Extensions.Configuration.Abstractions` added to the Application project.
- 9 unit tests (`ReviewVoteTests` x6: like, toggle off, switch, not-found, two-user consistency, `myVote` per-caller; `AdminReviewTests` x3: approve, reject with reason, soft-delete).
- Migrations:
  - `AddReviewVotes` — creates `review_votes` with cascade FK to `reviews` and partial unique index on `(ReviewId, UserId)`.
  - `AddReviewModerationFields` — adds `DislikesCount` (NOT NULL DEFAULT 0) and `RejectionReason` (nullable text) columns to `reviews`.

### Phase 1: DTO Compatibility (Prompt 2)
- Global camelCase JSON serialization (`Program.cs` — `JsonNamingPolicy.CamelCase`, case-insensitive deserialization, omit-null on write, enums as camelCase strings).
- `POST /api/auth/login` accepts both `email` and `emailOrUsername` (legacy callers unaffected; service uses `GetIdentifier()`).
- `POST /api/auth/register` accepts `name` and auto-generates `username` (letters/digits, spaces → `_`, 20-char cap, collision-retry up to 5 times) and splits `name` into `firstName`/`lastName`.
- `AuthResponse` exposes a `token` alias of `accessToken` while keeping `accessToken`, `refreshToken`, `accessTokenExpiry`, and `user`.
- `UserDto` now includes `firstName`, `lastName`, `avatar` (alias of `avatarUrl`), `updatedAt`, and the role is emitted in lowercase (`"user"`/`"admin"`).
- New alias endpoint `GET /api/auth/profile` (delegates to `/api/auth/me`).
- New alias controller `MoviesAliasController` mapping `/api/movies`, `/api/movies/featured`, `/api/movies/trending`, `/api/movies/{id}`, `/api/movies/{id}/related`, `POST /api/movies/{id}/view` to the existing content services. Response envelope is the FE-shaped `{data, total, page, totalPages}`.
- New `MovieDto` with frontend-friendly field names (`poster`, `backdrop`, `trailer`, `streamUrl`, `duration`, `rating`, `genres: string[]`, `cast: string[]`, `quality`, `views`, `likes`, `dislikes`, i18n placeholders for `titleUz`/`titleRu`/`descriptionUz`/`descriptionRu`).
- New `Content.Quality` field (`VideoQuality` enum, default `HD=2`; existing `VideoQuality` enum extended with `UHD4K=4`).
- Migration `AddContentQualityField` adds `Quality` integer column to `contents` with default `2`.

### Backward compatibility
- Existing endpoints `/api/contents/*`, `/api/auth/me`, `/api/auth/login` with `emailOrUsername`, and the original `PagedResult<T>` envelope (`items`/`totalCount`/`pageSize`) all continue to work.
