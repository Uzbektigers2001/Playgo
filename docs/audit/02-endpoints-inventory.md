# 02 — Endpoints Inventory

> Every controller action in `src/Playgo.API/Controllers/` enumerated.
> Frontend mapping is sourced from the `playgo-frontend` Next.js 14 expectations summarised in `04-frontend-integration-gaps.md`.

Legend:
- **Auth**: ❌ anonymous · 🔐 JWT required · 👑 Admin role required
- **Status Codes** lists the codes the controller currently emits — *not* every theoretically possible code.
- Where the frontend has no equivalent page, `—` is used.

---

## Auth — `AuthController` (`/api/auth`)

| # | Method | Route | Auth | Roles | Controller:Line | Service | Request DTO | Response DTO | Status Codes | Frontend Page |
|---|--------|-------|------|-------|------------------|---------|-------------|--------------|--------------|----------------|
| 1 | POST | `/api/auth/register` | ❌ | — | `AuthController.cs:25` | `AuthService.RegisterAsync` | `RegisterRequest` (`AuthDtos.cs:3`) | `AuthResponse` (`AuthDtos.cs:26`) | 201 / 400 | `/auth/register` |
| 2 | POST | `/api/auth/login` | ❌ | — | `AuthController.cs:32` | `AuthService.LoginAsync` | `LoginRequest` (`AuthDtos.cs:9`) | `AuthResponse` | 200 / 400 | `/auth/login` |
| 3 | POST | `/api/auth/refresh` | ❌ | — | `AuthController.cs:39` | `AuthService.RefreshTokenAsync` | `RefreshTokenRequest` (`AuthDtos.cs:13`) | `AuthResponse` | 200 / 400 | (silent — auth interceptor) |
| 4 | POST | `/api/auth/logout` | 🔐 | any | `AuthController.cs:47` | `AuthService.LogoutAsync` | — | — | 204 / 400 / 401 | header menu |
| 5 | GET | `/api/auth/me` | 🔐 | any | `AuthController.cs:55` | `AuthService.GetCurrentUserAsync` | — | `UserDto` (`AuthDtos.cs:17`) | 200 / 400 / 401 | `/profile` (FE calls `/profile`) |
| 6 | PUT | `/api/auth/me` | 🔐 | any | `AuthController.cs:63` | `AuthService.UpdateProfileAsync` | `UpdateProfileRequest` (`AuthDtos.cs:32`) | `UserDto` | 200 / 400 / 401 | `/profile/edit` |
| 7 | PUT | `/api/auth/me/password` | 🔐 | any | `AuthController.cs:71` | `AuthService.ChangePasswordAsync` | `ChangePasswordRequest` (`AuthDtos.cs:36`) | — | 204 / 400 / 401 | `/profile/security` |

---

## Contents (public) — `ContentsController` (`/api/contents`)

| # | Method | Route | Auth | Roles | Controller:Line | Service | Request DTO | Response DTO | Status Codes | Frontend Page |
|---|--------|-------|------|-------|------------------|---------|-------------|--------------|--------------|----------------|
| 8 | GET | `/api/contents` | ❌ | — | `ContentsController.cs:19` | `ContentService.GetContentsAsync` | `ContentFilterRequest` (`ContentDtos.cs:142`) | `PagedResult<ContentListItemDto>` | 200 | `/movies` (FE calls `/movies`) |
| 9 | GET | `/api/contents/featured?limit=` | ❌ | — | `ContentsController.cs:26` | `ContentService.GetFeaturedAsync` | query `limit` | `List<ContentListItemDto>` | 200 | `/` (home hero) |
| 10 | GET | `/api/contents/trending?limit=` | ❌ | — | `ContentsController.cs:33` | `ContentService.GetTrendingAsync` | query `limit` | `List<ContentListItemDto>` | 200 | `/` (trending rail) |
| 11 | GET | `/api/contents/{id:guid}` | ❌ | — | `ContentsController.cs:40` | `ContentService.GetContentByIdAsync` | path `id` | `ContentDetailDto` (`ContentDtos.cs:44`) | 200 / 404 | `/movies/[id]` |
| 12 | GET | `/api/contents/slug/{slug}` | ❌ | — | `ContentsController.cs:47` | `ContentService.GetContentBySlugAsync` | path `slug` | `ContentDetailDto` | 200 / 404 | `/movies/[slug]` (optional) |
| 13 | GET | `/api/contents/{id:guid}/similar?limit=` | ❌ | — | `ContentsController.cs:54` | `ContentService.GetSimilarAsync` | path `id`, query `limit` | `List<ContentListItemDto>` | 200 | `/movies/[id]` → "related" |
| 14 | POST | `/api/contents/{id:guid}/view` | ❌ | — | `ContentsController.cs:61` | `ContentService.IncrementViewCountAsync` | path `id` | — | 204 | player onPlay |

---

## Contents (admin) — `AdminContentsController` (`/api/admin/contents`)

| # | Method | Route | Auth | Roles | Controller:Line | Service | Request DTO | Response DTO | Status Codes | Frontend Page |
|---|--------|-------|------|-------|------------------|---------|-------------|--------------|--------------|----------------|
| 15 | POST | `/api/admin/contents` | 🔐 | 👑 Admin | `AdminContentsController.cs:26` | `ContentService.CreateAsync` | `CreateContentRequest` (`ContentDtos.cs:72`) | `ContentDetailDto` | 201 / 400 / 401 / 403 | `/admin/movies/new` |
| 16 | PUT | `/api/admin/contents/{id:guid}` | 🔐 | 👑 Admin | `AdminContentsController.cs:33` | `ContentService.UpdateAsync` | `UpdateContentRequest` (`ContentDtos.cs:94`) | `ContentDetailDto` | 200 / 400 | `/admin/movies/[id]/edit` |
| 17 | DELETE | `/api/admin/contents/{id:guid}` | 🔐 | 👑 Admin | `AdminContentsController.cs:40` | `ContentService.DeleteAsync` | path `id` | — | 204 / 400 | `/admin/movies` |
| 18 | POST | `/api/admin/contents/{contentId:guid}/seasons` | 🔐 | 👑 Admin | `AdminContentsController.cs:47` | `SeasonEpisodeService.CreateSeasonAsync` | `CreateSeasonRequest` (`ContentDtos.cs:118`) | `SeasonDto` | 201 / 400 | `/admin/series/[id]/seasons` |
| 19 | POST | `/api/admin/contents/seasons/{seasonId:guid}/episodes` | 🔐 | 👑 Admin | `AdminContentsController.cs:55` | `SeasonEpisodeService.CreateEpisodeAsync` | `CreateEpisodeRequest` (`ContentDtos.cs:126`) | `EpisodeDto` | 201 / 400 | `/admin/series/[id]/episodes` |
| 20 | DELETE | `/api/admin/contents/seasons/{seasonId:guid}` | 🔐 | 👑 Admin | `AdminContentsController.cs:63` | `SeasonEpisodeService.DeleteSeasonAsync` | path | — | 204 / 400 | `/admin/series/...` |
| 21 | DELETE | `/api/admin/contents/episodes/{episodeId:guid}` | 🔐 | 👑 Admin | `AdminContentsController.cs:70` | `SeasonEpisodeService.DeleteEpisodeAsync` | path | — | 204 / 400 | `/admin/series/...` |

---

## Genres — `GenresController` (`/api/genres`)

| # | Method | Route | Auth | Roles | Controller:Line | Service | Request DTO | Response DTO | Status Codes | Frontend Page |
|---|--------|-------|------|-------|------------------|---------|-------------|--------------|--------------|----------------|
| 22 | GET | `/api/genres` | ❌ | — | `GenresController.cs:20` | `GenreService.GetAllAsync` | — | `List<GenreDto>` | 200 | `/movies` filter sidebar |
| 23 | POST | `/api/genres` | 🔐 | 👑 Admin | `GenresController.cs:28` | `GenreService.CreateAsync` | `CreateGenreRequest` (`ContentDtos.cs:137`) | `GenreDto` | 201 / 400 | `/admin/genres` |
| 24 | DELETE | `/api/genres/{id:guid}` | 🔐 | 👑 Admin | `GenresController.cs:36` | `GenreService.DeleteAsync` | path | — | 204 / 400 | `/admin/genres` |

---

## Reviews — `ReviewsController` (`/api/reviews`)

| # | Method | Route | Auth | Roles | Controller:Line | Service | Request DTO | Response DTO | Status Codes | Frontend Page |
|---|--------|-------|------|-------|------------------|---------|-------------|--------------|--------------|----------------|
| 25 | GET | `/api/reviews/content/{contentId:guid}` | ❌ | — | `ReviewsController.cs:21` | `ReviewService.GetReviewsForContentAsync` | path + query `page`,`pageSize` | `PagedResult<ReviewDto>` (`UserActivityDtos.cs:3`) | 200 | `/movies/[id]` comments tab |
| 26 | POST | `/api/reviews` | 🔐 | any | `ReviewsController.cs:33` | `ReviewService.CreateAsync` | `CreateReviewRequest` (`UserActivityDtos.cs:14`) | `ReviewDto` | 201 / 400 / 401 | `/movies/[id]` add comment |
| 27 | PUT | `/api/reviews/{id:guid}` | 🔐 | any (owner) | `ReviewsController.cs:42` | `ReviewService.UpdateAsync` | `UpdateReviewRequest` (`UserActivityDtos.cs:19`) | `ReviewDto` | 200 / 400 / 401 | `/movies/[id]` edit own |
| 28 | DELETE | `/api/reviews/{id:guid}` | 🔐 | any (owner) or 👑 Admin | `ReviewsController.cs:51` | `ReviewService.DeleteAsync` | path | — | 204 / 400 / 401 | `/movies/[id]` delete own |

---

## Favorites — `FavoritesController` (`/api/favorites`)

| # | Method | Route | Auth | Roles | Controller:Line | Service | Request DTO | Response DTO | Status Codes | Frontend Page |
|---|--------|-------|------|-------|------------------|---------|-------------|--------------|--------------|----------------|
| 29 | GET | `/api/favorites?page=&pageSize=` | 🔐 | any | `FavoritesController.cs:21` | `FavoriteService.GetUserFavoritesAsync` | query | `PagedResult<FavoriteDto>` | 200 / 401 | `/profile/favorites` |
| 30 | POST | `/api/favorites/{contentId:guid}` | 🔐 | any | `FavoritesController.cs:31` | `FavoriteService.AddAsync` | path | — | 204 / 400 / 401 | movie card heart icon |
| 31 | DELETE | `/api/favorites/{contentId:guid}` | 🔐 | any | `FavoritesController.cs:39` | `FavoriteService.RemoveAsync` | path | — | 204 / 400 / 401 | movie card heart icon |
| 32 | GET | `/api/favorites/{contentId:guid}/check` | 🔐 | any | `FavoritesController.cs:47` | `FavoriteService.IsFavoriteAsync` | path | `{ isFavorite: bool }` | 200 / 401 | per-card prefetch |

---

## Watch History — `WatchHistoryController` (`/api/watch-history`)

| # | Method | Route | Auth | Roles | Controller:Line | Service | Request DTO | Response DTO | Status Codes | Frontend Page |
|---|--------|-------|------|-------|------------------|---------|-------------|--------------|--------------|----------------|
| 33 | GET | `/api/watch-history?page=&pageSize=` | 🔐 | any | `WatchHistoryController.cs:22` | `WatchHistoryService.GetUserHistoryAsync` | query | `PagedResult<WatchHistoryDto>` (`UserActivityDtos.cs:23`) | 200 / 401 | `/profile/history` |
| 34 | GET | `/api/watch-history/continue-watching?limit=` | 🔐 | any | `WatchHistoryController.cs:32` | `WatchHistoryService.GetContinueWatchingAsync` | query | `List<WatchHistoryDto>` | 200 / 401 | `/` "Continue watching" rail |
| 35 | PUT | `/api/watch-history/progress` | 🔐 | any | `WatchHistoryController.cs:39` | `WatchHistoryService.UpdateProgressAsync` | `UpdateWatchProgressRequest` (`UserActivityDtos.cs:36`) | — | 204 / 400 / 401 | player onTimeUpdate |
| 36 | DELETE | `/api/watch-history` | 🔐 | any | `WatchHistoryController.cs:47` | `WatchHistoryService.ClearHistoryAsync` | — | — | 204 / 401 | `/profile/history` clear-all |

---

## Uploads — `UploadController` (`/api/upload`)

| # | Method | Route | Auth | Roles | Controller:Line | Service | Request DTO | Response DTO | Status Codes | Frontend Page |
|---|--------|-------|------|-------|------------------|---------|-------------|--------------|--------------|----------------|
| 37 | POST | `/api/upload/image` (multipart) | 🔐 | 👑 Admin | `UploadController.cs:27` | `IFileStorageService.UploadAsync` | `IFormFile` (jpeg/png/webp, ≤10 MiB) | `{ url: string }` | 200 / 400 / 401 / 403 | `/admin/movies/new` poster picker |
| 38 | POST | `/api/upload/video` (multipart) | 🔐 | 👑 Admin | `UploadController.cs:39` | `IFileStorageService.UploadAsync` | `IFormFile` (mp4/webm, ≤2 GiB) | `{ url: string }` | 200 / 400 / 401 / 403 | `/admin/movies/new` video picker |

---

## Infrastructure (non-controller routes)

| # | Method | Route | Auth | Source | Purpose |
|---|--------|-------|------|--------|---------|
| 39 | GET | `/health` | ❌ | `Program.cs:103` | Aggregated health check (Postgres + Redis) |
| 40 | GET | `/swagger`, `/swagger/v1/swagger.json` | ❌ (Dev only) | `Program.cs:86-90` | OpenAPI doc + UI |
| 41 | * | `/hangfire` | 🔐 👑 Admin | `Program.cs:97-100`, `Middleware/HangfireAdminAuthFilter.cs` | Hangfire dashboard |
| 42 | GET | `/{wwwroot/uploads/**}` | ❌ | `Program.cs:83` (`UseStaticFiles`) | Serves uploaded posters/videos |

---

## Quick stats

- **Controllers**: 8
- **HTTP endpoints (1-38)**: 38
- **Public (no auth)**: 14
- **Authenticated (any role)**: 17
- **Admin-only**: 7
- **Endpoints the frontend expects but backend lacks**: see `04-frontend-integration-gaps.md` — at least 6 routes (movies catalog under `/api/movies`, `/api/auth/profile`, `/api/movies/{id}/comments`, etc.).
