# 04 — Frontend Integration Gaps

> Compares the contract `playgo-frontend` (Next.js 14) consumes against what the .NET backend currently emits.
> Frontend expectations are the ones stated in the audit brief; backend evidence is anchored to `path:line`.

Severity scale: **Critical** (page cannot render) · **High** (feature broken or wrong field name) · **Med** (cosmetic / fallbackable) · **Low** (nice-to-have).

Solution strategies (referenced in the table):
- **A — DTO rename / alias**: surface the existing field under the FE name (JSON property attribute or new mapping DTO). Cheapest fix.
- **B — Route alias**: add a new controller mapping (e.g. `/api/movies` → existing `/api/contents` service).
- **C — Frontend adapter**: front-end response transformer translates BE → FE shape. Use when BE shape is correct and consumed by other clients.
- **D — Domain extension + migration**: add a new column/entity/table.
- **E — New endpoint**: implement a service + controller action the BE doesn't have today.
- **F — Defer + stub**: ship a `null`/`[]` placeholder until a real feature lands.

---

## A. Routes & response envelope

| # | Endpoint / Field | Frontend Expects | Backend Returns | Severity | Solution Strategy |
|---|------------------|------------------|-----------------|----------|-------------------|
| 1 | Catalog base path | `GET /api/movies?page=&limit=&genre=&year=&rating=&search=` | `GET /api/contents` with `?Page=&PageSize=&GenreId=&Year=&Search=&Type=` — no `rating` filter, `limit`→`pageSize`, `genre`→`GenreId` (Guid) (`ContentsController.cs:18`, `ContentDtos.cs:142-152`) | **Critical** | B (route alias `/api/movies`) + A (accept `limit`, `genre` slug, `rating` query params) |
| 2 | List response envelope | `{ data: Movie[], total: number, page: number, totalPages: number }` | `PagedResult<T>` → `{ items, totalCount, page, pageSize, totalPages, hasNextPage, hasPreviousPage }` (`Common/Result.cs:29-39`) | **Critical** | A (rename `items`→`data`, `totalCount`→`total` via DTO) or C (FE adapter). Recommend A. |
| 3 | Featured | `GET /api/movies/featured` | `GET /api/contents/featured?limit=` (`ContentsController.cs:26`) | **High** | B |
| 4 | Detail by id | `GET /api/movies/{id}` | `GET /api/contents/{id:guid}` (`ContentsController.cs:40`) | **High** | B |
| 5 | Related | `GET /api/movies/{id}/related` | `GET /api/contents/{id}/similar` (`ContentsController.cs:54`) | **High** | B (`/related` alias to `similar`) |
| 6 | Comments list | `GET /api/movies/{id}/comments` | `GET /api/reviews/content/{id}` (`ReviewsController.cs:21`) | **High** | B (mount alias under `/api/movies/{id}/comments`) |
| 7 | Comment create | `POST /api/movies/{id}/comments` body `{content, rating}` | `POST /api/reviews` body `{contentId, rating, comment}` (`ReviewsController.cs:33`, `UserActivityDtos.cs:14`) | **High** | B + A (accept `content`/`rating`, infer `contentId` from path) |
| 8 | Profile | `GET /api/auth/profile` | `GET /api/auth/me` (`AuthController.cs:55`) | **High** | B (alias `/profile` → `/me`) |

---

## B. Auth payload shapes

| # | Endpoint / Field | Frontend Expects | Backend Returns | Severity | Solution Strategy |
|---|------------------|------------------|-----------------|----------|-------------------|
| 9 | Login request body | `{ email, password }` | `LoginRequest { EmailOrUsername, Password }` (`AuthDtos.cs:9`) | **Critical** | A (rename property to `email` via JSON attribute and accept it; keep `emailOrUsername` for back-compat) |
| 10 | Register request body | `{ name, email, password }` | `RegisterRequest { Username, Email, Password, FullName }` (`AuthDtos.cs:3`) | **Critical** | A (map `name` → split into `FullName`; auto-generate `Username` from email or `name`) |
| 11 | Auth response shape | `{ token, user }` | `AuthResponse { accessToken, refreshToken, accessTokenExpiry, user }` (`AuthDtos.cs:26`) | **High** | A (add a top-level `token` mirror of `accessToken`; FE refresh logic still gets the refresh token) |

---

## C. User model fields

Frontend expects:
```
{ id, email, username, firstName, lastName, avatar, role: 'user'|'admin',
  subscription: { type, expiresAt }, preferences: { language, quality, autoplay },
  watchHistory: [], favorites: [], playlists: [], createdAt, updatedAt }
```
Backend `UserDto` (`AuthDtos.cs:17-24`) returns:
`{ Id, Username, Email, FullName, AvatarUrl, Role, CreatedAt }`.

| # | Endpoint / Field | Frontend Expects | Backend Returns | Severity | Solution Strategy |
|---|------------------|------------------|-----------------|----------|-------------------|
| 12 | `firstName` / `lastName` | Two separate fields | One `FullName` (`Domain/Entities/User.cs:12`) | **High** | D (add `FirstName`, `LastName` columns + migration; keep `FullName` as computed) |
| 13 | `avatar` | string url | `AvatarUrl` (`AuthDtos.cs:22`) | **Low** | A (rename via JSON attribute) |
| 14 | `role` casing | `'user'`/`'admin'`/`'moderator'` lowercase | `"User"`/`"Admin"`/`"Moderator"` (`JwtTokenService.cs:37`, `AuthService.cs:167`) | **Med** | A (`.ToLowerInvariant()` in `UserDto` mapping) |
| 15 | `subscription` object | `{ type, expiresAt }` | not present | **Low** | F now → D in Phase 4 (`Subscriptions/Plans`) |
| 16 | `preferences` object | `{ language, quality, autoplay }` | not present | **High** | D (`UserPreference` 1:1 table + Phase 3 endpoint) |
| 17 | `watchHistory[]` / `favorites[]` / `playlists[]` embedded in user | inline arrays on profile | Backend exposes via dedicated endpoints | **Med** | C (FE composes on profile page) — keep BE normalized |
| 18 | `updatedAt` | always present | `UpdatedAt` is `DateTime?` (`BaseEntity.cs:7`) | **Low** | A (fall back to `CreatedAt` when null in the DTO) |

---

## D. Movie model fields

Frontend expects:
```
{ id, title, titleUz, titleRu, description, descriptionUz, descriptionRu,
  poster, backdrop, trailer, streamUrl, duration, releaseYear, rating,
  genres: string[], director, cast: string[], country, language, quality,
  views, likes, dislikes, createdAt, updatedAt }
```
Backend `ContentDetailDto` (`ContentDtos.cs:44-70`) returns the columns from `Content.cs:8-39`.

| # | Endpoint / Field | Frontend Expects | Backend Returns | Severity | Solution Strategy |
|---|------------------|------------------|-----------------|----------|-------------------|
| 19 | `titleUz` / `titleRu` / `descriptionUz` / `descriptionRu` | i18n fields per locale | Single `Title` + `Description` (`Content.cs:8,10`) | **High** | D (Phase 2: `ContentTranslation { ContentId, Locale, Title, Description }`) |
| 20 | `poster`, `backdrop`, `trailer`, `streamUrl` | flat names | `PosterUrl`, `BackdropUrl`, `TrailerUrl`, `VideoUrl` (+ `HlsManifestUrl`) (`Content.cs:18-22`) | **Med** | A (rename via JSON attributes; pick `HlsManifestUrl ?? VideoUrl` as `streamUrl`) |
| 21 | `duration` | minutes as number | `DurationMinutes` (`Content.cs:31`) | **Low** | A |
| 22 | `rating` | single number `0..10` | `AverageRating` + separate `RatingCount` (`Content.cs:34-35`) | **Low** | A (rename `AverageRating`→`rating`; keep `ratingCount` alongside) |
| 23 | `genres: string[]` | flat name array | List view: `List<string>` ✅; detail view: `List<GenreDto>` ❌ (`ContentDtos.cs:42, 69`) | **High** | A (make detail also return `string[]`; expose richer object as `genreDetails`) |
| 24 | `cast: string[]` | array | `Cast: string?` (CSV) (`Content.cs:25`) | **High** | A short-term (split by comma in DTO) → D long-term (`ContentCastMember` join table) |
| 25 | `quality` | enum/string | not exposed (only enum `VideoQuality` exists but unused on `Content`) (`Domain/Enums/Enums.cs:19-24`) | **Med** | D (add `Content.Quality` column or expose via streams table) |
| 26 | `views` | number | `ViewCount` (`Content.cs:36`) | **Low** | A |
| 27 | `likes` / `dislikes` | counters | not exposed; `Review.LikesCount` exists but at review level, not content level (`UserActivity.cs:41`) | **High** | D (Phase 2 — `Vote { UserId, ContentId, Value=±1 }`; aggregate counts onto `Content`) |
| 28 | `updatedAt` | always present | `DateTime?` | **Low** | A (fallback to `CreatedAt`) |

---

## E. Filters & paging

| # | Endpoint / Field | Frontend Expects | Backend Returns | Severity | Solution Strategy |
|---|------------------|------------------|-----------------|----------|-------------------|
| 29 | `limit` vs `pageSize` | `?limit=20` | `?pageSize=20` (`ContentDtos.cs:151`) | **High** | A (accept both via DTO with `[FromQuery(Name="limit")]` fallback) |
| 30 | `genre` query | `?genre=action` (slug) | `?GenreId={guid}` only (`ContentDtos.cs:146`) | **High** | A (accept either slug or Guid in filter) |
| 31 | `rating` query | `?rating=7` (minimum) | unsupported | **High** | E (add `MinRating` filter in `ContentFilterRequest` + index on `AverageRating`) |
| 32 | `year` query | `?year=2024` | supported (`ContentDtos.cs:147`, `ContentService.cs:46`) | — | — |
| 33 | Sort | usually implicit "newest" | `?sortBy=popular|rating|newest` (`ContentService.cs:51-57`) | **Low** | document — no fix needed |

---

## F. Comments (review) shape

| # | Endpoint / Field | Frontend Expects | Backend Returns | Severity | Solution Strategy |
|---|------------------|------------------|-----------------|----------|-------------------|
| 34 | Body field name | `content` | `comment` (`UserActivityDtos.cs:14`) | **High** | A (alias `content` ↔ `comment`) |
| 35 | Rating scale | 1–5 stars (likely) | 1–10 (`Validators.cs:66`) | **Med** | Decide canonical scale; mirror via FE adapter or change validator |
| 36 | Author object | `{ id, username, avatar }` | flat fields `username`, `userAvatar` (`UserActivityDtos.cs:3-12`) | **Med** | A (nest under `author` in a new DTO; keep flat for back-compat) |

---

## G. Watchlist / Favorites / Playlists semantics

| # | Endpoint / Field | Frontend Expects | Backend Returns | Severity | Solution Strategy |
|---|------------------|------------------|-----------------|----------|-------------------|
| 37 | `watchlist` vs `favorites` | Two distinct lists (FE has both `favorites` and "watchlist"/"playlists") | Only `Favorite` join entity (`Domain/Entities/UserActivity.cs:5-12`) | **Med** | D (Phase 2 prompt 5: introduce `Watchlist`, `Playlist`, `PlaylistItem` entities) |
| 38 | Playlists CRUD | per-user named playlists | none | **Med** | E (Phase 2 prompt 5) |

---

## H. Admin / Subscriptions

| # | Endpoint / Field | Frontend Expects | Backend Returns | Severity | Solution Strategy |
|---|------------------|------------------|-----------------|----------|-------------------|
| 39 | Admin users page | list / role / ban endpoints | none | **Med** | E (Phase 3 prompt 7) |
| 40 | Admin dashboard metrics | counters & charts | none | **Med** | E (Phase 3 prompt 7) |
| 41 | Subscription plans / billing | plans + user.subscription | none | **Low** | E (Phase 4 prompt 8) |

---

## Quick severity tally

- Critical: 3 (rows 1, 2, 9)
- High: 16
- Med: 17
- Low: 5
- **Total gaps**: 41

The Phase 1 ("Compatibility") prompt should close every Critical + most High rows that map to solution strategies **A** or **B**. Phases 2–4 own the strategy **D**/**E** rows. See `05-roadmap.md`.
