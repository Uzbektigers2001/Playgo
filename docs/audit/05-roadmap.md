# 05 — Roadmap

> Five phases mapped 1:1 to the upcoming prompts (2–10). Phase priority follows the unblocking order: make the frontend speak to the backend, then grow the domain, then admin, then commerce, then harden for production.

Reading guide for each phase:
- **Goals** — what "done" looks like.
- **Files to change** — pre-scoped surface area (paths only, no edits in this doc).
- **New migrations** — EF migration count + name skeletons.
- **Breaking changes** — explicit YES/NO, with mitigation if YES.
- **Estimated time** — single engineer, focused work.

---

## Phase 1 — Compatibility · `Critical`

### Prompt 2 — DTO compatibility & route aliases

**Goals**
- Frontend's `playgo-frontend` can log in, browse `/movies`, open a movie, and post a comment **without code changes**.
- Closes every Critical + most High rows from `04-frontend-integration-gaps.md` that have solution strategy A or B.
- Concretely:
  - `POST /api/auth/login` accepts `{email, password}` and returns `{token, user, refreshToken, accessTokenExpiry}`.
  - `POST /api/auth/register` accepts `{name, email, password}`.
  - `GET /api/auth/profile` mirrors `/me`.
  - `GET /api/movies*`, `/api/movies/{id}`, `/api/movies/{id}/related`, `/api/movies/{id}/comments` aliased to existing services.
  - `PagedResult<T>` serialised as `{data, total, page, totalPages}` for FE-facing endpoints (keep current shape behind a `v=raw` flag if needed).
  - `Content*Dto` exposes `poster`, `backdrop`, `trailer`, `streamUrl`, `duration`, `rating`, `views`, `genres: string[]`, `cast: string[]`, plus camelCased role string.

**Files to change**
- `src/Playgo.Application/DTOs/Auth/AuthDtos.cs` (new property aliases, optional `Name` adapter, `Token` mirror).
- `src/Playgo.Application/DTOs/Content/ContentDtos.cs` (FE-friendly DTOs or `[JsonPropertyName]` attributes; unify genres + cast).
- `src/Playgo.Application/DTOs/Content/UserActivityDtos.cs` (`content` alias for `comment`).
- `src/Playgo.Application/Common/Result.cs` (FE-shaped serialization for `PagedResult<T>` or new `PagedResponse<T>`).
- `src/Playgo.Application/Services/AuthService.cs` (auto-derive `Username` from email when `RegisterRequest.Name` is supplied).
- `src/Playgo.Application/Services/ContentService.cs` (extend `ContentFilterRequest` with `limit`, `genre` slug, `minRating`; split `Cast` CSV in mapper).
- `src/Playgo.API/Controllers/ContentsController.cs` → add alias controller `MoviesController` or `[Route("api/movies")]` second attribute.
- New `src/Playgo.API/Controllers/MoviesController.cs` (thin router → existing `IContentService`/`IReviewService`).
- `src/Playgo.API/Controllers/AuthController.cs` (add `[HttpGet("profile")]` action delegating to `Me`).
- `src/Playgo.API/Controllers/ReviewsController.cs` (add `/api/movies/{id}/comments` aliases).

**New migrations** — none. (Pure DTO/route work.)

**Breaking changes** — **NO** for the frontend (only additive); **YES (mild)** for Swagger consumers that depended on `items/totalCount` exactly.
Mitigation: keep the original `PagedResult<T>` shape on `/api/contents/*` and only emit the FE shape on `/api/movies/*`, OR add both `items` and `data` aliases via a custom JSON converter.

**Estimated time** — 1.5–2 days.

---

## Phase 2 — Domain Extensions · `High`

### Prompt 3 — UserPreferences

**Goals**
- Domain entity `UserPreference` (1:1 with `User`) capturing `Language`, `PreferredQuality`, `AutoplayEnabled`, plus `UpdatedAt`.
- Endpoints `GET /api/auth/preferences` and `PUT /api/auth/preferences`.
- `UserDto` includes a nested `preferences` object.

**Files to change**
- `src/Playgo.Domain/Entities/User.cs` (navigation to `UserPreference`).
- New `src/Playgo.Domain/Entities/UserPreference.cs`.
- `src/Playgo.Infrastructure/Persistence/ApplicationDbContext.cs` (`DbSet<UserPreference>`).
- New `src/Playgo.Infrastructure/Persistence/Configurations/UserPreferenceConfiguration.cs`.
- `src/Playgo.Application/Common/Interfaces/IApplicationDbContext.cs` (new `DbSet`).
- `src/Playgo.Application/DTOs/Auth/AuthDtos.cs` (`UserPreferenceDto`, request DTOs).
- `src/Playgo.Application/Services/AuthService.cs` (default-preference creation on register, mapping into `UserDto`).
- `src/Playgo.API/Controllers/AuthController.cs` (two new endpoints).

**New migrations** — **1**: `AddUserPreferences`.

**Breaking changes** — **NO**. `UserDto.preferences` is additive; default values are filled server-side.

**Estimated time** — 1 day.

---

### Prompt 4 — i18n `ContentTranslation`

**Goals**
- Move per-locale strings out of `Content` and into a `ContentTranslation { ContentId, Locale, Title, Description, ShortDescription }`.
- Backfill migration: copy current `Content.Title`/`Description` into `ContentTranslation` rows with `Locale='uz'` (assumption; adjust if `Content.Language` reveals otherwise).
- DTOs expose `titleUz`, `titleRu`, `descriptionUz`, `descriptionRu` populated from translations.
- Catalog listing accepts `?lang=` and sorts/filters in that locale.

**Files to change**
- `src/Playgo.Domain/Entities/Content.cs` (remove single-language properties OR mark obsolete; add navigation).
- New `src/Playgo.Domain/Entities/ContentTranslation.cs`.
- `src/Playgo.Infrastructure/Persistence/Configurations/ContentTranslationConfiguration.cs`.
- `src/Playgo.Application/DTOs/Content/ContentDtos.cs` (translations array + computed FE fields).
- `src/Playgo.Application/Services/ContentService.cs` (write/read across translations).
- `src/Playgo.Application/Validators/Validators.cs` (per-locale rules).

**New migrations** — **2**: `AddContentTranslations` (schema) + `BackfillContentTranslations` (data; SQL inside `Up`).

**Breaking changes** — **YES**: `Content.Title` / `Content.Description` semantics change (default locale lookup).
Mitigation: keep `Content.Title` as a denormalised "default locale" column for one release; document deprecation; add an integration test that `titleUz` after backfill equals the old `Title`.

**Estimated time** — 2 days.

---

### Prompt 5 — Watchlist & Playlists

**Goals**
- Distinguish `Favorite` (heart) from `Watchlist` ("watch later") from `Playlist` (user-named lists of contents).
- Endpoints:
  - `GET/POST/DELETE /api/watchlist/{contentId}`.
  - `GET/POST/PUT/DELETE /api/playlists`, `/api/playlists/{id}/items`.

**Files to change**
- `src/Playgo.Domain/Entities/UserActivity.cs` (or new files): add `Watchlist`, `Playlist`, `PlaylistItem` entities.
- `src/Playgo.Infrastructure/Persistence/ApplicationDbContext.cs` + configurations for each.
- `src/Playgo.Application/DTOs/Content/UserActivityDtos.cs` (new DTOs).
- `src/Playgo.Application/Services/*.cs` — two new services: `IWatchlistService`, `IPlaylistService`.
- `src/Playgo.API/Controllers/WatchlistController.cs`, `PlaylistsController.cs`.

**New migrations** — **1**: `AddWatchlistAndPlaylists`.

**Breaking changes** — **NO**. New surface area only.

**Estimated time** — 2 days.

---

### Prompt 6 — Review Votes & Moderation

**Goals**
- `ReviewVote { ReviewId, UserId, Value=±1 }` to drive `LikesCount`/`DislikesCount` on reviews and content-level `LikesCount`/`DislikesCount` aggregates.
- Moderation queue: review create defaults to `IsApproved=false`; admin endpoints to approve/reject (`/api/admin/reviews?status=pending`, `PUT /api/admin/reviews/{id}/approve|reject`).
- Frontend sees `likes`/`dislikes` counters on the movie detail page.

**Files to change**
- `src/Playgo.Domain/Entities/UserActivity.cs` (add `ReviewVote`, add `DislikesCount` to `Review`, add `LikesCount`/`DislikesCount` to `Content`).
- Configurations under `src/Playgo.Infrastructure/Persistence/Configurations/`.
- `src/Playgo.Application/Services/ReviewService.cs` + new `IReviewModerationService`.
- New controller `src/Playgo.API/Controllers/AdminReviewsController.cs`.

**New migrations** — **1**: `AddReviewVotesAndModeration`.

**Breaking changes** — **YES (mild)**: existing reviews flip `IsApproved` default from `true` (in code, `UserActivity.cs:42`) to `false`. Mitigation: in the migration's `Up`, set `IsApproved = true` for every existing row (`UPDATE reviews SET "IsApproved"=true WHERE "CreatedAt" < now();`).

**Estimated time** — 2 days.

---

## Phase 3 — Admin Features · `Medium`

### Prompt 7 — Admin Users & Dashboard

**Goals**
- `GET /api/admin/users?search=&role=&page=&pageSize=` listing.
- `PUT /api/admin/users/{id}/role` and `POST /api/admin/users/{id}/ban` / `unban`.
- `GET /api/admin/dashboard` returning counters (users, active subscriptions, content by status, views last 30d, reviews pending).
- `User.IsBanned` flag (defaults false), index on `LastLoginAt`.

**Files to change**
- `src/Playgo.Domain/Entities/User.cs` (add `IsBanned`, `BannedUntil`).
- New `src/Playgo.Application/Services/IAdminUserService.cs` + `AdminUserService.cs`.
- New `src/Playgo.Application/Services/IDashboardService.cs` + impl using SQL projection.
- New controllers `src/Playgo.API/Controllers/AdminUsersController.cs`, `AdminDashboardController.cs`.

**New migrations** — **1**: `AddUserModeration`.

**Breaking changes** — **NO**.

**Estimated time** — 2 days.

---

## Phase 4 — Business · `Low`

### Prompt 8 — Subscriptions & Plans

**Goals**
- `Plan { Id, Code, Name, PriceUzs, DurationDays, Features json }`.
- `Subscription { Id, UserId, PlanId, StartsAt, ExpiresAt, Status=active|expired|cancelled, AutoRenew }`.
- `GET /api/plans` (public), `POST /api/subscriptions` (create), `POST /api/subscriptions/cancel`.
- `UserDto.subscription` populated.
- No real payment gateway in this phase — stub a `IPaymentProvider` interface.

**Files to change**
- `src/Playgo.Domain/Entities/Plan.cs`, `Subscription.cs`.
- `src/Playgo.Application/Services/IPlanService.cs`, `ISubscriptionService.cs`.
- `src/Playgo.Application/Common/Interfaces/IPaymentProvider.cs` (with a no-op `MockPaymentProvider`).
- New controllers `PlansController`, `SubscriptionsController`.

**New migrations** — **1**: `AddPlansAndSubscriptions` (+ seed inserts for `Free`, `Premium` plans).

**Breaking changes** — **NO**.

**Estimated time** — 2 days.

---

## Phase 5 — Production Readiness · `Critical`

### Prompt 9 — Cache, Rate Limit, Email

**Goals**
- Wrap `IContentService.GetFeaturedAsync` / `GetTrendingAsync` / `GetAllAsync` (genres) with cache-aside (`IDistributedCache`), TTL 10 minutes, invalidate on admin write (S1 mitigation in `03-issues-and-technical-debt.md` P3).
- `AddRateLimiter` policies:
  - `auth` — 10 req/min/IP for `/api/auth/login|register|refresh`.
  - `global` — 200 req/min/IP fallback.
- SMTP / SendGrid abstraction `IEmailSender`; verification email on register; password-reset flow.
- Tightens S1 (`JwtSettings:Secret` must come from env var; throw on startup if missing or short).

**Files to change**
- New `src/Playgo.Infrastructure/Caching/CachedContentService.cs` (decorator) + DI registration.
- `src/Playgo.API/Program.cs` (rate limiter middleware between auth and controllers; HSTS; security headers).
- New `src/Playgo.Infrastructure/Email/SmtpEmailSender.cs` + `EmailVerificationToken` entity.
- `src/Playgo.Application/Services/AuthService.cs` (issue verification token; enforce `IsEmailVerified` on login if configured).
- New controller `src/Playgo.API/Controllers/AuthVerificationController.cs`.
- `appsettings.json` (`RateLimit`, `Smtp` sections).

**New migrations** — **1**: `AddEmailVerificationTokens` (and `PasswordResetTokens` if combined).

**Breaking changes** — **YES**: users who never verified will be locked out if `RequireEmailVerified=true`. Mitigation: ship a config toggle defaulting to `false`; backfill `IsEmailVerified=true` for existing users in the migration.

**Estimated time** — 2.5–3 days.

---

### Prompt 10 — Tests, Seed, CI

**Goals**
- Test pyramid:
  - Unit tests for `AuthService`, `ContentService` aggregation, `ReviewService` rating math.
  - Integration tests (xUnit + `WebApplicationFactory` + Testcontainers Postgres) for auth + catalog flows.
  - Snapshot tests for endpoint JSON shape (locks in the FE contract from Phase 1).
- Idempotent seed data via a new `dotnet run --project tools/Playgo.SeedTool` or an EF migration with `HasData` for `Genres`, plus a few `Content` rows.
- GitHub Actions workflow: build → test → migrate-check → docker build on `main`/PR.

**Files to change**
- `tests/Playgo.Tests/` — replace `UnitTest1.cs` with real test classes; new project `Playgo.IntegrationTests` if scope grows.
- New `tools/Playgo.SeedTool/` console project or `Infrastructure/Persistence/Seed/SeedData.cs`.
- New `.github/workflows/ci.yml`.
- Update `Dockerfile` / `docker-compose.override.yml` to align with CI image tags.

**New migrations** — optional **1**: `SeedReferenceData` (if `HasData` route chosen).

**Breaking changes** — **NO**.

**Estimated time** — 3 days.

---

## Roll-up

| Phase | Prompts | Migrations | Breaking? | Effort |
|-------|---------|------------|-----------|--------|
| 1 — Compatibility | 2 | 0 | No (additive) | 1.5–2 d |
| 2 — Domain Extensions | 3, 4, 5, 6 | 5 | Prompt 4 + 6 (mitigated) | 7 d |
| 3 — Admin Features | 7 | 1 | No | 2 d |
| 4 — Business | 8 | 1 | No | 2 d |
| 5 — Production Readiness | 9, 10 | 1 (+ optional seed) | Prompt 9 (mitigated) | 5.5–6 d |

**Total estimated calendar effort: ~17–19 engineer-days.**

Start with Prompt 2 (Phase 1) — it unblocks every frontend page without touching the schema and gives the team a working integration to test against. Prompt 3 (UserPreferences) is the smallest Phase 2 step and a good warm-up for Prompts 4–6 which add real schema.
