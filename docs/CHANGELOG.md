# Changelog

All notable changes to the Playgo backend will be documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Phase 4: Subscriptions (Prompt 8)
- `Plan`, `UserSubscription`, `Payment` entities. Enums: `BillingPeriod` (Monthly/Quarterly/Yearly/Lifetime), `SubscriptionStatus` (Active/Cancelled/Expired/PendingPayment), `PaymentStatus` (Pending/Completed/Failed/Refunded), `PaymentProvider` (Click/Payme/Stripe/Manual).
- 3 default plans seeded via migration: `free` (0 UZS, 720p, ads), `premium_monthly` (49 000 UZS, 2160p, no ads, downloads), `premium_yearly` (490 000 UZS, yearly, 4 streams).
- `Content.IsPremium` flag and premium content gating in `ContentService` — `videoUrl` / `hlsManifestUrl` come back `null` for non-subscribers; `ContentDetailDto` exposes new `isPremium` and `requiresPremium` fields. New `GET /api/contents/{id}/stream` `[Authorize]` endpoint returns streaming URLs only for active subscribers (`403` otherwise).
- Payment provider abstraction via `IPaymentProviderService`. Stubs: `ClickPaymentProvider`, `PaymePaymentProvider`, `StripePaymentProvider`, `ManualPaymentProvider` — all return placeholder `https://demo-payment/...` URLs and signature verification is a TODO. Resolved by **keyed scoped DI** (`AddKeyedScoped<IPaymentProviderService, ...>(provider.ToString())`), looked up by `SubscriptionService` and `PaymentsController` callback handler.
- Subscribe / cancel flow: `POST /api/subscriptions` creates a `PendingPayment` subscription **and** `Pending` payment in a single `SaveChangesAsync` (atomic), returns `{ paymentId, paymentUrl }`. `POST /api/payments/callback/{provider}` (no auth) verifies signature stub and runs `PaymentService.MarkCompletedAsync`, which flips the payment to `Completed` and the subscription to `Active` with `ExpiresAt` recomputed from `BillingPeriod`. `POST /api/subscriptions/cancel` sets `Cancelled` + `CancelledAt`; `ExpiresAt` is preserved so the user keeps their paid days.
- `PlanService` caches active plans in Redis under `plans:active` with a 1-hour TTL; CRUD operations invalidate the cache.
- New controllers: `PlansController` (public), `AdminPlansController` (Admin CRUD), `SubscriptionsController` ([Authorize]), `PaymentsController` (mix of [Authorize], [Authorize(Roles=Admin)], and webhook).
- 11 unit tests: `PlanServiceTests` x3 (create, duplicate code rejected, active-only sorted by price), `SubscriptionServiceTests` x5 (subscribe creates pending Payment, callback activates Subscription, cancel preserves ExpiresAt, IsActive checks status+expiry, already-active prevents new subscription), `PaymentServiceTests` x3 (initiate creates Pending, MarkCompleted idempotent, per-user payment scoping).
- Migrations:
  - `AddIsPremiumToContent` — adds `IsPremium bool NOT NULL DEFAULT false` to `contents`.
  - `AddPlansSubscriptionsPayments` — creates `plans`, `user_subscriptions`, `payments` tables with indexes (`IX_plans_Code` unique, `IX_user_subscriptions_UserId_Status`, `IX_payments_UserId`, `IX_payments_ProviderTransactionId`) and seeds the 3 default plans.
- **No real money flows.** Every provider is a stub. Click/Payme/Stripe signature validation is a `// TODO`.

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

### Phase 3: Admin Features (Prompt 7)
- User ban functionality with reason and audit fields — `User.IsBanned`, `User.BanReason`, `User.BannedAt`. Ban clears `RefreshToken` so the user is effectively logged out immediately.
- New `ContentViewLog` entity for analytics — captures `ContentId`, optional `UserId`, `ViewedAt`, `IpAddress` (45 char max), `UserAgent` (500 char max). `POST /api/contents/{id}/view` now writes a log row alongside incrementing the counter; values are pulled from `ICurrentUserService` (extended with `IpAddress`/`UserAgent`).
- Admin Users CRUD (`/api/admin/users`) with search (username/email/fullName, case-insensitive), filters (role/isBanned/isEmailVerified), sort (`createdAt` default, `lastLoginAt`, `email`), pagination, ban/unban, self-protection (admin cannot ban or delete themselves), and a detail endpoint that bundles `reviewsCount` / `favoritesCount` / `watchlistCount` / `playlistsCount` plus the user's 10 most recent activities.
- Admin Dashboard Stats endpoint (`GET /api/admin/dashboard/stats`) returning totals (users, contents, views, reviews, favorites, watchlists), `newUsersThisMonth`, `publishedContents`/`draftContents`, recent movies (top 5), recent users (top 5), `topGenres` (top 5 by content count with `totalViews`), and `viewsLast30Days` (always 30 zero-filled entries built from `ContentViewLogs`).
- Redis caching foundation — `ICacheService` abstraction plus `RedisCacheService` implementation backed by `IDistributedCache`. `RemoveByPrefixAsync` uses `IConnectionMultiplexer` (`StackExchange.Redis`) and a `SCAN`-based key sweep. Dashboard stats cache key `admin:dashboard:stats` with a 60-second TTL.
- Authorization policies `AdminOnly` and `AdminOrModerator` registered in `Program.cs` for future per-policy attribute usage (existing controllers continue to use `[Authorize(Roles = "Admin")]`).
- Hangfire recurring job `cleanup-old-view-logs` (daily 03:00 UTC) soft-deletes `ContentViewLogs` older than 90 days.
- 8 new unit tests — `AdminUserServiceTests` x6 (search, filter, ban, unban, cannot-ban-self, cannot-delete-self) and `AdminDashboardServiceTests` x2 (stats aggregation + 60s cache hit on second call).
- Migrations:
  - `AddUserBanFields` — three new columns on `users`: `IsBanned bool NOT NULL DEFAULT false`, `BanReason text NULL`, `BannedAt timestamptz NULL`.
  - `AddContentViewLogs` — creates `content_view_logs` with indexes on `ContentId` and `ViewedAt`, FKs to `contents` and `users` (`NoAction` on delete; `UserId` nullable for anonymous views).

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
