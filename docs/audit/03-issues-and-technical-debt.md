# 03 — Issues & Technical Debt

> Severity scale: **Critical** (ship-blocker / data-loss / RCE) · **High** (real prod risk) · **Med** (degraded UX or scale) · **Low** (clean-up).
> Every row links to `path:line` for verification.

---

## 1. Security Issues

| # | Issue | File:Line | Severity | Suggested Fix |
|---|-------|-----------|----------|---------------|
| S1 | JWT signing secret committed to source: `"CHANGE_THIS_IN_PRODUCTION_MIN_32_CHARS_LONG_SECRET_KEY"`. The string is also the validation key in `Program.cs`. | `src/Playgo.API/appsettings.json:7`, `src/Playgo.API/appsettings.Development.json:7`, `Program.cs:45` | **Critical** | Move to environment variable / user-secrets / Vault. Fail-fast on startup if `JwtSettings:Secret` is missing or < 32 bytes. Rotate after exposure. |
| S2 | Hardcoded DB password `playgo123` / `5707` in both appsettings files. | `appsettings.json:3`, `appsettings.Development.json:3` | **Critical** | Use `${DB_PASSWORD}` substitution via env var (already documented in `.env.example` but not wired). Remove plaintext from repo and `git-secrets` scan history. |
| S3 | No rate limiting on auth endpoints — `/api/auth/login`, `/api/auth/register`, `/api/auth/refresh` are unprotected against brute-force / credential stuffing. | `Program.cs` (no `UseRateLimiter`); `AuthController.cs:25,32,39` | **Critical** | Add `Microsoft.AspNetCore.RateLimiting` with stricter token-bucket per IP for `/api/auth/*` (e.g. 10 req/min) and global fixed window elsewhere. |
| S4 | No email verification flow — `User.IsEmailVerified` exists but is never set to `true` and never enforced anywhere. | `Domain/Entities/User.cs:17`; `AuthService.cs:37-44` (no token issued, no SMTP) | **High** | Add `EmailVerificationToken` entity + endpoint `POST /api/auth/verify-email`; block login or downgrade scope until verified. Send via SMTP/SendGrid. |
| S5 | Weak password policy: only `MinimumLength(6)`, no complexity/breached-password check. | `src/Playgo.Application/Validators/Validators.cs:23` | **High** | Raise to 10+ with at least one digit + letter; integrate `HaveIBeenPwned` k-anonymity API in `BcryptPasswordHasher` or validator. |
| S6 | Refresh tokens stored as plaintext in `users.RefreshToken`. A DB leak hands attackers active sessions. | `UserConfiguration.cs:24`; `JwtTokenService.cs:56-60`; `AuthService.cs:49-50` | **High** | Hash refresh tokens (SHA-256) before persisting; rotate-only-once family detection. |
| S7 | Login responses don't distinguish "invalid email" vs "invalid password" — good — but `register` leaks "Email is already registered" vs "Username is already taken", which enables user enumeration. | `AuthService.cs:31-35` | **Med** | Return a single generic error or use a queue-based confirmation email for both cases. |
| S8 | CORS allows credentials with any origin from a list, but `AllowAnyHeader` + `AllowAnyMethod` + `AllowCredentials` is permissive and origins include `http://localhost:*` even in prod build. | `Program.cs:56-63`; `appsettings.json:17` | **Med** | Move allowed origins to env var; tighten allowed methods to the ones actually used; never combine credentials with `*` (already not the case here, just verify on deploy). |
| S9 | No security headers middleware — no HSTS preload, CSP, X-Content-Type-Options, X-Frame-Options. | `Program.cs:80-103` | **Med** | Add `UseSecurityHeaders` (NWebSec or custom middleware) before `UseRouting`. |
| S10 | `LocalFileStorageService` accepts unsanitized file extensions — only content-type is validated, attackers can upload `evil.exe` claiming `image/png`. Files are then served by `UseStaticFiles` directly. | `Services/LocalFileStorageService.cs:26-30`; `UploadController.cs:49-60`; `Program.cs:83` | **High** | Whitelist extensions, sniff magic bytes, strip executable bits, serve through a controller that sets `Content-Disposition` and `X-Content-Type-Options: nosniff`. |
| S11 | Hangfire dashboard mounted at fixed `/hangfire` with only role-based auth — discoverable. | `Program.cs:97-100`; `Middleware/HangfireAdminAuthFilter.cs:7-13` | **Low** | Move behind an unguessable path via config; restrict by IP allow-list in prod. |
| S12 | `Authorize(Roles = "Admin")` uses the literal string "Admin" but `JwtRegisteredClaimNames` writes role as the enum `.ToString()` — that's also "Admin" so it works today, but any rename of `UserRole.Admin` silently breaks authorization. | `JwtTokenService.cs:37`; `AuthController.cs`, `AdminContentsController.cs:11`, etc. | **Low** | Introduce a `Roles` static constant class and use it in both `JwtTokenService` and `[Authorize]`. |

---

## 2. Performance Issues

| # | Issue | File:Line | Severity | Suggested Fix |
|---|-------|-----------|----------|---------------|
| P1 | Catalog search uses `ToLower().Contains(s)` on `Title`, `OriginalTitle`, `Description` — defeats every B-tree index and forces a full table scan. | `Application/Services/ContentService.cs:30-37` | **High** | Add a Postgres `tsvector` column + GIN index, use `EF.Functions.ToTsVector` + `Matches`; or `citext` + `ILIKE` with trigram index for prefix search. |
| P2 | Listing/paged endpoints use OFFSET pagination (`Skip(...).Take(...)`) — degrades linearly past page ~50. | `ContentService.cs:62-64`, `FavoriteService.cs:32-33`, `ReviewService.cs:32-33`, `WatchHistoryService.cs:33-34` | **Med** | Add keyset/cursor pagination using `(CreatedAt, Id)` or `(LastWatchedAt, Id)`. Keep `page` for back-compat but document max page. |
| P3 | Redis registered as `IDistributedCache` but **no service caches anything** — featured/trending/genres hit the DB every request. | `Infrastructure/DependencyInjection.cs:26-31`; no `IDistributedCache` consumer anywhere | **High** | Wrap `GetFeaturedAsync`, `GetTrendingAsync`, `GetAllAsync` (genres), and `GetContentBySlugAsync` with cache-aside (5–15 min TTL) and invalidate on admin write. |
| P4 | `ContentService.GetContentsAsync` always `Include`s `ContentGenres → Genre` even for list views — Cartesian explosion-light but unnecessary data. | `ContentService.cs:27` | **Med** | Project directly to `ContentListItemDto` in the query so EF emits a single flat SELECT; the genre names list can be done via correlated subquery. |
| P5 | `IncrementViewCountAsync` is a load-modify-save round trip — race-prone and N requests = N updates. | `ContentService.cs:243-248` | **High** | Use `ExecuteUpdateAsync` (`ViewCount + 1`), or batch via a Redis counter flushed periodically by a Hangfire job. |
| P6 | Missing indexes on common filter columns: `contents.ReleaseYear`, `contents.AverageRating`, `contents.IsTrending`, `contents.CreatedAt`. | `ContentConfiguration.cs:39-40` | **Med** | Add a composite `(Status, IsDeleted, CreatedAt DESC)` index for the default sort and individual indexes for filters. |
| P7 | `ClearHistoryAsync` loads every history row into memory then flips `IsDeleted`. For heavy users this is a giant `SELECT *`. | `WatchHistoryService.cs:125-133` | **Med** | Use `ExecuteUpdateAsync(w => new {IsDeleted=true, UpdatedAt=UtcNow})` (EF 8). |
| P8 | `LoadWithRelationsAsync` issues nested `.Include(...).ThenInclude(...)` with predicate filters — produces a heavy multi-LEFT-JOIN query for a single content detail page. | `ContentService.cs:251-262` | **Med** | Split into two queries: one for content + genres, a second for `Seasons → Episodes`; combine in code. |
| P9 | `HangfireJobs.RecalculateTrendingAsync` reads **every** published content into memory, then writes. | `Identity/HangfireJobsSetup.cs:56-82` | **Med** | Compute the top-20 set, then run two `ExecuteUpdateAsync` calls (one sets `IsTrending=true` for `IN (topIds)`, the other `false` for `IsTrending=true AND Id NOT IN (topIds)`). |
| P10 | `Review.CreateAsync` recomputes average rating in C# instead of using SQL aggregate — drifts over time with concurrent writes and `IsDeleted` reviews. | `ReviewService.cs:76-79` | **Med** | After save, recompute `AVG(Rating)` and `COUNT(*)` over approved + non-deleted reviews for that content using a single SQL projection. |

---

## 3. Architecture Issues

| # | Issue | File:Line | Severity | Suggested Fix |
|---|-------|-----------|----------|---------------|
| A1 | Two service-interfaces files coexist (`Application/Common/Interfaces/IServices.cs` for infra abstractions and `Application/Services/IServices.cs` for app services) — easy to grep into the wrong one. | `src/Playgo.Application/Common/Interfaces/IServices.cs`; `src/Playgo.Application/Services/IServices.cs` | **Low** | Split: keep infra contracts in `Common/Interfaces/*.cs`, app contracts under `Services/IAuthService.cs` etc., one per file. |
| A2 | DTOs leak `Domain` enums (`ContentType`, `ContentStatus`) directly through API responses (`ContentListItemDto`, `ContentDetailDto`). A future enum rename ripples to clients. | `Application/DTOs/Content/ContentDtos.cs:36, 50, 51` | **Med** | Either pin enum values via `[JsonStringEnumConverter]` (already global) and treat them as a public contract — or map to string constants explicitly. |
| A3 | Controllers each carry private `HandleResult / HandleCreated / HandleNoContent` helpers (3 duplicates in `AuthController` + `AdminContentsController`, ad-hoc in others). | `AuthController.cs:77-93`; `AdminContentsController.cs:76-92`; `FavoritesController.cs:33-44`; etc. | **Med** | Promote to an `ApiControllerBase` (Result→IActionResult) or an extension `result.ToActionResult()`. |
| A4 | `IApplicationDbContext` exposes `DbSet<>` directly, which means services can call `Include`, `OrderBy`, etc. — the abstraction is a thin shim, not a true repository. | `Application/Common/Interfaces/IApplicationDbContext.cs:6-19`; usage in `ContentService.cs:25-72` | **Low** | Either accept the trade-off (current Clean Architecture-lite is reasonable) and document, or introduce specifications/repositories per aggregate. |
| A5 | Service methods that look up the current user re-read from DB on every action (`AuthService.GetCurrentUserAsync`). | `AuthService.cs:122-129` | **Low** | Optional: bind a per-request `User` object once on auth success and inject via `ICurrentUserService`. |
| A6 | `Program.cs` does too much: JWT, CORS, Swagger, Hangfire, auto-migrate all inline. Hard to test, hard to swap. | `Program.cs:33-114` | **Low** | Extract each concern into an `IServiceCollection` extension (e.g. `services.AddPlaygoAuth(config)`, `services.AddPlaygoCors(config)`). |
| A7 | Validators are registered but the pipeline disables ASP.NET model validation (`SuppressModelStateInvalidFilter = true`) — validation only runs if a service manually invokes the validator. **It currently doesn't**: no service injects `IValidator<>`. | `Program.cs:29-30`; `Application/DependencyInjection.cs:19`; `AuthService.RegisterAsync` (no validator call) | **High** | Either re-enable model validation, or add a FluentValidation MVC integration / pipeline filter, or call `await validator.ValidateAndThrowAsync(request)` in each service entry point. |
| A8 | `IncrementViewCountAsync` returns `Task` (no `Result`) and silently no-ops on "not found", which mixes failure modes with success. | `ContentService.cs:243-249` | **Low** | Return `Result` and return `Fail("Content not found.")` for clarity; controller can still translate to 204. |

---

## 4. Missing Features (vs README Roadmap + FE expectations + scattered TODOs)

| # | Item | Source | Severity | Notes |
|---|------|--------|----------|-------|
| M1 | HLS transcoding / FFmpeg worker | `README.md` Roadmap line 1 | **High** | Player will demand `.m3u8`; `Episode.HlsManifestUrl` exists but is set manually. |
| M2 | CDN signed URLs (Bunny.net) | `README.md` Roadmap line 2 | **High** | Required for paid content protection. |
| M3 | Full-text search (Postgres tsvector) | `README.md` Roadmap line 3 + P1 | **High** | See `P1`. |
| M4 | Keyset / cursor pagination | `README.md` Roadmap line 4 + P2 | **Med** | |
| M5 | Redis caching | `README.md` Roadmap line 5 + P3 | **High** | |
| M6 | Email verification | `README.md` Roadmap line 6 + S4 | **High** | |
| M7 | Admin analytics dashboard endpoint | `README.md` Roadmap line 7 | **Med** | Frontend `/admin/dashboard` is a stub today. |
| M8 | Comments endpoint at `/api/movies/{id}/comments` | Frontend contract (see `04-frontend-integration-gaps.md`) | **High** | Backend uses `/api/reviews?contentId=…`. |
| M9 | i18n title/description (`titleUz`, `titleRu`, `descriptionUz`, `descriptionRu`) | Frontend contract | **High** | Domain has only single-locale fields (`Content.Title`, `Content.Description`). |
| M10 | UserPreferences (language / quality / autoplay) | Frontend contract | **High** | No table / column today. |
| M11 | Watchlist / Playlists | Frontend contract | **Med** | `Favorite` only covers a single saved list; playlists need a join entity. |
| M12 | Likes / Dislikes / Vote on reviews | Frontend `likes`/`dislikes` on Movie; `LikesCount` on Review unused | **Med** | `Review.LikesCount` exists but is never incremented anywhere. |
| M13 | Review moderation (`IsApproved=false` flow) | `Review.IsApproved` defaults `true` — moderation queue missing | **Med** | Add admin endpoint to approve/reject. |
| M14 | Subscriptions / Plans | Frontend `user.subscription` | **Low** | Roadmap item for Phase 4. |
| M15 | Admin user-management endpoints (list, role-edit, ban) | No controller exists | **Med** | Required for `/admin/users`. |
| M16 | Test suite | `tests/Playgo.Tests/UnitTest1.cs` is the default xUnit stub | **High** | No coverage for `AuthService`, `ContentService` aggregation, etc. |
| M17 | Seed data script | None in repo | **Med** | Frontend cannot run without genres + a few contents. |
| M18 | CI workflow (`.github/workflows/*`) | Not present at repo root | **Med** | Build + tests + migration check on PR. |

---

## 5. Code Quality

| # | Issue | File:Line | Severity | Suggested Fix |
|---|-------|-----------|----------|---------------|
| Q1 | Magic strings: `"sub"`, `"username"`, `"Admin"`, `"Bearer"`, `"images"`, `"videos"`, `"playgo:"`. | `Extensions/ClaimsExtensions.cs:9,13,16`; `AuthController.cs`; `ReviewsController.cs:53`; `UploadController.cs:33,45`; `Infrastructure/DependencyInjection.cs:30` | **Low** | Constants class (`AuthClaims`, `Roles`, `UploadFolders`). |
| Q2 | Two near-identical `GenerateSlug` methods. | `Application/Services/ContentService.cs:264-270`; `Application/Services/GenreService.cs:65-71` | **Low** | Extract to `Application/Common/Slug.cs`. |
| Q3 | Three near-identical `HandleResult/HandleCreated/HandleNoContent` controller helpers. | `AuthController.cs:77-93`; `AdminContentsController.cs:76-92`; ad-hoc copies in other controllers | **Low** | See A3. |
| Q4 | `RegisterRequestValidator` checks username regex but the service downstream does not enforce case-insensitivity uniqueness for username. | `Validators/Validators.cs:11-15` vs `AuthService.cs:34` | **Low** | Lowercase username at validation + DB level, or use `citext`. |
| Q5 | No XML docs on public services/controllers — Swagger renders empty descriptions. | All controllers; all services | **Low** | Enable `<GenerateDocumentationFile>true</GenerateDocumentationFile>` and write `///` summaries for at least public endpoints. |
| Q6 | `ContentFilterRequest.PageSize` has no upper bound — `?pageSize=100000` would happily try to load 100k rows. | `Application/DTOs/Content/ContentDtos.cs:151`; `ContentService.cs:23` | **Med** | Clamp to `Math.Min(pageSize, 100)`. |
| Q7 | `Content.Cast` is stored as a single comma-joined `text` column — querying "movies starring X" is impossible. | `Domain/Entities/Content.cs:25`; `ContentConfiguration.cs` (no max length on Cast) | **Low** (now), **High** (when search adds actor filter) | Promote to a `ContentCastMember` join table when search needs it. |
| Q8 | Inconsistent shape: `ContentListItemDto.Genres` is `List<string>` but `ContentDetailDto.Genres` is `List<GenreDto>` — clients have to handle both. | `Application/DTOs/Content/ContentDtos.cs:42, 69` | **Low** | Pick one (frontend already wants `string[]` — see `04-frontend-integration-gaps.md`). |
| Q9 | `appsettings.Development.json` has a real-looking password (`5707`) committed. | `src/Playgo.API/appsettings.Development.json:3` | **Med** | Replace with placeholder and add to `.gitignore` if it ever holds real creds. |
| Q10 | `auto-migrate on startup in Development only` is fine, but no explicit guard on Production — silent if someone flips `ASPNETCORE_ENVIRONMENT`. | `Program.cs:106-111` | **Low** | Log a warning if migrations are pending in non-Dev. |
| Q11 | TODO/Roadmap items live only in `README.md` and inside this audit — no GitHub Issues mirror. | `README.md:127-134` | **Low** | Open one issue per Phase from `05-roadmap.md`. |
